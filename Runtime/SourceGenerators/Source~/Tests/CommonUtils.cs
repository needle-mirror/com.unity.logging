using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using SourceGenerator.Logging;

namespace Tests
{
    public static class CommonUtils
    {
        public static readonly string Header;

        static CommonUtils()
        {
            Header = $@"
using System;
using System.Text;
using FAKEUSING = UnityEngine;

public enum Allocator
{{
    Invalid = 0,
    None = 1,
    Temp = 2,
    TempJob = 3,
    Persistent = 4,
    AudioKernel = 5,
    FirstUserIndex = 64, // 0x00000040
}}

namespace Unity.Logging
{{
    public interface ILoggableMirrorStruct
    {{
        bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth);
    }}
    public interface ILoggableMirrorStruct<T> : ILoggableMirrorStruct {{}}

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NotLogged : Attribute {{}}

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class LogWithName : Attribute
    {{
        public string ReplacedName;

        public LogWithName(string newName)
        {{
            ReplacedName = newName;
        }}
    }}

    public struct LoggerHandle
    {{
        public long Value;

        public bool IsValid => Value != 0;
    }}

    public class Logger : IDisposable
    {{
        public readonly LoggerHandle Handler;
    }}
}}

struct NativeText
{{
    public NativeText(int capacity, Allocator allocator)
    {{
    }}
}}

struct UnsafeText
{{
    public UnsafeText(int capacity, Allocator allocator)
    {{
    }}
}}
";

            static string GenerateImplicitCastsFixedString(int indx)
            {
                var thisFs = FixedStringUtils.FSTypes[indx];

                var result = "";
                for (var i = 0; i < indx; i++)
                {
                    var fs = FixedStringUtils.FSTypes[i];
                    result += $"public static implicit operator {thisFs.Name}({fs.Name} b) => new {thisFs.Name}(b);/n";
                }
                return result;
            }

