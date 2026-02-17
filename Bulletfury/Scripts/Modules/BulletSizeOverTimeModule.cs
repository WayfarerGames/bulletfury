using System;
using BulletFury.Data;
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

        public override void Execute(ref BulletContainer bullet, float deltaTime)
        {
            bullet.CurrentSize = bullet.StartSize * sizeOverTime.Evaluate(Mode == CurveUsage.Lifetime
                ? bullet.CurrentLifePercent
                : bullet.CurrentLifeSeconds % Time / Time);
            bullet.CurrentSize = Mathf.Max(bullet.CurrentSize, 0.1f);
        }
    }
}