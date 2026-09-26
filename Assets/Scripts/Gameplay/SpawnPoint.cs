using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>A place a combatant can spawn: position is the feet, forward is the facing.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        public static SpawnPoint Create(string name, Vector3 feetPosition, Vector3 lookAt, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Vector3 facing = lookAt - feetPosition;
            facing.y = 0f;
            go.transform.SetPositionAndRotation(feetPosition, facing.sqrMagnitude > 0f ? Quaternion.LookRotation(facing) : Quaternion.identity);
            return go.AddComponent<SpawnPoint>();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward * 1.2f);
        }
    }
}
