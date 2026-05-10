using System;
using BulletFury.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace BulletFury.Modules
{
    [Serializable]
    [ModuleDescription("Scale bullet size throughout lifetime.")]
    [ModulePerformanceImpact(ModulePerformanceImpactRating.Low)]
    public class BulletSizeOverTimeModule : BulletModule, IParallelBulletModule
    {
        [Tooltip("The size curve to apply to the bullet over time")]
        public AnimationCurve sizeOverTime = AnimationCurve.Constant(0, 1, 1);

        private NativeCurve _curve;

        public override void Execute(ref BulletContainer bullet, float deltaTime)
        {
            bullet.CurrentSize = bullet.StartSize * sizeOverTime.Evaluate(Mode == CurveUsage.Lifetime
                ? bullet.CurrentLifePercent
                : bullet.CurrentLifeSeconds % Time / Time);
            bullet.CurrentSize = Mathf.Max(bullet.CurrentSize, 0.1f);
        }

        public JobHandle Schedule(NativeArray<BulletContainer> bullets, int count, float deltaTime, JobHandle dependency)
        {
            if (count <= 0) return dependency;

            if (!_curve.IsCreated) _curve = NativeCurve.Create();
            _curve.Bake(sizeOverTime);

            var job = new Job
            {
                Bullets = bullets,
                Curve = _curve.Samples,
                ModeLifetime = Mode == CurveUsage.Lifetime ? (byte)1 : (byte)0,
                LoopTime = Time <= 0f ? 1f : Time
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
            public byte ModeLifetime;
            public float LoopTime;

            public void Execute(int index)
            {
                var b = Bullets[index];
                if (b.Dead == 1 || b.EndOfLife == 1) return;

                float t = ModeLifetime == 1
                    ? b.CurrentLifePercent
                    : (b.CurrentLifeSeconds % LoopTime) / LoopTime;

                b.CurrentSize = math.max(b.StartSize * NativeCurve.Sample(Curve, t), 0.1f);
                Bullets[index] = b;
            }
        }
    }
}
