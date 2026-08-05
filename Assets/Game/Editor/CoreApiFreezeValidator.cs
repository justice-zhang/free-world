using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            new FrozenAssembly { Name = "Game.Core", Hash = "25766747b7014e0386506567e5e3c35f78b6dc5d00d850b00c35d28eb8d7e176" },
            new FrozenAssembly { Name = "Game.Content.Runtime", Hash = "ca593752954be1622e60e21f7d68627779de30abcfa1f28f9e219b9eaeba502d" },
            new FrozenAssembly { Name = "Game.Simulation", Hash = "a6555342a937f674d827f83eea0b0100fe2feeafff92f0e53b58e9fd7b39181f" },
            new FrozenAssembly { Name = "Game.Application", Hash = "bea7fe9998f2ae9f872a505e9f36cee00a9ddfd26e5af8e105916ea4b3d46197" },
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
            var signatures = CaptureSignatures(assemblyName);
            var builder = new StringBuilder(signatures.Length * 80);
            for (var index = 0; index < signatures.Length; index++)
                builder.Append(signatures[index]).Append('\n');
            return new ApiSurfaceDigest(
                assemblyName,
                Hash(builder.ToString()),
                signatures.Length);
        }

        internal static string[] CaptureSignatures(string assemblyName)
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
            return signatures.ToArray();
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
                {
                    Debug.Log("[M10 API Freeze] Assembly=" + values[index].AssemblyName +
                              " Hash=" + values[index].Hash +
                              " Signatures=" + values[index].SignatureCount + ".");
                }
                ExportSignatures(values);
                Debug.Log("[M10 API Freeze] PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        private static void ExportSignatures(ApiSurfaceDigest[] values)
        {
            var outputDirectory = Environment.GetEnvironmentVariable("M10_API_SIGNATURE_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputDirectory)) return;
            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);
            for (var index = 0; index < values.Length; index++)
            {
                var path = Path.Combine(
                    outputDirectory,
                    values[index].AssemblyName + ".signatures.txt");
                File.WriteAllLines(
                    path,
                    CoreApiFreezeValidator.CaptureSignatures(values[index].AssemblyName),
                    new UTF8Encoding(false));
            }
        }
    }
}
