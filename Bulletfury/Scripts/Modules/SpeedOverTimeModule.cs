using System;
using BulletFury.Data;
using UnityEngine;

namespace BulletFury.Modules
{
    [Serializable]
    [ModuleDescription("Change bullet speed over lifetime.")]
    [ModulePerformanceImpact(ModulePerformanceImpactRating.Low)]
    public class SpeedOverTimeModule : BulletModule, IParallelBulletModule
    {
        [SerializeField] private AnimationCurve speedOverTime = AnimationCurve.Constant(0, 1, 1);
        [SerializeField] private float scale = 1;
        
        public override void Execute(ref BulletContainer bullet, float deltaTime)
        {
            bullet.CurrentSpeed = bullet.Speed * speedOverTime.Evaluate(Mode == CurveUsage.Lifetime
                ? bullet.CurrentLifePercent
                : bullet.CurrentLifeSeconds % Time / Time)
                * scale;
        }
    }
}