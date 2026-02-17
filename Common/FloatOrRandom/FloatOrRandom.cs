using System;
using UnityEngine;

namespace Common.FloatOrRandom
{
    [Serializable]
    public struct FloatOrRandom
    {
        [SerializeField] private bool isRandom;
        [SerializeField] private float minValue;
        [SerializeField] private float maxValue;
        
        public float GetValue(Squirrel3 rnd)
        {
            return isRandom ? rnd.Range(minValue, maxValue) : minValue;
        }

        public float Value => GetValue(Squirrel3.Instance);
        
        // implicit conversion from FloatOrRandom to float
        public static implicit operator float(FloatOrRandom value) => value.Value;
        
        // implicit conversion from float to FloatOrRandom
        public static implicit operator FloatOrRandom(float value)
        {
            return new FloatOrRandom
            {
                isRandom = false,
                minValue = value,
                maxValue = value
            };
        }
    }
}