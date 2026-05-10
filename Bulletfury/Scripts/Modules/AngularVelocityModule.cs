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
    [ModuleDescription("Add rotational movement over time.")]
    [ModulePerformanceImpact(ModulePerformanceImpactRating.Low)]
    public class AngularVelocityModule : BulletModule, IParallelBulletModule
    {
        [SerializeField] private AnimationCurve angularVelocity = AnimationCurve.Constant(0, 1, 1);
        [SerializeField] private float scale;

        private NativeCurve _curve;

        public override void Execute(ref BulletContainer bullet, float deltaTime)
        {
            var vel = angularVelocity.Evaluate(Mode == CurveUsage.Lifetime
                          ? bullet.CurrentLifePercent
                          : bullet.CurrentLifeSeconds % Time / Time) *
                      scale;

            bullet.Rotation *= Quaternion.Euler(0, 0, vel * deltaTime);

            bullet.Forward = bullet.Rotation * Vector3.forward;
            bullet.Right = bullet.Rotation * Vector3.right;
            bullet.Up = bullet.Rotation * Vector3.up;
        }

        public JobHandle Schedule(NativeArray<BulletContainer> bullets, int count, float deltaTime, JobHandle dependency)
        {
            if (count <= 0) return dependency;

            if (!_curve.IsCreated) _curve = NativeCurve.Create();
            _curve.Bake(angularVelocity);

            var job = new Job
            {
                Bullets = bullets,
                Curve = _curve.Samples,
                Scale = scale,
                DeltaTime = deltaTime,
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
            public float Scale;
            public float DeltaTime;
            public byte ModeLifetime;
            public float LoopTime;

            public void Execute(int index)
            {
                var b = Bullets[index];
                if (b.Dead == 1 || b.EndOfLife == 1) return;

                float t = ModeLifetime == 1
                    ? b.CurrentLifePercent
                    : (b.CurrentLifeSeconds % LoopTime) / LoopTime;

                float vel = NativeCurve.Sample(Curve, t) * Scale;
                quaternion delta = quaternion.AxisAngle(new float3(0f, 0f, 1f), math.radians(vel * DeltaTime));

                quaternion rot = math.mul((quaternion)b.Rotation, delta);
                b.Rotation = rot;
                b.Forward = math.mul(rot, new float3(0f, 0f, 1f));
                b.Right = math.mul(rot, new float3(1f, 0f, 0f));
                b.Up = math.mul(rot, new float3(0f, 1f, 0f));

                Bullets[index] = b;
            }
        }
    }
}
