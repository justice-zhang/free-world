using Game.Core;

namespace Game.Simulation
{
    /// <summary>
    /// Resolves generic damage-immunity tags from active status instances. Stable
    /// tags are parsed once and comparisons remain allocation-free in resolution.
    /// </summary>
    internal static class StatusDamagePolicy
    {
        private static readonly ContentTag ImmuneAll = Tag("base.damage_policy.immune.all");
        private static readonly ContentTag ImmuneDirect = Tag("base.damage_policy.immune.direct");
        private static readonly ContentTag ImmuneContact = Tag("base.damage_policy.immune.contact");
        private static readonly ContentTag ImmunePeriodic = Tag("base.damage_policy.immune.periodic");
        private static readonly ContentTag ImmuneHazard = Tag("base.damage_policy.immune.hazard");
        private static readonly ContentTag ImmuneBossHazard = Tag("base.damage_policy.immune.boss_hazard");

        public static bool IsImmune(ActorCombatRecord actor, DamageChannelId channel)
        {
            var channelTag = ResolveChannelTag(channel);
            for (var statusIndex = 0; statusIndex < actor.Statuses.Count; statusIndex++)
            {
                var tags = actor.Statuses.GetAt(statusIndex).Definition.Tags;
                for (var tagIndex = 0; tagIndex < tags.Count; tagIndex++)
                {
                    if (tags[tagIndex] == ImmuneAll ||
                        (channelTag.IsValid && tags[tagIndex] == channelTag))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static ContentTag ResolveChannelTag(DamageChannelId channel)
        {
            if (channel == BuiltInDamageChannels.Direct) return ImmuneDirect;
            if (channel == BuiltInDamageChannels.Contact) return ImmuneContact;
            if (channel == BuiltInDamageChannels.Periodic) return ImmunePeriodic;
            if (channel == BuiltInDamageChannels.Hazard) return ImmuneHazard;
            if (channel == BuiltInDamageChannels.BossHazard) return ImmuneBossHazard;
            return default;
        }

        private static ContentTag Tag(string value)
        {
            var result = ContentTag.Create(value);
            if (!result.IsSuccess) throw new System.InvalidOperationException(result.Error.ToString());
            return result.Value;
        }
    }
}
