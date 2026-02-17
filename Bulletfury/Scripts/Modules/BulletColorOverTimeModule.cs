using System;
using BulletFury.Data;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace BulletFury.Modules
{
    [Serializable]
    [ModuleDescription("Animate bullet color throughout lifetime.")]
    [ModulePerformanceImpact(ModulePerformanceImpactRating.Low)]
    public class BulletColorOverTimeModule : BulletModule
    {
        [GradientUsage(true)] [Tooltip("The gradient to apply to the bullet over time")]
        public Gradient colorOverTime;

        public override void Execute(ref BulletContainer bullet, float deltaTime)
        {
            float t;
            if (Mode == CurveUsage.Lifetime)
            {
                t = Mathf.Clamp01(bullet.CurrentLifePercent);
            }
            else
            {
                t = Time <= Mathf.Epsilon
                    ? 0f
                    : Mathf.Repeat(bullet.CurrentLifeSeconds, Time) / Time;
            }

            bullet.Color = bullet.StartColor * colorOverTime.Evaluate(t);
        }
    }
}
