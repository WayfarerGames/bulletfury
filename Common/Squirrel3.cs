using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Common
{
    public class Squirrel3
    {
        private static Squirrel3 _instance;
        public static Squirrel3 Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Squirrel3();
                return _instance;
            }
        }

        private const uint NOISE1 = 0xb5297a4d;
        private const uint NOISE2 = 0x68e31da4;
        private const uint NOISE3 = 0x1b56c4e9;
        private const uint CAP = uint.MaxValue;

        private int _n = 0;
        private int _seed;

        public Squirrel3()
        {
            _seed = Environment.TickCount;
        }
        
        public Squirrel3(int seed)
        {
            _seed = seed;
        }

        public void SetSeed(int seed)
        {
            _seed = seed;
        }

        public void ResetState()
        {
            _n = 0;
        }
        
        public float Next()
        {
            ++_n;
            return Rnd(_n, _seed) / (float)CAP;
        }

        public double NextDouble()
        {
            return Rnd(_n, _seed) / (double) CAP;
        }

        public Vector2 RandomPointInUnitCircle()
        {
            Vector2 result = Vector2.one;
            while(result.magnitude > 1f)
            {
                result.x = Range(-1f, 1f);
                result.y = Range(-1f, 1f);
            }
            return result;
        }
        
        public float Range(float min, float max)
        {
            return Mathf.Lerp(min, max, Next());
        }

        public double Range(double min, double max)
        {
            return Lerp(min, max, NextDouble());
        }
        
        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * math.clamp(t, 0d, 1d);
        }
        
        public int Range(int min, int max)
        {
            return Mathf.RoundToInt(Mathf.Lerp(min, max, Next()));
        }

        public T WeightedRandom<T>(Dictionary<T, float> items) where T : class
        {
            var sum = items.Sum(item => item.Value) * Next();
            foreach (var item in items)
            {
                sum -= item.Value;
                if (sum > 0)
                    continue;

                return item.Key;
            }

            return null;
        }

        public T WeightedRandom<T>(List<T> items) where T : class, IWeightedItem
        {
            var sum = items.Sum(item => item.Weight) * Next();
            foreach (var item in items)
            {
                sum -= item.Weight;
                if (sum > 0)
                    continue;

                return item;
            }

            return null;
        }

        public T GetRandomElement<T>(IEnumerable<T> list, out int idx)
        {
            var enumerable = list as T[] ?? list.ToArray();
            idx = Range(0, enumerable.Length);
            return enumerable[idx];
        }

        public bool Bool()
        {
            return NextDouble() > 0.5;
        }
        
        public bool Bool(float chance)
        {
            return Next() < chance;
        }
        
        public bool Bool(double chance)
        {
            return NextDouble() < chance;
        }

        private static long Rnd(long n, int seed = 0)
        {
            n *= NOISE1;
            n += seed;
            n ^= n >> 8;
            n += NOISE2;
            n ^= n << 8;
            n *= NOISE3;
            n ^= n >> 8;
            return n % CAP;
        }
    }

    public interface IWeightedItem
    {
        public float Weight { get; }
    }
}