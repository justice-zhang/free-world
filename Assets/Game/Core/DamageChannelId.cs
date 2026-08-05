using System;

namespace Game.Core
{
    /// <summary>Stable channel used for target-local damage cooldown and barrier policy.</summary>
    [Serializable]
    public readonly struct DamageChannelId : IEquatable<DamageChannelId>, IComparable<DamageChannelId>
    {
        private readonly ContentId identifier;

        private DamageChannelId(ContentId identifier)
        {
            this.identifier = identifier;
        }

        public string Value => identifier.Value;
        public bool IsValid => identifier.IsValid;
        public int StableHash => identifier.StableHash;

        public static Result<DamageChannelId> Create(
            string input,
            ContentId packId = default,
            string authorAssetPath = "")
        {
            var result = ContentId.Create(input, packId, authorAssetPath);
            return result.IsSuccess
                ? Result<DamageChannelId>.Success(new DamageChannelId(result.Value))
                : Result<DamageChannelId>.Failure(result.Error);
        }

        public static bool TryCreate(string input, out DamageChannelId channel)
        {
            var result = Create(input);
            channel = result.IsSuccess ? result.Value : default;
            return result.IsSuccess;
        }

        public bool Equals(DamageChannelId other) => identifier.Equals(other.identifier);
        public override bool Equals(object obj) => obj is DamageChannelId other && Equals(other);
        public int CompareTo(DamageChannelId other) => identifier.CompareTo(other.identifier);
        public override int GetHashCode() => identifier.GetHashCode();
        public override string ToString() => Value;
        public static bool operator ==(DamageChannelId left, DamageChannelId right) => left.Equals(right);
        public static bool operator !=(DamageChannelId left, DamageChannelId right) => !left.Equals(right);
    }

    /// <summary>Built-in damage channels whose stable values may not be reused.</summary>
    public static class BuiltInDamageChannels
    {
        public static readonly DamageChannelId Direct = Create("base.damage_channel.direct");
        public static readonly DamageChannelId Contact = Create("base.damage_channel.contact");
        public static readonly DamageChannelId Periodic = Create("base.damage_channel.periodic");
        public static readonly DamageChannelId Hazard = Create("base.damage_channel.hazard");
        public static readonly DamageChannelId BossHazard = Create("base.damage_channel.boss_hazard");

        private static DamageChannelId Create(string value)
        {
            var result = DamageChannelId.Create(value);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToString());
            return result.Value;
        }
    }
}
