using System;

namespace BulletFury
{
    public enum ModulePerformanceImpactRating
    {
        Low = 0,
        Medium = 1,
        High = 2,
        VeryHigh = 3
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ModulePerformanceImpactAttribute : Attribute
    {
        public ModulePerformanceImpactAttribute(ModulePerformanceImpactRating rating)
        {
            Rating = rating;
        }

        public ModulePerformanceImpactAttribute(ModulePerformanceImpactRating rating, string justification)
        {
            Rating = rating;
            Justification = justification;
        }

        public ModulePerformanceImpactRating Rating { get; }
        public string Justification { get; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ModuleDescriptionAttribute : Attribute
    {
        public ModuleDescriptionAttribute(string description)
        {
            Description = description;
        }

        public string Description { get; }
    }
}
