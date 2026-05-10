using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace BulletFury
{
    /// <summary>
    /// Blittable, Burst-friendly lookup table baked from an <see cref="AnimationCurve"/>.
    /// Bake on the main thread, then read from inside a job via <see cref="Sample"/>.
    /// </summary>
    public struct NativeCurve : IDisposable
    {
        public NativeArray<float> Samples;

        public bool IsCreated => Samples.IsCreated;

        public static NativeCurve Create(int resolution = 128, Allocator allocator = Allocator.Persistent)
        {
            return new NativeCurve
            {
                Samples = new NativeArray<float>(math.max(2, resolution), allocator, NativeArrayOptions.UninitializedMemory)
            };
        }

        /// <summary>
        /// Resample the curve uniformly over [0, 1] into the backing array.
        /// Cheap (default 128 evaluates) — safe to call every frame to pick up inspector edits.
        /// </summary>
        public void Bake(AnimationCurve curve)
        {
            if (!Samples.IsCreated) return;
            int n = Samples.Length;
            if (curve == null || curve.length == 0)
            {
                for (int i = 0; i < n; i++) Samples[i] = 0f;
                return;
            }

            float step = 1f / (n - 1);
            for (int i = 0; i < n; i++)
                Samples[i] = curve.Evaluate(i * step);
        }

        public void Dispose()
        {
            if (Samples.IsCreated) Samples.Dispose();
        }

        /// <summary>
        /// Linearly interpolate the baked samples. Clamps <paramref name="t"/> to [0, 1].
        /// Callable from a Burst job.
        /// </summary>
        public static float Sample(NativeArray<float> samples, float t)
        {
            int n = samples.Length;
            if (n == 0) return 0f;
            if (n == 1) return samples[0];

            t = math.clamp(t, 0f, 1f);
            float pos = t * (n - 1);
            int i0 = (int)pos;
            int i1 = math.min(i0 + 1, n - 1);
            float frac = pos - i0;
            return math.lerp(samples[i0], samples[i1], frac);
        }
    }
}
