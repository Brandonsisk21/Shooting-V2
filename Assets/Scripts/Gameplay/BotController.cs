using System.Collections.Generic;
using ArenaShooter.Core;
using UnityEngine;
using UnityEngine.AI;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Autonomous FFA bot. No set routes: it roams to random reachable spots (or toward the sniper
    /// and dropped weapons), notices enemies it can see, hear or that shoot it, and fights with
    /// human-like limits: reaction delay, capped turn speed, aim error that shrinks while tracking,
    /// strafing and the occasional jump. Uses the same motor and weapons as the player.
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public class BotController : MonoBehaviour
    {
        public BotSkill skill = BotSkill.Normal();
        [Tooltip("Pitch pivot at eye height; yaw turns the whole body.")]
        public Transform head;
        [Tooltip("Where the bot may roam.")]
        public Bounds roamArea = new Bounds(Vector3.zero, new Vector3(40f, 10f, 60f));
        public float perceptionInterval = 0.15f;
        [Tooltip("Gunfire within this range draws the bot's attention.")]
        public float hearingRange = 35f;
        [Tooltip("Enemies this close are noticed even behind the bot.")]
        public float closeAwareness = 6f;
        [Tooltip("Preferred fighting distance with the rifle (min, max).")]
        public Vector2 rifleRange = new Vector2(8f, 22f);
        [Tooltip("Preferred fighting distance with the sniper (min, max).")]
        public Vector2 sniperRange = new Vector2(15f, 50f);

        private Combatant _self;
        private PlayerMotor _motor;
        private WeaponHolder _weapons;

        private float _yaw, _pitch;
        private float _noiseSeed;
        private float _nextPerception;

        private Combatant _target;
        private bool _targetVisible;
        private float _acquiredAt;
        private float _trackingTime;
        private bool _aimHead;
        private Vector3 _lastSeenPos;
        private float _lastSeenTime;
        private float _nextTriggerAt;

        private Combatant _lastAttacker;
        private float _lastAttackedAt;
        private Vector3? _investigate;
        private float _investigateUntil;

        private NavMeshPath _path;
        private readonly List<Vector3> _corners = new List<Vector3>();
        private int _cornerIndex;
        private Vector3 _destination;
        private bool _hasPath;
        private float _repathAt;
        private bool _hasRoamGoal;
        private Vector3 _roamGoal;
        private float _roamGiveUpAt;
        private float _nextRoamPickAt;

        private float _strafeDir = 1f;
        private float _strafeSwitchAt;
        private float _stuckTime;

        private void Awake()
        {
            _self = GetComponent<Combatant>();
            _motor = GetComponent<PlayerMotor>();
            _weapons = GetComponent<WeaponHolder>();
            _path = new NavMeshPath();
            _noiseSeed = Random.value * 100f;
            _nextPerception = Time.time + Random.value * perceptionInterval; // stagger bots
        }

        private void OnEnable() => WeaponHolder.AnyShotFired += OnShotHeard;
        private void OnDisable() => WeaponHolder.AnyShotFired -= OnShotHeard;

        private void Start()
        {
            _self.Health.Damaged += OnDamaged;
            _self.Respawned += _ => ResetState();
            ResetState();
        }

        private void ResetState()
        {
            _target = null;
            _targetVisible = false;
            _investigate = null;
            _hasPath = false;
            _hasRoamGoal = false;
            _stuckTime = 0f;
            _yaw = transform.eulerAngles.y;
            _pitch = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!_self.IsAlive || dt <= 0f) return;

            if (Time.time >= _nextPerception)
            {
                Perceive();
                _nextPerception = Time.time + perceptionInterval;
            }
            _trackingTime = _targetVisible ? _trackingTime + dt : Mathf.Max(0f, _trackingTime - dt * 2f);

            ChooseWeapon();

            Vector3 move;
            bool jump = false;
            float lookYaw, lookPitch, turnScale = 1f;

            if (_target != null && _target.IsAlive && _targetVisible)
            {
                move = CombatMove(ref jump);
                AimAt(out lookYaw, out lookPitch);
                TryShoot(lookYaw, lookPitch);
            }
            else
            {
                if (_target != null && (!_target.IsAlive || Time.time - _lastSeenTime > 4f)) _target = null;

                Vector3 goal;
                if (_target != null) goal = _lastSeenPos; // chase where we last saw them
                else if (_investigate.HasValue && Time.time < _investigateUntil) goal = _investigate.Value;
                else
                {
                    _investigate = null;
                    goal = RoamGoal();
                }

                move = NavigateTo(goal, out bool arrived);
                if (arrived)
                {
                    if (_target != null) _target = null;
                    else if (_investigate.HasValue) _investigate = null;
                    else _hasRoamGoal = false;
                }

                Vector3 lookDir = _investigate.HasValue ? _investigate.Value - head.position : move;
                if (_target != null) lookDir = _lastSeenPos - head.position;
                FaceDirection(lookDir, out lookYaw, out lookPitch);
                turnScale = 0.6f;
                MaybeReload();
            }

            float turn = skill.turnSpeed * turnScale * dt;
            _yaw = Mathf.MoveTowardsAngle(_yaw, lookYaw, turn);
            _pitch = Mathf.MoveTowardsAngle(_pitch, lookPitch, turn * 0.7f);
            _pitch = Mathf.Clamp(_pitch, -80f, 80f);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            HandleStuck(move, ref jump, dt);
            var input = new Vector2(Vector3.Dot(move, transform.right), Vector3.Dot(move, transform.forward));
            _motor.Move(Vector2.ClampMagnitude(input, 1f), jump, dt);

            if (_weapons.NearbyPickup != null) _weapons.TryPickup();
        }

        // ---------------------------------------------------------------- perception

        private void Perceive()
        {
            Vector3 lookForward = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
            Combatant best = null;
            float bestScore = float.MaxValue;

            foreach (var other in Combatant.All)
            {
                if (other == _self || !other.IsAlive || other.ChestCenter == null) continue;
                Vector3 to = other.ChestCenter.position - head.position;
                float distance = to.magnitude;
                if (distance > skill.viewDistance * 1.2f) continue;

                bool recentlyShotBy = other == _lastAttacker && Time.time - _lastAttackedAt < 2f;
                bool aware = skill.CanSee(Vector3.Angle(lookForward, to), distance)
                             || distance < closeAwareness
                             || recentlyShotBy
                             || (other == _target && _targetVisible);
                if (!aware || !HasLineOfSight(other)) continue;

                float score = distance * (other == _target ? 0.7f : 1f) * (recentlyShotBy ? 0.6f : 1f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = other;
                }
            }

            if (best == null)
            {
                _targetVisible = false;
                return;
            }

            if (best != _target)
            {
                _target = best;
                _acquiredAt = Time.time;
                _trackingTime = 0f;
                _aimHead = Random.value < skill.headshotChance;
            }
            _targetVisible = true;
            _lastSeenPos = best.transform.position;
            _lastSeenTime = Time.time;
            _investigate = null;
        }

        private bool HasLineOfSight(Combatant other)
        {
            return CanHit(other, other.HeadCenter) || CanHit(other, other.ChestCenter);
        }

        private bool CanHit(Combatant other, Transform point)
        {
            if (point == null) return false;
            Vector3 to = point.position - head.position;
            if (!_weapons.TraceShot(new Ray(head.position, to), to.magnitude + 0.5f, out RaycastHit hit)) return false;
            return hit.collider.transform.IsChildOf(other.transform);
        }

        private void OnShotHeard(Combatant shooter, Vector3 position)
        {
            if (shooter == _self || !_self.IsAlive || _targetVisible) return;
            if ((position - transform.position).sqrMagnitude > hearingRange * hearingRange) return;
            _investigate = position;
            _investigateUntil = Time.time + 5f;
        }

        private void OnDamaged(Health health, DamageResult result, DamageSource source)
        {
            var attacker = source.Instigator;
            if (attacker == null || attacker == _self) return;
            _lastAttacker = attacker;
            _lastAttackedAt = Time.time;
            _nextPerception = Time.time; // look for them right away
            if (!_targetVisible)
            {
                _investigate = attacker.transform.position;
                _investigateUntil = Time.time + 4f;
            }
        }

        // ---------------------------------------------------------------- combat

        private void AimAt(out float yaw, out float pitch)
        {
            Transform point = _aimHead ? _target.HeadCenter : _target.ChestCenter;
            DirectionToAngles(point.position - head.position, out yaw, out pitch);

            // Smooth wandering error (Perlin noise) that shrinks the longer we track the target.
            float error = skill.AimErrorAfter(_trackingTime);
            float t = Time.time * 1.7f;
            yaw += (Mathf.PerlinNoise(t, _noiseSeed) * 2f - 1f) * error;
            pitch += (Mathf.PerlinNoise(_noiseSeed, t) * 2f - 1f) * error;
        }

        private void TryShoot(float aimYaw, float aimPitch)
        {
            var weapon = _weapons.Loadout.Active;
            if (weapon == null || !_weapons.Loadout.CanFire || Time.time < _nextTriggerAt) return;
            if (Time.time - _acquiredAt < skill.reactionTime) return;

            float distance = Vector3.Distance(head.position, _target.ChestCenter.position);
            if (distance > weapon.Stats.range) return;
            bool sniper = weapon.Stats.id == "sniper";
            if (sniper && _trackingTime < skill.aimSettleTime * 0.5f) return; // take a moment to line up
            float tolerance = sniper ? skill.fireTolerance * 0.6f : skill.fireTolerance;
            if (Mathf.Abs(Mathf.DeltaAngle(_yaw, aimYaw)) > tolerance || Mathf.Abs(Mathf.DeltaAngle(_pitch, aimPitch)) > tolerance) return;

            _weapons.QueueFire();
            _nextTriggerAt = Time.time + weapon.Stats.fireInterval + Random.Range(0.02f, 0.12f);
            if (Random.value < 0.25f) _aimHead = Random.value < skill.headshotChance; // occasionally re-pick head vs body
        }

        private Vector3 CombatMove(ref bool jump)
        {
            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            Vector2 band = IsHolding("sniper") ? sniperRange : rifleRange;

            Vector3 approach = Vector3.zero;
            if (distance > band.y) approach = NavigateTo(_target.transform.position, out _);
            else if (distance < band.x && distance > 0.01f) approach = -toTarget / distance;

            if (Time.time >= _strafeSwitchAt)
            {
                if (Random.value < 0.7f) _strafeDir = -_strafeDir;
                _strafeSwitchAt = Time.time + Random.Range(0.35f, 1.1f);
                if (_motor.IsGrounded && Random.value < 0.12f) jump = true;
            }
            Vector3 right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            Vector3 move = approach + right * (_strafeDir * 0.9f);

            // Don't strafe off ledges or into walls: flip if the nav mesh ends that way.
            if (IsBlocked(move))
            {
                _strafeDir = -_strafeDir;
                move = approach + right * (_strafeDir * 0.9f);
                if (IsBlocked(move)) move = approach;
            }
            return Vector3.ClampMagnitude(move, 1f);
        }

        private bool IsBlocked(Vector3 move)
        {
            if (move.sqrMagnitude < 0.01f) return false;
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit from, 1.5f, NavMesh.AllAreas)) return false;
            return NavMesh.Raycast(from.position, from.position + move.normalized * 1.5f, out _, NavMesh.AllAreas);
        }

        private void ChooseWeapon()
        {
            var loadout = _weapons.Loadout;
            if (loadout.IsSwitching || loadout.Slots.Count < 2) return;

            int sniperSlot = SlotOf("sniper");
            if (sniperSlot < 0) return;
            bool sniperUsable = !loadout.Slots[sniperSlot].IsEmpty;
            bool wantSniper = sniperUsable;
            if (_targetVisible && _target != null)
                wantSniper = sniperUsable && Vector3.Distance(transform.position, _target.transform.position) > sniperRange.x;

            int want = wantSniper ? sniperSlot : SlotOf("rifle");
            if (want >= 0 && want != loadout.ActiveIndex) loadout.SwitchTo(want);
        }

        private void MaybeReload()
        {
            var weapon = _weapons.Loadout.Active;
            if (weapon == null || weapon.IsReloading) return;
            if (weapon.Magazine < weapon.Stats.magazineSize * 0.5f) _weapons.Reload();
        }

        private bool IsHolding(string id) => _weapons.Loadout.Active != null && _weapons.Loadout.Active.Stats.id == id;

        private int SlotOf(string id)
        {
            var slots = _weapons.Loadout.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Stats.id == id) return i;
            return -1;
        }

        // ---------------------------------------------------------------- navigation

        private Vector3 RoamGoal()
        {
            if (_hasRoamGoal && Time.time < _roamGiveUpAt) return _roamGoal;
            // Throttle re-picks so a map without a usable nav mesh can't make bots search every frame.
            if (Time.time < _nextRoamPickAt) return transform.position;

            _nextRoamPickAt = Time.time + 0.5f;
            _hasRoamGoal = true;
            _roamGiveUpAt = Time.time + 20f;
            _roamGoal = PickRoamGoal();
            return _roamGoal;
        }

        private Vector3 PickRoamGoal()
        {
            // Weapons first: the sniper pad (if we don't have one) or a dropped weapon nearby.
            if (SlotOf("sniper") < 0)
            {
                var pad = MatchManager.Current != null ? MatchManager.Current.SniperPad : null;
                if (pad != null && pad.HasWeaponOnPad && Random.value < 0.5f) return pad.transform.position;
            }
            WeaponPickup nearest = null;
            float nearestDist = 30f * 30f;
            foreach (var pickup in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
            {
                if (pickup.Weapon == null || _weapons.Loadout.Preview(pickup.Weapon) == PickupOutcome.None) continue;
                float d = (pickup.transform.position - transform.position).sqrMagnitude;
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = pickup;
                }
            }
            if (nearest != null) return nearest.transform.position;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                var p = new Vector3(
                    Random.Range(roamArea.min.x, roamArea.max.x),
                    roamArea.center.y,
                    Random.Range(roamArea.min.z, roamArea.max.z));
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, roamArea.extents.y + 2f, NavMesh.AllAreas)) continue;
                if (!roamArea.Contains(hit.position)) continue;
                if ((hit.position - transform.position).sqrMagnitude < 64f) continue; // somewhere new
                if (!ComputePath(hit.position) || _path.status != NavMeshPathStatus.PathComplete) continue;
                return hit.position;
            }
            return transform.position;
        }

        /// <summary>World-space direction to walk this frame to follow a nav path toward <paramref name="goal"/>.</summary>
        private Vector3 NavigateTo(Vector3 goal, out bool arrived)
        {
            arrived = false;
            if (!_hasPath || (goal - _destination).sqrMagnitude > 4f || Time.time >= _repathAt)
            {
                _destination = goal;
                _repathAt = Time.time + 1f;
                _hasPath = ComputePath(goal);
                if (_hasPath)
                {
                    _corners.Clear();
                    _corners.AddRange(_path.corners);
                    _cornerIndex = 1;
                }
            }
            if (!_hasPath)
            {
                arrived = true; // unreachable: give up on this goal
                return Vector3.zero;
            }

            Vector3 pos = transform.position;
            while (_cornerIndex < _corners.Count && Flat(_corners[_cornerIndex] - pos).sqrMagnitude < 0.36f)
                _cornerIndex++;
            if (_cornerIndex >= _corners.Count)
            {
                arrived = true;
                _hasPath = false;
                return Vector3.zero;
            }
            return Flat(_corners[_cornerIndex] - pos).normalized;
        }

        private bool ComputePath(Vector3 goal)
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start, 2f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(goal, out NavMeshHit end, 3f, NavMesh.AllAreas)) return false;
            return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path)
                   && _path.status != NavMeshPathStatus.PathInvalid
                   && _path.corners.Length > 0;
        }

        private void HandleStuck(Vector3 move, ref bool jump, float dt)
        {
            Vector3 velocity = Flat(_motor.Velocity);
            bool wantsToMove = move.sqrMagnitude > 0.1f;
            _stuckTime = wantsToMove && velocity.sqrMagnitude < 0.25f ? _stuckTime + dt : 0f;
            if (_stuckTime > 0.6f && _motor.IsGrounded) jump = true;
            if (_stuckTime > 2f)
            {
                _stuckTime = 0f;
                _hasPath = false;
                _hasRoamGoal = false;
                _strafeDir = -_strafeDir;
            }
        }

        // ---------------------------------------------------------------- helpers

        private void FaceDirection(Vector3 direction, out float yaw, out float pitch)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                yaw = _yaw;
                pitch = 0f;
                return;
            }
            DirectionToAngles(direction, out yaw, out pitch);
            pitch *= 0.5f; // relaxed: look roughly level while moving
        }

        private static void DirectionToAngles(Vector3 direction, out float yaw, out float pitch)
        {
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float flat = new Vector2(direction.x, direction.z).magnitude;
            pitch = -Mathf.Atan2(direction.y, flat) * Mathf.Rad2Deg;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