            for (var i = 0; i < FixedStringUtils.FSTypes.Length; i++)
            {
                var fs = FixedStringUtils.FSTypes[i];

                Header += $@"struct {fs.Name} {{
            internal const ushort utf8MaxLengthInBytes = {fs.MaxLength};
            public static int UTF8MaxLengthInBytes => utf8MaxLengthInBytes;

            public static implicit operator {fs.Name}(string b) => new {fs.Name}(b);

" + GenerateImplicitCastsFixedString(i)
                    + $@"

            public {fs.Name}(String source)
            {{
                bytes = default;
                utf8LengthInBytes = 0;
                unsafe
                {{
                    fixed (char* sourceptr = source)
                    {{
                        var error = UTF8ArrayUnsafeUtility.Copy(GetUnsafePtr(), out utf8LengthInBytes, utf8MaxLengthInBytes, sourceptr, source.Length);
                        CheckCopyError(error, source);
                        this.Length = utf8LengthInBytes;
                    }}
                }}
            }}
    }}
    ";
            }
        }

        private static Compilation CreateCompilation(string source) =>
            CSharpCompilation.Create("SourceGeneratorTestsCompilation",
                new[] {CSharpSyntaxTree.ParseText(source)},
                new[] {MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location)},
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        public static LoggingSourceGenerator GenerateCodeWithPrefix(string userHeader, string testData)
        {
            return GenerateCodeInternal(userHeader + Header + testData, out _);
        }

        public static LoggingSourceGenerator GenerateCode(string testData)
        {
            return GenerateCodeInternal(Header + testData, out _);
        }

        public static LoggingSourceGenerator GenerateCodeExpectErrors(string testData, out ImmutableArray<Diagnostic> diagnostics)
        {
            return GenerateCodeInternal(Header + testData, out diagnostics, expectErrors: true);
        }

        static LoggingSourceGenerator GenerateCodeInternal(string testData, out ImmutableArray<Diagnostic> diagnostics, bool expectErrors = false)
        {
            var inputCompilation = CreateCompilation(testData);

            var generator = new LoggingSourceGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out diagnostics);

            if (expectErrors == false)
            {
                foreach (var err in diagnostics)
                {
                    if (err.Location != null)
                    {
                        try
                        {
                            var txt = err.Location.SourceTree.ToString();

                            txt = txt.Insert(err.Location.SourceSpan.End, "<<<");
                            txt = txt.Insert(err.Location.SourceSpan.Start, ">>>");

                            var endIndx = txt.IndexOf('\n', err.Location.SourceSpan.End);
                            var startIndx = txt.LastIndexOf('\n', endIndx - 1);
                            var errorLine = txt.Substring(startIndx, endIndx - startIndx).Trim();

                            Console.Error.WriteLine(errorLine);
                        }
                        catch
                        {
                        }
                    }

                    Console.Error.WriteLine(err.ToString());
                    Console.Error.WriteLine();
                }

                var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                Assert.AreEqual(0, errors.Length, "Errors were detected");
                Assert.AreEqual(0, diagnostics.Length, "Warnings were detected");
            }

            Assert.IsNotNull(generator);
            Assert.IsNotNull(generator.methodsGenCode);

            var methodSource = generator.methodsGenCode;
            var typesGenSource = generator.typesGenCode == null ? "" : generator.typesGenCode.ToString();
            var userTypesGenSource = generator.userTypesGenCode ?? "";
            var parserGenSource = generator.parserGenCode == null ? "" : generator.parserGenCode.ToString();

            Assert.IsNotNull(methodSource);

            // all structs must have unique full name, otherwise it is a compile error
            if (generator.structureData.StructTypes != null)
            {
                var names = generator.structureData.StructTypes.Select(s => s.FullGeneratedTypeName).ToList();
                var hashSet = new HashSet<string>(names);
                Assert.AreEqual(names.Count, hashSet.Count, "Struct names are not unique!");
            }

            Assert.IsFalse(methodSource.Contains("in string"));
            Assert.IsFalse(methodSource.Contains("in global::System.String"));
            Assert.IsFalse(methodSource.Contains("object msg"));

            var expectedUnsafe = generator.invokeData.InvokeInstances.Any(kv => kv.Value.Any(v => v.ShouldBeMarkedUnsafe));
            if (generator.structureData.StructTypes != null)
                expectedUnsafe = expectedUnsafe || generator.structureData.StructTypes.Any(s => s.ShouldBeMarkedUnsafe);

            if (expectedUnsafe == false)
            {
                Assert.IsFalse(methodSource.Contains("unsafe"));
                Assert.IsFalse(parserGenSource.Contains("unsafe"));
                Assert.IsFalse(typesGenSource.Contains("unsafe"));
                Assert.IsFalse(userTypesGenSource.Contains("unsafe"));
            }

            {
                // validate that structs are generated only once
                var lines = typesGenSource.Split('\n');
                var structNamespace = lines.Select(l => l.Trim()).Where(l => l.StartsWith("namespace ") || l.StartsWith("public struct ")).ToArray();
                var n = structNamespace.Length;
                var currentNamespace = "";
                var uniqHash = new HashSet<string>();
                for (int i = 0; i < n; i++)
                {
                    var item = $"{currentNamespace} {structNamespace[i]}";
                    if (structNamespace[i].StartsWith("namespace "))
                    {
                        currentNamespace = structNamespace[i];
                    }
                    else if (uniqHash.Add(item) == false)
                    {
                        Assert.Fail($"{item} was generated at least 2 times");
                    }
                }
            }

            StripDefaultLogs(generator);

            return generator;
        }

        private static void StripDefaultLogs(LoggingSourceGenerator generator)
        {
            foreach (var level in Enum.GetValues<LogCallKind>())
            {
                Assert.IsTrue(generator.invokeData.InvokeInstances.ContainsKey(level), "generator.invokeData.InvokeInstances.ContainsKey(level)");

                var invokes = generator.invokeData.InvokeInstances[level];
                for (var i = invokes.Count - 1; i >= 0; i--)
                {
                    var invoke = invokes[i];
                    if (invoke.ArgumentData.Count == 0 && invoke.MessageData.FixedStringType.Name == FixedStringUtils.Smallest.Name && invoke.MessageData.LiteralValue == LogCallMessageData.DefaultFixedString32Literal)
                    {
                        invokes.RemoveAt(i);
                    }
                }

                if (invokes.Count == 0)
                    generator.invokeData.InvokeInstances.Remove(level);
            }
        }

        public static int StringOccurrencesCount(string haystack, string needle, StringComparison strComp)
        {
            var result = 0;
            var index = haystack.IndexOf(needle, strComp);
            while (index != -1)
            {
                ++result;
                index = haystack.IndexOf(needle, index + needle.Length, strComp);
            }
            return result;
        }
    }
}
