using System;
using Common;
using UnityEngine;

namespace Wayfarer_Games.Common.FloatOrRandom
{
    [Serializable]
    public struct IntOrRandom
    {
        [SerializeField] private bool isRandom;
        [SerializeField] private int minValue;
        [SerializeField] private int maxValue;
        
        public int GetValue(Squirrel3 rnd)
        {
            return isRandom ? rnd.Range(minValue, maxValue) : minValue;
        }

        public int Value => GetValue(Squirrel3.Instance);
        
        // implicit conversion from FloatOrRandom to float
        public static implicit operator int(IntOrRandom value) => value.Value;
        
        // implicit conversion from float to FloatOrRandom
        public static implicit operator IntOrRandom(int value)
        {
            return new IntOrRandom
            {
                isRandom = false,
                minValue = value,
                maxValue = value
            };
        }
    }
}