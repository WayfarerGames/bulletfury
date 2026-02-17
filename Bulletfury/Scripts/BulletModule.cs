using BulletFury.Data;
using UnityEngine;

namespace BulletFury
{
    public abstract class BulletModule : IBulletModule
    {
        [Tooltip("Should the gradient loop after a set time, or be applied over the lifetime of the bullet?")]
        public CurveUsage Mode = CurveUsage.Lifetime;

        [Tooltip("The time in seconds it takes for the curve to loop")]
        public float Time = 1f;


        public abstract void Execute(ref BulletContainer bullet, float deltaTime);
    }
}