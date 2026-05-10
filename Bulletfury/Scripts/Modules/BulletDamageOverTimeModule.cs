using System;
using BulletFury.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace BulletFury.Modules
{
    [Serializable]
    [ModuleDescription("Scale bullet damage over its lifetime.")]
    [ModulePerformanceImpact(ModulePerformanceImpactRating.Low)]
    public class BulletDamageOverTimeModule : IBulletModule, IParallelBulletModule
    {
        [Tooltip("The damage curve to apply to the bullet over time")]
        public AnimationCurve damageOverTime = AnimationCurve.Constant(0, 1, 1);

        private NativeCurve _curve;

        public void Execute(ref BulletContainer container, float deltaTime)
        {
            container.Damage = damageOverTime.Evaluate(container.CurrentLifePercent);
        }

        public JobHandle Schedule(NativeArray<BulletContainer> bullets, int count, float deltaTime, JobHandle dependency)
        {
            if (count <= 0) return dependency;

            if (!_curve.IsCreated) _curve = NativeCurve.Create();
            _curve.Bake(damageOverTime);

            var job = new Job
            {
                Bullets = bullets,
                Curve = _curve.Samples
            };
            return job.Schedule(count, 256, dependency);
        }

        public void DisposeJobResources()
        {
            if (_curve.IsCreated) _curve.Dispose();
        }

#if !UNITY_EDITOR
        [BurstCompile]
#endif
        private struct Job : IJobParallelFor
        {
            public NativeArray<BulletContainer> Bullets;
            [ReadOnly] public NativeArray<float> Curve;

            public void Execute(int index)
            {
                var b = Bullets[index];
                if (b.Dead == 1 || b.EndOfLife == 1) return;
                b.Damage = NativeCurve.Sample(Curve, b.CurrentLifePercent);
                Bullets[index] = b;
            }
        }
    }
}
