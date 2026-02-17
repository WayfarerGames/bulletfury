using System;
using UnityEngine;

namespace BulletFury
{
    [Serializable, Tooltip("Settings for controlling burst behavior of bullet spawning.")]
    public class BurstData
    {
       
        [Tooltip("The delay before the first bullet is generated.")]
        public float delay = 0f;
        
        [Tooltip("The maximum number of bullets that this spawner can have active at once. 0 for no limit."), Min(0)]
        public int maxActiveBullets = 0;

        [Tooltip("The number of bursts to spawn.")]
        public int burstCount = 1;

        [Tooltip("The delay between spawning each burst (if burstCount > 1).")]
        public float burstDelay = 0f;

        [Tooltip("The speed increase for each burst (if burstCount > 1).")]
        public float stackSpeedIncrease = 0f;

        [Tooltip("Should bursts update the position & rotation for every bullet?")]
        public bool burstsUpdatePositionEveryBullet;
    }
}