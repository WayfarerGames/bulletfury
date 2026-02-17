using System;
using BulletFury.Data;
using UnityEngine;

namespace BulletFury.Modules
{
    [Serializable]
    [ModuleDescription("Pause progression until a timed condition is met.")]
    [ModulePerformanceImpact(ModulePerformanceImpactRating.Low)]
    public class WaitToContinueModule : IBulletInitModule
    {
        [SerializeField, Tooltip("Seconds a bullet runs normally before entering waiting mode. Waiting bullets resume when ActivateWaitingBullets is called.")]
        private float timeToPlayBeforeWaiting;
        public void Execute(ref BulletContainer container)
        {
            container.Waiting = 1;
            container.TimeToWait = timeToPlayBeforeWaiting;
        }
    }
}