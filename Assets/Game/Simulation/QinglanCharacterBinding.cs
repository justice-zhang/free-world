using System;
using Game.Content.Runtime;
using Game.Core;

namespace Game.Simulation
{
    /// <summary>
    /// Low-frequency composition helper that binds stable character mechanic IDs to
    /// load-local indices before a run starts. It never participates in fixed Tick work.
    /// </summary>
    internal static class QinglanCharacterBinding
    {
        public static Result<RuntimeCharacterDefinition> Attach(
            ContentRegistry registry,
            ContentId characterId,
            EntityHandle owner,
            CharacterMechanicRuntime runtime)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!owner.IsValid || !characterId.IsValid)
                return Failure(characterId, "Character binding requires a valid owner and stable character ID.");
            if (!registry.TryGet(characterId, out RuntimeCharacterDefinition character))
                return Failure(characterId, "Character definition is missing from the loaded registry.");
            if (character.MechanicIds.Count > runtime.AvailableCapacity)
                return Failure(characterId, "Character mechanic runtime capacity is insufficient.");

            var entries = new ContentRegistryEntry[character.MechanicIds.Count];
            for (var index = 0; index < entries.Length; index++)
            {
                if (!registry.TryGet(character.MechanicIds[index], out entries[index]) ||
                    !(entries[index].Definition is RuntimeCharacterMechanicDefinition))
                {
                    return Failure(
                        characterId,
                        "Character mechanic reference '" + character.MechanicIds[index] +
                        "' is missing or has the wrong kind.");
                }
            }

            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                if (runtime.TryAttach(
                        owner,
                        entry.Index,
                        (RuntimeCharacterMechanicDefinition)entry.Definition))
                {
                    continue;
                }

                runtime.Detach(owner);
                return Failure(characterId, "Character mechanic attachment failed atomically.");
            }

            return Result<RuntimeCharacterDefinition>.Success(character);
        }

        private static Result<RuntimeCharacterDefinition> Failure(
            ContentId characterId,
            string message) =>
            Result<RuntimeCharacterDefinition>.Failure(
                new Error(ErrorCode.MissingReference, message, characterId));
    }
}
