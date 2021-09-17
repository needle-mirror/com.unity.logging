using System;
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
using FAKEUSING = UnityEngine;

namespace Unity.Logging
{{
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
            CSharpCompilation.Create("compilation",
                new[] {CSharpSyntaxTree.ParseText(source)},
                new[] {MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location)},
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        public static LoggingGenerator GenerateCodeWithPrefix(string userHeader, string testData)
        {
            return GenerateCodeInternal(userHeader + Header + testData, out _);
        }

        public static LoggingGenerator GenerateCode(string testData)
        {
            return GenerateCodeInternal(Header + testData, out _);
        }

        public static LoggingGenerator GenerateCodeExpectErrors(string testData, out ImmutableArray<Diagnostic> diagnostics)
        {
            return GenerateCodeInternal(Header + testData, out diagnostics, expectErrors: true);
        }

        static LoggingGenerator GenerateCodeInternal(string testData, out ImmutableArray<Diagnostic> diagnostics, bool expectErrors = false)
        {
            var inputCompilation = CreateCompilation(testData);

            var generator = new LoggingGenerator {WriteFilesToDisk = false};
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out diagnostics);

            if (expectErrors == false)
            {
                var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

                foreach (var err in errors)
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

                Assert.AreEqual(0, errors.Length, "Errors were detected");
            }

            Assert.IsNotNull(generator);
            Assert.IsNotNull(generator.methodsGenCode);

            var methodSource = generator.methodsGenCode.ToString();

            Assert.IsNotNull(methodSource);

            // all structs must have unique full name, otherwise it is a compile error
            if (generator.structureData.StructTypes != null)
            {
                var names = generator.structureData.StructTypes.Select(s => s.FullGeneratedTypeName).ToList();
                var hashSet = new HashSet<string>(names);
                Assert.AreEqual(names.Count, hashSet.Count, "Struct names are not unique!");
            }

            Assert.IsFalse(methodSource.Contains("object msg"));

            return generator;
        }
    }
}
