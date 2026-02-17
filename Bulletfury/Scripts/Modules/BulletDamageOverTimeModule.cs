using System;
using BulletFury.Data;
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

        public void Execute(ref BulletContainer container, float deltaTime)
        {
            container.Damage = damageOverTime.Evaluate(container.CurrentLifePercent);
        }
    }
}