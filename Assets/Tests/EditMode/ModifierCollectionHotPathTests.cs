using System;
using System.Reflection;
using System.Reflection.Emit;
using Game.Core;
using Game.Simulation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ModifierCollectionHotPathTests
    {
        private static readonly OpCode[] OneByteOpCodes = new OpCode[256];
        private static readonly OpCode[] TwoByteOpCodes = new OpCode[256];

        static ModifierCollectionHotPathTests()
        {
            var fields = typeof(OpCodes).GetFields(
                BindingFlags.Public | BindingFlags.Static);
            for (var index = 0; index < fields.Length; index++)
            {
                var opCode = (OpCode)fields[index].GetValue(null);
                var value = unchecked((ushort)opCode.Value);
                if (value < 256)
                {
                    OneByteOpCodes[value] = opCode;
                }
                else if ((value & 0xff00) == 0xfe00)
                {
                    TwoByteOpCodes[value & 0xff] = opCode;
                }
            }
        }

        [Test]
        public void EvaluateUsesResolvedIntegerGroupKeysWithoutAllocating()
        {
            var collection = new ModifierCollection(initialCapacity: 4);
            var group = Id("test.stack.hot_path");
            Add(collection, "test.source.low", 2f, 1, group);
            Add(collection, "test.source.high", 5f, 2, group, 0.5f);
            Add(collection, "test.source.ungrouped", 1f, 0, default);

            var entryType = typeof(ModifierCollection).GetNestedType(
                "Entry",
                BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null);
            var groupKeyField = entryType.GetField(
                "StackingGroupKey",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(groupKeyField, Is.Not.Null);
            Assert.That(groupKeyField.FieldType, Is.EqualTo(typeof(int)));

            var entriesField = typeof(ModifierCollection).GetField(
                "entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = (Array)entriesField.GetValue(collection);
            var lowGroupKey = (int)groupKeyField.GetValue(entries.GetValue(0));
            var highGroupKey = (int)groupKeyField.GetValue(entries.GetValue(1));
            var ungroupedKey = (int)groupKeyField.GetValue(entries.GetValue(2));
            Assert.That(lowGroupKey, Is.GreaterThan(0));
            Assert.That(highGroupKey, Is.EqualTo(lowGroupKey));
            Assert.That(ungroupedKey, Is.Zero);

            AssertHotPathMethod("Evaluate", BindingFlags.Instance | BindingFlags.Public);
            AssertHotPathMethod("SumOperation", BindingFlags.Instance | BindingFlags.NonPublic);
            AssertHotPathMethod("ApplyOrdered", BindingFlags.Instance | BindingFlags.NonPublic);
            AssertHotPathMethod("TryFindNext", BindingFlags.Instance | BindingFlags.NonPublic);
            AssertHotPathMethod(
                "IsSuppressedByStackingGroup",
                BindingFlags.Instance | BindingFlags.NonPublic);

            for (var index = 0; index < 16; index++)
            {
                collection.Evaluate(BuiltInStatIndices.Damage, 10f);
            }

            var checksum = 0f;
            for (var index = 0; index < 256; index++)
            {
                checksum += collection.Evaluate(BuiltInStatIndices.Damage, 10f);
            }

            Assert.That(checksum, Is.EqualTo(4096f));

            Assert.That(
                collection.Evaluate(BuiltInStatIndices.Damage, 10f),
                Is.EqualTo(16f));
            collection.Tick(0.5f);
            Assert.That(
                collection.Evaluate(BuiltInStatIndices.Damage, 10f),
                Is.EqualTo(13f));
        }

        [Test]
        public void ClearAndResetRetainStorageAndRejectStaleHandles()
        {
            var collection = new ModifierCollection(initialCapacity: 2);
            var firstHandle = Add(
                collection,
                "test.source.first",
                2f,
                1,
                Id("test.stack.reset"));
            Add(
                collection,
                "test.source.second",
                5f,
                2,
                Id("test.stack.reset"));

            var entriesField = typeof(ModifierCollection).GetField(
                "entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var entriesBefore = entriesField.GetValue(collection);
            var revisionBefore = collection.Revision;

            collection.Clear();

            Assert.That(collection.Count, Is.Zero);
            Assert.That(collection.Revision, Is.GreaterThan(revisionBefore));
            Assert.That(entriesField.GetValue(collection), Is.SameAs(entriesBefore));
            Assert.That(
                collection.Evaluate(BuiltInStatIndices.Damage, 10f),
                Is.EqualTo(10f));

            Add(collection, "test.source.reused", 3f, 0, default);
            Assert.That(collection.Remove(firstHandle), Is.False);
            collection.Reset();
            Assert.That(collection.Count, Is.Zero);
            Assert.That(entriesField.GetValue(collection), Is.SameAs(entriesBefore));
        }

        [Test]
        public void ActorStatBlockResetReusesArraysAndClearsCachedModifiers()
        {
            var statBlockType = typeof(ModifierCollection).Assembly.GetType(
                "Game.Simulation.ActorStatBlock",
                throwOnError: true);
            var constructor = statBlockType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)[0];
            var initialValues = StatBaseValues.CreateDefault();
            initialValues.Damage = 10f;
            var statBlock = constructor.Invoke(new object[] { initialValues, null });
            var instanceFlags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var modifiers = (ModifierCollection)statBlockType
                .GetProperty("Modifiers", instanceFlags)
                .GetValue(statBlock);
            var staleHandle = Add(
                modifiers,
                "test.source.actor_reset",
                5f,
                0,
                default);
            var getMethod = statBlockType.GetMethod("Get", instanceFlags);
            Assert.That(
                (float)getMethod.Invoke(
                    statBlock,
                    new object[] { BuiltInStatIndices.Damage }),
                Is.EqualTo(15f));

            var baseValuesBefore = GetArray(statBlockType, statBlock, "baseValues");
            var cachedValuesBefore = GetArray(statBlockType, statBlock, "cachedValues");
            var modifierRevisionsBefore = GetArray(
                statBlockType,
                statBlock,
                "cachedModifierRevisions");
            var baseRevisionsBefore = GetArray(
                statBlockType,
                statBlock,
                "baseRevisions");
            var cachedBaseRevisionsBefore = GetArray(
                statBlockType,
                statBlock,
                "cachedBaseRevisions");

            var replacementValues = StatBaseValues.CreateDefault(250f, 8f);
            replacementValues.Damage = 3f;
            statBlockType.GetMethod("Reset", instanceFlags).Invoke(
                statBlock,
                new object[] { replacementValues });

            Assert.That(modifiers.Count, Is.Zero);
            Assert.That(modifiers.Remove(staleHandle), Is.False);
            Assert.That(GetArray(statBlockType, statBlock, "baseValues"),
                Is.SameAs(baseValuesBefore));
            Assert.That(GetArray(statBlockType, statBlock, "cachedValues"),
                Is.SameAs(cachedValuesBefore));
            Assert.That(GetArray(statBlockType, statBlock, "cachedModifierRevisions"),
                Is.SameAs(modifierRevisionsBefore));
            Assert.That(GetArray(statBlockType, statBlock, "baseRevisions"),
                Is.SameAs(baseRevisionsBefore));
            Assert.That(GetArray(statBlockType, statBlock, "cachedBaseRevisions"),
                Is.SameAs(cachedBaseRevisionsBefore));
            Assert.That(
                (float)getMethod.Invoke(
                    statBlock,
                    new object[] { BuiltInStatIndices.Damage }),
                Is.EqualTo(3f));
            Assert.That(
                (float)getMethod.Invoke(
                    statBlock,
                    new object[] { BuiltInStatIndices.Health }),
                Is.EqualTo(250f));
        }

        private static void AssertHotPathMethod(string name, BindingFlags flags)
        {
            var method = typeof(ModifierCollection).GetMethod(name, flags);
            Assert.That(method, Is.Not.Null, name);
            var body = method.GetMethodBody();
            Assert.That(body, Is.Not.Null, name);
            var il = body.GetILAsByteArray();
            var offset = 0;
            while (offset < il.Length)
            {
                var opCode = ReadOpCode(il, ref offset);
                Assert.That(opCode, Is.Not.EqualTo(OpCodes.Newarr), name);
                Assert.That(opCode, Is.Not.EqualTo(OpCodes.Box), name);

                var operandSize = GetOperandSize(opCode.OperandType, il, offset);
                if (opCode.OperandType == OperandType.InlineMethod)
                {
                    var token = BitConverter.ToInt32(il, offset);
                    var member = method.Module.ResolveMember(token);
                    if (opCode == OpCodes.Newobj)
                    {
                        Assert.That(
                            typeof(Exception).IsAssignableFrom(member.DeclaringType),
                            Is.True,
                            name);
                    }

                    Assert.That(member.DeclaringType, Is.Not.EqualTo(typeof(ContentId)), name);
                    Assert.That(member.DeclaringType, Is.Not.EqualTo(typeof(string)), name);
                }

                offset += operandSize;
            }
        }

        private static OpCode ReadOpCode(byte[] il, ref int offset)
        {
            var first = il[offset++];
            if (first != 0xfe)
            {
                return OneByteOpCodes[first];
            }

            return TwoByteOpCodes[il[offset++]];
        }

        private static int GetOperandSize(
            OperandType operandType,
            byte[] il,
            int operandOffset)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return 4 + (BitConverter.ToInt32(il, operandOffset) * 4);
                default:
                    throw new InvalidOperationException(
                        "Unsupported IL operand type: " + operandType + ".");
            }
        }

        private static Array GetArray(Type type, object instance, string fieldName)
        {
            return (Array)type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(instance);
        }

        private static ModifierHandle Add(
            ModifierCollection collection,
            string sourceId,
            float value,
            int priority,
            ContentId group,
            float duration = float.PositiveInfinity)
        {
            var modifier = new Modifier(
                Id(sourceId),
                BuiltInStatIds.Damage,
                ModifierOperation.AddFlat,
                value,
                priority,
                group,
                duration);
            Assert.That(collection.TryAdd(modifier, out var handle), Is.True);
            return handle;
        }

        private static ContentId Id(string value)
        {
            var result = ContentId.Create(value);
            Assert.That(result.IsSuccess, Is.True, result.Error.ToString());
            return result.Value;
        }
    }
}
