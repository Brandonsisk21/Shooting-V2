using System;
using System.Collections.Generic;
using ArenaShooter.Core;
using ArenaShooter.Core.Net;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Carries a <see cref="Loadout"/>, fires hitscan shots along the aim transform, handles the
    /// sniper scope and weapon pickups. Driven by <see cref="PlayerController"/> or <see cref="BotController"/>.
    /// </summary>
    /// <summary>A shot fired by an online client's own grunt, for the host to validate and apply.</summary>
    public struct ShotReport
    {
        public Health Target;
        public HitZone Zone;
        public Vector3 From;
        public Vector3 To;
        public string WeaponId;
    }

    public class WeaponHolder : MonoBehaviour
    {
        [Tooltip("Online client's own grunt: shots and pickups are sent to the host instead of applied locally.")]
        public bool remoteAuthority;
        [Tooltip("Someone else's grunt driven by the network: only shows their weapon and shot effects.")]
        public bool isProxy;
        [Tooltip("Shots travel along this transform's forward (camera for the player, head pivot for bots).")]
        public Transform aim;
        [Tooltip("Player only: the camera the first-person view model hangs from.")]
        public Camera viewModelCamera;
        [Tooltip("Where bolts start when there's no view model (bots' gun muzzle). Set automatically when thirdPersonGunMount is used.")]
        public Transform worldMuzzle;
        [Tooltip("Bots: the hand/head point the held weapon's model attaches to.")]
        public Transform thirdPersonGunMount;
        public AudioSource audioSource;
        public WeaponStats spawnWeapon = WeaponStats.Rifle();
        public float switchTime = 0.4f;
        [Tooltip("A click this close to the next allowed shot still fires when the weapon is ready (seconds).")]
        public float fireBufferTime = 0.12f;
        public float pickupRadius = 1.6f;
        [Tooltip("Seconds before a weapon dropped by a swap or death disappears.")]
        public float droppedWeaponLifetime = 30f;
        [Tooltip("Track which combatant is under the crosshair (red reticle, aim assist). Player only.")]
        public bool trackAimTarget;

        public Loadout Loadout { get; private set; }
        public Combatant Owner => _ownerCombatant != null ? _ownerCombatant : _ownerCombatant = GetComponent<Combatant>();
        public bool IsZoomed { get; private set; }
        public float CurrentZoom => IsZoomed && Loadout.Active != null ? Loadout.Active.Stats.zoom : 1f;
        public WeaponPickup NearbyPickup { get; private set; }
        public PickupOutcome NearbyPickupOutcome { get; private set; }
        /// <summary>Living enemy under the crosshair within weapon range, if <see cref="trackAimTarget"/>.</summary>
        public Combatant AimTarget { get; private set; }

        /// <summary>This holder confirmed a hit on something with health.</summary>
        public event Action<HitInfo> HitConfirmed;
        /// <summary>This holder fired a shot.</summary>
        public event Action<WeaponStats> Fired;
        /// <summary>Any holder fired (bots use this to hear gunfire).</summary>
        /// <summary>Any holder fired: shooter, bolt start, bolt end, weapon id (bots hear it; the host relays it online).</summary>
        public static event Action<Combatant, Vector3, Vector3, string> AnyShotFired;
        /// <summary>Online client: our shot, to send to the host.</summary>
        public event Action<ShotReport> ShotReported;
        /// <summary>Online client: we want this pickup; the host decides.</summary>
        public event Action<WeaponPickup> PickupRequested;

        private Transform _owner;
        private Combatant _ownerCombatant;
        private Transform _viewModel;
        private Transform _muzzle;
        private float _fireQueuedUntil = float.NegativeInfinity;
        private float _muzzleFlashUntil;
        private GameObject _muzzleFlash;
        private readonly Collider[] _overlap = new Collider[16];
        private RaycastHit[] _hits = new RaycastHit[32];

        private void Awake()
        {
            _owner = transform.root;
            Loadout = new Loadout(switchTime);
            Loadout.ActiveChanged += OnActiveChanged;
            ResetLoadout();
        }

        private void Start()
        {
            // Cameras/aim are usually assigned after Awake, so build the first view model here.
            if (_viewModel == null) BuildViewModel(Loadout.Active);
        }

        /// <summary>Back to the spawn loadout: just the rifle (GDD 2.2).</summary>
        public void ResetLoadout()
        {
            Loadout.Clear();
            Loadout.Give(new WeaponState(spawnWeapon));
            IsZoomed = false;
            _fireQueuedUntil = float.NegativeInfinity;
        }

        /// <summary>
        /// Drops carried power weapons where the owner died (GDD 2.3.1). The spawn weapon isn't
        /// dropped and empty weapons are discarded. Dropped weapons despawn like swapped ones.
        /// </summary>
        public void DropOnDeath()
        {
            IsZoomed = false;
            var drops = Loadout.TakeDeathDrops(spawnWeapon.id);
            for (int i = 0; i < drops.Count; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 90f, 0f) * (Vector3.forward * 0.5f * i);
                WeaponPickup.Create(drops[i], GroundPointBelow(_owner.position + offset) + Vector3.up * 0.5f, droppedWeaponLifetime);
            }
            BuildViewModel(Loadout.Active);
        }

        public void QueueFire() => _fireQueuedUntil = Time.time + fireBufferTime;

        public void ToggleZoom()
        {
            var active = Loadout.Active;
            if (active == null || !active.Stats.HasScope || Loadout.IsSwitching || active.IsReloading) return;
            IsZoomed = !IsZoomed;
        }

        public void Reload()
        {
            if (Loadout.TryReload()) PlayReloadFeedback();
        }

        public void SwitchNext() => Loadout.SwitchNext();

        public void SwitchTo(int index) => Loadout.SwitchTo(index);

        public void TryPickup()
        {
            var pickup = NearbyPickup;
            if (pickup == null) return;
            if (remoteAuthority)
            {
                PickupRequested?.Invoke(pickup);
                NearbyPickup = null;
                return;
            }
            PickupFrom(pickup);
        }

        /// <summary>Takes a pickup (also used by the host on behalf of online players).</summary>
        public PickupResult PickupFrom(WeaponPickup pickup)
        {
            PickupResult result = Loadout.Pickup(pickup.Weapon);
            switch (result.Outcome)
            {
                case PickupOutcome.Added:
                    pickup.Consume();
                    break;
                case PickupOutcome.Swapped:
                    pickup.Consume();
                    WeaponPickup.Create(result.Dropped, _owner.position + _owner.forward * 0.6f + Vector3.up * 0.5f, droppedWeaponLifetime);
                    break;
                case PickupOutcome.AmmoTaken:
                    if (pickup.Weapon.Magazine + pickup.Weapon.Reserve == 0) pickup.Consume();
                    break;
                default:
                    return result;
            }
            PlayOneShot(ProceduralAudio.Pickup);
            NearbyPickup = null;
            return result;
        }

        /// <summary>Online client: apply what the host granted us.</summary>
        public void ApplyGrantedPickup(PickupOutcome outcome, string weaponId, int magazine, int reserve, int ammoTaken)
        {
            var stats = WeaponIds.Stats(WeaponIds.ToByte(weaponId));
            if (stats == null) return;
            if (outcome == PickupOutcome.AmmoTaken) Loadout.Find(weaponId)?.AddReserve(ammoTaken);
            else Loadout.Pickup(new WeaponState(stats, magazine, reserve)); // swapped-out weapon is dropped by the host
            PlayOneShot(ProceduralAudio.Pickup);
        }

        /// <summary>Online client: on death, lose everything but the rifle (the host drops them).</summary>
        public void DiscardOnDeath()
        {
            IsZoomed = false;
            Loadout.TakeDeathDrops(spawnWeapon.id);
            BuildViewModel(Loadout.Active);
        }

        /// <summary>Host: mirror what an online player says they carry (for death drops and pickups).</summary>
        public void ApplyMirrorLoadout(List<SlotState> slots, int activeIndex)
        {
            bool same = slots.Count == Loadout.Slots.Count;
            for (int i = 0; same && i < slots.Count; i++) same = Loadout.Slots[i].Stats.id == WeaponIds.ToId(slots[i].Weapon);
            if (!same)
            {
                Loadout.Clear();
                foreach (var slot in slots)
                {
                    var stats = WeaponIds.Stats(slot.Weapon);
                    if (stats != null) Loadout.Give(new WeaponState(stats));
                }
                if (Loadout.Slots.Count == 0) Loadout.Give(new WeaponState(spawnWeapon));
            }
            for (int i = 0; i < slots.Count && i < Loadout.Slots.Count; i++) Loadout.Slots[i].SetAmmo(slots[i].Magazine, slots[i].Reserve);
            if (activeIndex != Loadout.ActiveIndex) Loadout.SwitchTo(activeIndex);
        }

        /// <summary>Online client: show the weapon a remote grunt is holding.</summary>
        public void SetDisplayedWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || (Loadout.Active != null && Loadout.Active.Stats.id == weaponId)) return;
            var stats = WeaponIds.Stats(WeaponIds.ToByte(weaponId));
            if (stats == null) return;
            Loadout.Clear();
            Loadout.Give(new WeaponState(stats));
        }

        /// <summary>Draws and plays a shot that happened somewhere else on the network.</summary>
        public void PlayRemoteShot(Vector3 from, Vector3 to, string weaponId)
        {
            Vector3 start = worldMuzzle != null ? worldMuzzle.position : from;
            if (weaponId == "sniper") ShotEffects.ZapBeam(start, to, WeaponModels.ZapGlow);
            else ShotEffects.PewBolt(start, to, WeaponModels.PewGlow);
            PlayOneShot(ProceduralAudio.Gunshot(weaponId));
            AnyShotFired?.Invoke(Owner, start, to, weaponId);
        }

        private void Update()
        {
            if (isProxy) return; // driven by the network: no firing, reloading or pickups here
            if (Owner != null && !Owner.IsAlive)
            {
                NearbyPickup = null;
                AimTarget = null;
                return;
            }

            bool wasReloading = Loadout.Active != null && Loadout.Active.IsReloading;
            Loadout.Tick(Time.deltaTime);

            var active = Loadout.Active;
            if (active != null && active.IsReloading) IsZoomed = false;
            if (wasReloading && active != null && !active.IsReloading) PlayOneShot(ProceduralAudio.Reload);

            if (Time.time <= _fireQueuedUntil && Loadout.TryFire())
            {
                _fireQueuedUntil = float.NegativeInfinity;
                FireHitscan(active);
                if (active.IsReloading) PlayReloadFeedback(); // auto-reload on empty
            }

            if (_muzzleFlash != null) _muzzleFlash.SetActive(Time.time < _muzzleFlashUntil && !IsZoomed);
            if (_viewModel != null) _viewModel.gameObject.SetActive(!IsZoomed);

            ScanForPickups();
            if (trackAimTarget) UpdateAimTarget();
        }

        /// <summary>First thing a shot along <paramref name="ray"/> would hit, skipping the shooter, pickups and body blockers.</summary>
        public bool TraceShot(Ray ray, float range, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(ray, _hits, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (count == _hits.Length) _hits = new RaycastHit[_hits.Length * 2];
            hit = default;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var h = _hits[i];
                if (h.distance >= best || !IsShotBlocker(h.collider, _owner)) continue;
                best = h.distance;
                hit = h;
            }
            return best < float.MaxValue;
        }

        /// <summary>
        /// Whether a collider stops bullets: world geometry and hitboxes do; the shooter, pickup
        /// triggers and CharacterController capsules (hitboxes sit inside them) don't.
        /// </summary>
        public static bool IsShotBlocker(Collider collider, Transform shooter)
        {
            if (shooter != null && collider.transform.IsChildOf(shooter)) return false;
            if (collider is CharacterController) return false;
            if (collider.isTrigger) return collider.GetComponent<Hitbox>() != null;
            return true;
        }

        private void FireHitscan(WeaponState weapon)
        {
            WeaponStats stats = weapon.Stats;
            var ray = new Ray(aim.position, aim.forward);
            Vector3 end = ray.origin + ray.direction * stats.range;
            bool reported = false;

            if (TraceShot(ray, stats.range, out RaycastHit hit))
            {
                end = hit.point;
                var hitbox = hit.collider.GetComponent<Hitbox>();
                Health target = hitbox != null ? hitbox.Owner : null;
                if (target != null && !target.IsDead)
                {
                    DamageResult damage;
                    if (remoteAuthority)
                    {
                        // Online client: show the hit right away (predicted); the host applies it.
                        float amount = Mathf.Min(stats.DamageFor(hitbox.zone), target.Current);
                        damage = new DamageResult(amount, amount >= target.Current);
                        ShotReported?.Invoke(new ShotReport { Target = target, Zone = hitbox.zone, From = ray.origin, To = hit.point, WeaponId = stats.id });
                        reported = true;
                    }
                    else
                    {
                        damage = target.TakeDamage(stats.DamageFor(hitbox.zone), new DamageSource(Owner, stats.id, hitbox.zone));
                    }
                    PlayOneShot(damage.Killed ? ProceduralAudio.Kill : hitbox.zone == HitZone.Head ? ProceduralAudio.HitHead : ProceduralAudio.HitBody);
                    HitConfirmed?.Invoke(new HitInfo(target, hitbox.zone, damage, hit.point));
                }
                else if (target == null)
                {
                    ShotEffects.Impact(hit.point, hit.normal, stats.id == "sniper" ? WeaponModels.ZapGlow : WeaponModels.PewGlow);
                }
            }

            if (remoteAuthority && !reported)
                ShotReported?.Invoke(new ShotReport { Target = null, From = ray.origin, To = end, WeaponId = stats.id }); // a miss: others still see the bolt

            Vector3 from = _muzzle != null && !IsZoomed ? _muzzle.position
                : worldMuzzle != null ? worldMuzzle.position
                : ray.origin + ray.direction * 0.3f + Vector3.down * 0.1f;
            if (stats.id == "sniper") ShotEffects.ZapBeam(from, end, WeaponModels.ZapGlow);
            else ShotEffects.PewBolt(from, end, WeaponModels.PewGlow);
            PlayOneShot(ProceduralAudio.Gunshot(stats.id));
            _muzzleFlashUntil = Time.time + 0.05f;
            Fired?.Invoke(stats);
            AnyShotFired?.Invoke(Owner, from, end, stats.id);
        }

        private void UpdateAimTarget()
        {
            AimTarget = null;
            var active = Loadout.Active;
            if (active == null || aim == null) return;
            if (!TraceShot(new Ray(aim.position, aim.forward), active.Stats.range, out RaycastHit hit)) return;
            var hitbox = hit.collider.GetComponent<Hitbox>();
            if (hitbox == null) return;
            var target = hitbox.GetComponentInParent<Combatant>();
            if (target != null && target != Owner && target.IsAlive) AimTarget = target;
        }

        private void ScanForPickups()
        {
            NearbyPickup = null;
            NearbyPickupOutcome = PickupOutcome.None;
            int count = Physics.OverlapSphereNonAlloc(_owner.position + Vector3.up * 0.9f, pickupRadius, _overlap, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var pickup = _overlap[i].GetComponentInParent<WeaponPickup>();
                if (pickup == null || pickup.Weapon == null) continue;
                PickupOutcome outcome = Loadout.Preview(pickup.Weapon);
                if (outcome == PickupOutcome.None) continue;
                float distance = (pickup.transform.position - _owner.position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                NearbyPickup = pickup;
                NearbyPickupOutcome = outcome;
            }
        }

        /// <summary>Where a dropped weapon should rest, so one dropped mid-jump doesn't float in the air.</summary>
        private Vector3 GroundPointBelow(Vector3 position)
        {
            var hits = Physics.RaycastAll(position + Vector3.up * 0.5f, Vector3.down, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = position;
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(_owner) || hit.collider is CharacterController) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                ground = hit.point;
            }
            return ground;
        }

        private void OnActiveChanged(WeaponState weapon)
        {
            IsZoomed = false;
            BuildViewModel(weapon);
        }

        private void BuildViewModel(WeaponState weapon)
        {
            if (_viewModel != null) Destroy(_viewModel.gameObject);
            _viewModel = null;
            _muzzle = null;
            _muzzleFlash = null;
            if (weapon == null) return;

            if (viewModelCamera == null)
            {
                // Third person (bots): swap the gun in their hands to match the held weapon.
                if (thirdPersonGunMount != null)
                {
                    _viewModel = new GameObject("HeldWeapon").transform;
                    _viewModel.SetParent(thirdPersonGunMount, false);
                    worldMuzzle = WeaponModels.Build(weapon.Stats.id, _viewModel);
                }
                return;
            }

            var root = new GameObject("ViewModel_" + weapon.Stats.id).transform;
            root.SetParent(viewModelCamera.transform, false);
            root.localPosition = new Vector3(0.2f, -0.19f, 0.3f);
            _muzzle = WeaponModels.Build(weapon.Stats.id, root);

            bool sniper = weapon.Stats.id == "sniper";
            _muzzleFlash = GrayBox.GlowVisual(PrimitiveType.Sphere, "MuzzleFlash", _muzzle, Vector3.zero, Vector3.one * 0.12f,
                sniper ? WeaponModels.ZapGlow : WeaponModels.PewGlow);
            _muzzleFlash.SetActive(false);
            _viewModel = root;
        }

        private void PlayReloadFeedback()
        {
            IsZoomed = false;
            PlayOneShot(ProceduralAudio.Reload);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
