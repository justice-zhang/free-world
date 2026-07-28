using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal readonly struct ApiSurfaceDigest
    {
        public ApiSurfaceDigest(string assemblyName, string hash, int signatureCount)
        {
            AssemblyName = assemblyName;
            Hash = hash;
            SignatureCount = signatureCount;
        }

        public string AssemblyName { get; }
        public string Hash { get; }
        public int SignatureCount { get; }
    }

    /// <summary>
    /// Locks the normalized public surface of the framework core assemblies at M10.
    /// Updating a digest requires an accepted ADR and migration plan.
    /// </summary>
    internal static class CoreApiFreezeValidator
    {
        private sealed class FrozenAssembly
        {
            public string Name;
            public string Hash;
        }

        private static readonly FrozenAssembly[] Frozen =
        {
            new FrozenAssembly { Name = "Game.Core", Hash = "cbc7dcb08b2460e73f94e4bdc0f521cd38bb4c12e86156ce732fa8d792e5385f" },
            new FrozenAssembly { Name = "Game.Content.Runtime", Hash = "f38753a12ebbbb32a436c7f59c83a49eee0ba85b481e31acf9d964109b04c235" },
            new FrozenAssembly { Name = "Game.Simulation", Hash = "ed82f11b72a93c079843eb7d41b27c11926e0f63f17380253c5ff80621ffd19a" },
            new FrozenAssembly { Name = "Game.Application", Hash = "56f87d47e257170228686e27583e79ae0bcb9eb5ea72dbd7e8f4a1796d08e2aa" },
            new FrozenAssembly { Name = "Game.Platform.Abstractions", Hash = "8eb5f2ccca0f5845a55d90c9f00fb42eae59cc82d81e98369995e84428a51738" }
        };

        public static ApiSurfaceDigest[] Capture()
        {
            var output = new ApiSurfaceDigest[Frozen.Length];
            for (var index = 0; index < Frozen.Length; index++)
                output[index] = Capture(Frozen[index].Name);
            return output;
        }

        public static void AppendCurrentProject(ValidationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            var actual = Capture();
            for (var index = 0; index < Frozen.Length; index++)
            {
                if (string.Equals(Frozen[index].Hash, "PENDING", StringComparison.Ordinal)) continue;
                if (string.Equals(Frozen[index].Hash, actual[index].Hash, StringComparison.Ordinal)) continue;
                report.Add(
                    "M10-API-FREEZE",
                    Frozen[index].Name + " public API changed from " + Frozen[index].Hash +
                    " to " + actual[index].Hash +
                    ". An accepted ADR and migration plan are required before updating the freeze.");
            }
        }

        private static ApiSurfaceDigest Capture(string assemblyName)
        {
            var assembly = FindAssembly(assemblyName);
            if (assembly == null)
                throw new InvalidOperationException("Core API assembly is not loaded: " + assemblyName);
            var signatures = new List<string>(256);
            var types = assembly.GetExportedTypes();
            Array.Sort(types, CompareTypes);
            for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                AppendType(types[typeIndex], signatures);
            signatures.Sort(StringComparer.Ordinal);
            var builder = new StringBuilder(signatures.Count * 80);
            for (var index = 0; index < signatures.Count; index++)
                builder.Append(signatures[index]).Append('\n');
            return new ApiSurfaceDigest(
                assemblyName,
                Hash(builder.ToString()),
                signatures.Count);
        }

        private static void AppendType(Type type, List<string> output)
        {
            var kind = type.IsEnum ? "enum" :
                type.IsInterface ? "interface" :
                type.IsValueType ? "struct" :
                type.IsDelegate() ? "delegate" : "class";
            var interfaces = type.GetInterfaces();
            Array.Sort(interfaces, CompareTypes);
            var interfaceText = new StringBuilder();
            for (var index = 0; index < interfaces.Length; index++)
            {
                if (index > 0) interfaceText.Append(',');
                interfaceText.Append(TypeName(interfaces[index]));
            }
            output.Add("T|" + kind + "|" + TypeName(type) + "|base=" +
                       TypeName(type.BaseType) + "|interfaces=" + interfaceText);

            var constructors = type.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            for (var index = 0; index < constructors.Length; index++)
                output.Add("C|" + TypeName(type) + "|" + Parameters(constructors[index].GetParameters()));

            var fields = type.GetFields(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly);
            for (var index = 0; index < fields.Length; index++)
            {
                var field = fields[index];
                var constant = field.IsLiteral
                    ? "|const=" + Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture)
                    : string.Empty;
                output.Add("F|" + TypeName(type) + "|" + field.Name + "|" +
                           TypeName(field.FieldType) + "|static=" + field.IsStatic +
                           "|readonly=" + field.IsInitOnly + constant);
            }

            var properties = type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly);
            for (var index = 0; index < properties.Length; index++)
            {
                var property = properties[index];
                output.Add("P|" + TypeName(type) + "|" + property.Name + "|" +
                           TypeName(property.PropertyType) + "|index=" +
                           Parameters(property.GetIndexParameters()) + "|get=" +
                           IsPublic(property.GetMethod) + "|set=" + IsPublic(property.SetMethod));
            }

            var events = type.GetEvents(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly);
            for (var index = 0; index < events.Length; index++)
                output.Add("E|" + TypeName(type) + "|" + events[index].Name + "|" +
                           TypeName(events[index].EventHandlerType));

            var methods = type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (method.IsSpecialName) continue;
                output.Add("M|" + TypeName(type) + "|" + method.Name + "|" +
                           TypeName(method.ReturnType) + "|generic=" +
                           (method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0) +
                           "|" + Parameters(method.GetParameters()) + "|static=" + method.IsStatic);
            }
        }

        private static string Parameters(ParameterInfo[] parameters)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < parameters.Length; index++)
            {
                if (index > 0) builder.Append(',');
                var parameter = parameters[index];
                builder.Append(parameter.IsOut ? "out:" : parameter.ParameterType.IsByRef ? "ref:" : "in:");
                builder.Append(TypeName(parameter.ParameterType));
                builder.Append(parameter.IsOptional ? ":optional" : string.Empty);
            }
            return builder.ToString();
        }

        private static bool IsPublic(MethodInfo method) => method != null && method.IsPublic;

        private static Assembly FindAssembly(string name)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
                if (string.Equals(assemblies[index].GetName().Name, name, StringComparison.Ordinal))
                    return assemblies[index];
            return null;
        }

        private static int CompareTypes(Type left, Type right) =>
            string.Compare(TypeName(left), TypeName(right), StringComparison.Ordinal);

        private static string TypeName(Type type)
        {
            if (type == null) return string.Empty;
            if (type.IsByRef) return TypeName(type.GetElementType()) + "&";
            if (type.IsPointer) return TypeName(type.GetElementType()) + "*";
            if (type.IsArray) return TypeName(type.GetElementType()) + "[" +
                                     new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsGenericParameter) return "`" + type.GenericParameterPosition;
            if (!type.IsGenericType) return type.FullName ?? type.Name;
            var definition = type.GetGenericTypeDefinition();
            var baseName = definition.FullName ?? definition.Name;
            var tick = baseName.IndexOf('`');
            if (tick >= 0) baseName = baseName.Substring(0, tick);
            var arguments = type.GetGenericArguments();
            var builder = new StringBuilder(baseName).Append('<');
            for (var index = 0; index < arguments.Length; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append(TypeName(arguments[index]));
            }
            return builder.Append('>').ToString();
        }

        private static bool IsDelegate(this Type type) =>
            type != null && typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                for (var index = 0; index < bytes.Length; index++)
                    builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }

    /// <summary>Prints the normalized M10 core public API digests for audit.</summary>
    public static class M10ApiFreezeCommand
    {
        /// <summary>Captures all frozen assembly digests and exits nonzero on failure.</summary>
        public static void Run()
        {
            var exitCode = 0;
            try
            {
                var values = CoreApiFreezeValidator.Capture();
                for (var index = 0; index < values.Length; index++)
                    Debug.Log("[M10 API Freeze] Assembly=" + values[index].AssemblyName +
                              " Hash=" + values[index].Hash +
                              " Signatures=" + values[index].SignatureCount + ".");
                Debug.Log("[M10 API Freeze] PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }
    }
}
