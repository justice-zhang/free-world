using System;

namespace Game.Simulation
{
    /// <summary>Deterministic integer-polynomial XP requirement curve.</summary>
    public readonly struct ExperienceCurve
    {
        public ExperienceCurve(float baseRequirement, float linearGrowth, float quadraticGrowth)
        {
            if (!FinitePositive(baseRequirement) || !FiniteNonNegative(linearGrowth) || !FiniteNonNegative(quadraticGrowth))
                throw new ArgumentOutOfRangeException(nameof(baseRequirement));
            BaseRequirement = baseRequirement;
            LinearGrowth = linearGrowth;
            QuadraticGrowth = quadraticGrowth;
        }

        public float BaseRequirement { get; }
        public float LinearGrowth { get; }
        public float QuadraticGrowth { get; }

        public float RequiredForLevel(int currentLevel)
        {
            if (currentLevel < 1) throw new ArgumentOutOfRangeException(nameof(currentLevel));
            var offset = currentLevel - 1d;
            var value = BaseRequirement +
                        (LinearGrowth * offset) +
                        (QuadraticGrowth * offset * Math.Max(0d, offset - 1d) * 0.5d);
            return value >= float.MaxValue ? float.MaxValue : (float)value;
        }

        public static ExperienceCurve Default => new ExperienceCurve(5f, 2f, 0.25f);
        private static bool FinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        private static bool FiniteNonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    /// <summary>Run-local XP, level, and queued level-up request state.</summary>
    public sealed class ExperienceProgression
    {
        public ExperienceProgression(ExperienceCurve? curve = null)
        {
            Curve = curve ?? ExperienceCurve.Default;
            Level = 1;
        }

        public ExperienceCurve Curve { get; }
        public int Level { get; private set; }
        public float CurrentExperience { get; private set; }
        public double TotalExperience { get; private set; }
        public int PendingLevelUps { get; private set; }
        public float RequiredExperience => Curve.RequiredForLevel(Level);

        public int Gain(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0f) return 0;
            TotalExperience += amount;
            CurrentExperience += amount;
            var gained = 0;
            while (CurrentExperience >= RequiredExperience)
            {
                CurrentExperience -= RequiredExperience;
                Level++;
                PendingLevelUps++;
                gained++;
            }
            return gained;
        }

        public bool ConsumeLevelUpRequest()
        {
            if (PendingLevelUps <= 0) return false;
            PendingLevelUps--;
            return true;
        }
    }
}
