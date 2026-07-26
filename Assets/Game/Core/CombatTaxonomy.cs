using System;

namespace Game.Core
{
    /// <summary>Damage categories shared by runtime content and the simulation.</summary>
    public enum DamageType : byte
    {
        /// <summary>Physical damage mitigated by armor.</summary>
        Physical = 1,

        /// <summary>Fire damage mitigated by fire resistance.</summary>
        Fire = 2,

        /// <summary>Cold damage mitigated by cold resistance.</summary>
        Cold = 3,

        /// <summary>Lightning damage mitigated by lightning resistance.</summary>
        Lightning = 4,

        /// <summary>Poison damage mitigated by poison resistance.</summary>
        Poison = 5,

        /// <summary>True damage bypasses armor and resistance.</summary>
        True = 6
    }

    /// <summary>Allocation-free mechanic tags carried by high-frequency damage packets.</summary>
    [Flags]
    public enum DamageTags : ulong
    {
        /// <summary>No mechanic tags.</summary>
        None = 0UL,

        /// <summary>Direct impact damage.</summary>
        Direct = 1UL << 0,

        /// <summary>Periodic damage over time.</summary>
        DamageOverTime = 1UL << 1,

        /// <summary>Damage originated from a status instance.</summary>
        Status = 1UL << 2,

        /// <summary>Damage originated from a secondary proc.</summary>
        Secondary = 1UL << 3
    }
}
