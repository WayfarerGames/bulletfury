using System;
using BulletFury.Data;
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
    }
}