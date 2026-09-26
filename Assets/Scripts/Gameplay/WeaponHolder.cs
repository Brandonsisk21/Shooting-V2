using System;
using ArenaShooter.Core;
using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Carries a <see cref="Loadout"/>, fires hitscan shots from the aim camera, handles the sniper
    /// scope and weapon pickups. Driven by <see cref="PlayerController"/> (and later by bots).
    /// </summary>
    public class WeaponHolder : MonoBehaviour
    {
        public Camera aimCamera;
        public AudioSource audioSource;
        public WeaponStats spawnWeapon = WeaponStats.Rifle();
        public float switchTime = 0.4f;
        [Tooltip("A click this close to the next allowed shot still fires when the weapon is ready (seconds).")]
        public float fireBufferTime = 0.12f;
        public float pickupRadius = 1.6f;
        [Tooltip("Seconds before a weapon dropped by a swap disappears.")]
        public float droppedWeaponLifetime = 30f;

        public Loadout Loadout { get; private set; }
        public bool IsZoomed { get; private set; }
        public float CurrentZoom => IsZoomed && Loadout.Active != null ? Loadout.Active.Stats.zoom : 1f;
        public WeaponPickup NearbyPickup { get; private set; }
        public PickupOutcome NearbyPickupOutcome { get; private set; }

        public event Action<HitInfo> HitConfirmed;

        private Transform _owner;
        private Transform _viewModel;
        private Transform _muzzle;
        private float _fireQueuedUntil = float.NegativeInfinity;
        private float _muzzleFlashUntil;
        private GameObject _muzzleFlash;
        private readonly Collider[] _overlap = new Collider[16];

        private void Awake()
        {
            _owner = transform.root;
            Loadout = new Loadout(switchTime);
            Loadout.ActiveChanged += OnActiveChanged;
            ResetLoadout();
        }

        private void Start()
        {
            // aimCamera is usually assigned after Awake, so build the first view model here.
            if (_viewModel == null) BuildViewModel(Loadout.Active);
        }

        /// <summary>Back to the spawn loadout: just the rifle (GDD 2.2). A carried sniper is lost.</summary>
        public void ResetLoadout()
        {
            Loadout.Clear();
            Loadout.Give(new WeaponState(spawnWeapon));
            IsZoomed = false;
            _fireQueuedUntil = float.NegativeInfinity;
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
                    return;
            }
            PlayOneShot(ProceduralAudio.Pickup);
            NearbyPickup = null;
        }

        private void Update()
        {
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
        }

        private void FireHitscan(WeaponState weapon)
        {
            WeaponStats stats = weapon.Stats;
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, stats.range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 end = ray.origin + ray.direction * stats.range;
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(_owner)) continue;

                end = hit.point;
                var hitbox = hit.collider.GetComponent<Hitbox>();
                Health target = hitbox != null ? hitbox.Owner : null;
                if (target != null && !target.IsDead)
                {
                    DamageResult damage = target.TakeDamage(stats.DamageFor(hitbox.zone));
                    var info = new HitInfo(target, hitbox.zone, damage, hit.point);
                    PlayOneShot(damage.Killed ? ProceduralAudio.Kill : hitbox.zone == HitZone.Head ? ProceduralAudio.HitHead : ProceduralAudio.HitBody);
                    HitConfirmed?.Invoke(info);
                }
                else
                {
                    ShotEffects.Impact(hit.point, hit.normal);
                }
                break;
            }

            Vector3 from = _muzzle != null && !IsZoomed ? _muzzle.position : ray.origin + ray.direction * 0.3f + Vector3.down * 0.1f;
            bool sniper = stats.id == "sniper";
            ShotEffects.Tracer(from, end, sniper ? new Color(0.6f, 0.9f, 1f, 0.9f) : new Color(1f, 0.85f, 0.4f, 0.8f), sniper ? 0.05f : 0.02f, sniper ? 0.25f : 0.06f);
            PlayOneShot(ProceduralAudio.Gunshot(stats.id));
            _muzzleFlashUntil = Time.time + 0.05f;
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
            if (weapon == null || aimCamera == null) return;

            bool sniper = weapon.Stats.id == "sniper";
            float length = sniper ? 0.9f : 0.6f;
            var root = new GameObject("ViewModel_" + weapon.Stats.id).transform;
            root.SetParent(aimCamera.transform, false);
            root.localPosition = new Vector3(0.22f, -0.2f, 0.35f);

            Color bodyColor = sniper ? new Color(0.2f, 0.3f, 0.25f) : new Color(0.25f, 0.3f, 0.4f);
            GrayBox.Visual(PrimitiveType.Cube, "Body", root, new Vector3(0f, 0f, length * 0.5f), new Vector3(0.07f, 0.1f, length), bodyColor);
            GrayBox.Visual(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.09f, 0.08f), new Vector3(0.05f, 0.12f, 0.06f), bodyColor * 0.7f);
            if (sniper)
            {
                var scope = GrayBox.Visual(PrimitiveType.Cylinder, "Scope", root, new Vector3(0f, 0.08f, 0.35f), new Vector3(0.05f, 0.14f, 0.05f), new Color(0.1f, 0.1f, 0.1f));
                scope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(root, false);
            _muzzle.localPosition = new Vector3(0f, 0.01f, length + 0.02f);

            _muzzleFlash = GrayBox.Visual(PrimitiveType.Sphere, "MuzzleFlash", _muzzle, Vector3.zero, Vector3.one * 0.09f, new Color(1f, 0.8f, 0.3f));
            _muzzleFlash.GetComponent<Renderer>().sharedMaterial = GrayBox.VertexColorUnlit;
            _muzzleFlash.SetActive(false);

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
