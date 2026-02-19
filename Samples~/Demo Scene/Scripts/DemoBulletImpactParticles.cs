using BulletFury;
using BulletFury.Data;
using UnityEngine;

namespace BulletFury.Samples
{
    [DisallowMultipleComponent]
    public sealed class DemoBulletImpactParticles : MonoBehaviour, IBulletHitHandler
    {
        [SerializeField] private ParticleSystem impactPrefab;
        [SerializeField, Min(1)] private int emitCount = 16;

        public void Hit(BulletContainer bullet)
        {
            impactPrefab.transform.position = bullet.Position;
            impactPrefab.Emit(emitCount);
        }
    }
}
