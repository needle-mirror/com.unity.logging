using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using SourceGenerator.Logging;

namespace Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class ParserTests
    {
        private class TestSyntaxWalker : CSharpSyntaxWalker
        {
            public LogCallFinder Receiver;

            public override void Visit(SyntaxNode node)
            {
                Receiver.OnVisitSyntaxNode(node);
                base.Visit(node);
            }
        }

        [Flags]
        public enum UsingType : short
        {
            NotRelevant = 0,

            Alias = 1 << 0,
            DirectUsing = 1 << 1,

            NeverCorrect = 1 << 14
        }

        private static (UsingType, string)[] _usingVariants =
        {
            (
                UsingType.NotRelevant,
@"using Unity.Mathematics;
                  using Something.Else;"
            ),
            (
                UsingType.Alias,
@"using   AAA  =    Unity.Logging;
                  using Unity.Mathematics;
                  using DD  = Unity.Logging;  "
            ),
            (
                UsingType.DirectUsing,
@"using    Unity.Logging;
                  using NetCode;"
            ),
            (
                UsingType.DirectUsing | UsingType.Alias,
@"using UnityEngine;
                  using Unity.Logging;
                  using DD =    Unity.Logging;
                  using AAA =    Unity.Logging;
                  using Something;"
            )
        };

        // this variants are always correct
        private static readonly string[] AlwaysCorrect =
        {
            @"Unity.Logging.Log.Info(""""); // correct",
            @"Unity.Logging.Log.Info(""correct"");",
            @"Unity.Logging.Log.Info(""correct {0}"", A);",
            @"Unity.Logging.Log.Info(""correct {0}, {1}"", 42, ""a"");",
        };

        // this variants are always correct all types
        private static readonly string[] AlwaysCorrectAllTypes =
        {
            @"Unity.Logging.Log.Verbose(""""); // correct",
            @"Unity.Logging.Log.Debug(""correct"");",
            @"Unity.Logging.Log.Info(""correct {0}"", A);",
            @"Unity.Logging.Log.Warning(""correct {0}, {1}"", 42, ""a"");",
            @"Unity.Logging.Log.Error(""correct1 {0}, {1}"", 42, ""a"");",
            @"Unity.Logging.Log.Fatal(""correct2 {0}, {1}"", 42, ""a"");",

            @"Unity.Logging.Log.Decorate(""correct2"", 42);"
        };

        // this variants are correct only when alias is specified
        private static readonly string[] AliasCorrect =
        {
            @"DD.Log.Info(""""); // alias correct",
            @"DD.Log.Info(""alias correct"");",
            @"DD.Log.Info(""alias correct using alias {0}"", A);",
            @"AAA.Log.Info(""alias AAA {0}"", A);",
            @"DD.Log.Info(""alias correct {0}, {1}"", 42, ""a"");",
            @"AAA.Log.Info(""""); // alias correct",
        };

        // this variants are correct only when alias is specified
        private static readonly string[] UsingCorrect =
        {
            @"Log.Info(""""); //using only correct",
            @"Log.Info(""using correct"");",
            @"Log.Info(""using correct"", 1, 2, 3, 4, new object[] {""1""});",
            @"Log.Info(""using correct {0}, {1}"", 42, ""a"");",
        };

        // this variants are never correct
        private static readonly string[] Incorrect =
        {
            @"FAKEUSING.Log.Info(""wrong"");",
            @"A.Logging.Log.Info(""wrong"");",
            @"Logger.Write(""wrong"");",
            @"ity.Logging.Log.Info(""wrong"");",
            @"Log.Info<T>(""wrong"", 1, 3);",
            @"Log.Info<int>(""wrong"");",
            @"Log.InfoSomethingElse(""wrong"");",
            @"SomeOtherCall(""wrong a {0}"", 42);"
        };

        private static IEnumerable<(UsingType, string)> GenerateCall()
        {
            foreach (var s in AlwaysCorrect)
            {
                yield return (UsingType.NotRelevant, s);
            }
            foreach (var s in AliasCorrect)
            {
                yield return (UsingType.Alias, s);
            }
            foreach (var s in UsingCorrect)
            {
                yield return (UsingType.DirectUsing, s);
            }
            foreach (var s in Incorrect)
            {
                yield return (UsingType.NeverCorrect, s);
            }
        }

        private static readonly int[] RandomSeeds =
        {
            0x_0000_F000, 0x_0F00_0F01, 0x_00F0_00F4, 0x_0F0F_0F05,
            0x_7FFF_FFFF, 0x_0FF0_0FCF, 0x_07F6_96FF, 0x_000F_530F,
            0x_1BF3_8EFA, 0x_3F74_3FB0, 0x_4003_8EFE, 0x_0228_F322,
        };

        private bool CalculateExpectedCalls(UsingType testEntryType, UsingType currentUsingMode)
        {
            switch (testEntryType)
            {
                case UsingType.NotRelevant:
                    return true;
                case UsingType.NeverCorrect:
                    return false;
                case UsingType.Alias:
                    return currentUsingMode.HasFlag(UsingType.Alias);
                case UsingType.DirectUsing:
                    return currentUsingMode.HasFlag(UsingType.DirectUsing);
                default:
                    throw new Exception("Weird test data");
            }
        }

        [Test]
        public void TestParse([ValueSourceAttribute(nameof(_usingVariants))] (UsingType, string) usingHeader,
            [ValueSourceAttribute(nameof(RandomSeeds))] int randomSeed)
        {
            var rnd = new Random(randomSeed);

            var data = GenerateCall().OrderBy(a => rnd.Next()).ToList();

            var usingCount = rnd.Next(0, data.Count);

            var usingParams = usingHeader.Item1;
            var expectedCalls = 0;

            var callsA = "";
            var callsB = "";

            for (var i = 0; i < usingCount; i++)
            {
                if (CalculateExpectedCalls(data[i].Item1, usingParams))
                    ++expectedCalls;

                if (rnd.Next() % 2 == 0)
                    callsA += data[i].Item2 + '\n';
                else
                    callsB += data[i].Item2 + '\n';
            }

            var receiver = RunTest(usingHeader.Item2, callsA, callsB, expectedCalls);

            foreach (var logLevel in receiver.LogCallsLevel)
            {
                Assert.AreEqual(LogCallKind.Info, logLevel);
            }
        }

        [Test]
        public void TestParseLevels([ValueSourceAttribute(nameof(_usingVariants))] (UsingType, string) usingHeader,
            [ValueSourceAttribute(nameof(RandomSeeds))] int randomSeed)
        {
            var rnd = new Random(randomSeed);

            var data = GenerateCall().OrderBy(a => rnd.Next()).ToList();

            var usingCount = rnd.Next(0, data.Count);

            var usingParams = usingHeader.Item1;
            var expectedCalls = 0;

            var callsA = "";
            var callsB = "";

            var allTypes = Enum.GetValues(typeof(LogCallKind)) as LogCallKind[];

            var dictExpected = new Dictionary<LogCallKind, int>();
            foreach (LogCallKind i in Enum.GetValues(typeof(LogCallKind)))
                dictExpected[i] = 0;

            for (var i = 0; i < usingCount; i++)
            {
                if (CalculateExpectedCalls(data[i].Item1, usingParams))
                {
                    ++expectedCalls;

                    var NewType = allTypes[rnd.Next(0, allTypes.Length)];
                    var NewTypeString = NewType.ToString();

                    var s = data[i].Item2.Replace("Info", NewTypeString);
                    data[i] = (data[i].Item1, s);

                    dictExpected[NewType]++;
                }

                if (rnd.Next() % 2 == 0)
                    callsA += data[i].Item2 + '\n';
                else
                    callsB += data[i].Item2 + '\n';
            }

            var receiver = RunTest(usingHeader.Item2, callsA, callsB, expectedCalls);

            var dictActual = new Dictionary<LogCallKind, int>();
            foreach (LogCallKind i in Enum.GetValues(typeof(LogCallKind)))
                dictActual[i] = 0;

            foreach (var logLevel in receiver.LogCallsLevel)
            {
                dictActual[logLevel]++;
            }

            Assert.AreEqual(dictExpected, dictActual);
            foreach (LogCallKind i in Enum.GetValues(typeof(LogCallKind)))
                Assert.AreEqual(dictExpected[i], dictActual[i]);
        }

        [Test]
        public void EmptyTest([ValueSourceAttribute(nameof(_usingVariants))] (UsingType, string) usingHeader)
        {
            var receiver = RunTest(usingHeader.Item2, "", "", 0);

            foreach (var logLevel in receiver.LogCallsLevel)
                Assert.AreEqual(LogCallKind.Info, logLevel);
        }

        [Test]
        public void AllTest([ValueSourceAttribute(nameof(_usingVariants))] (UsingType, string) usingHeader)
        {
            var receiver = RunTest(usingHeader.Item2, string.Join('\n', AlwaysCorrect), "", AlwaysCorrect.Length);

            foreach (var logLevel in receiver.LogCallsLevel)
                Assert.AreEqual(LogCallKind.Info, logLevel);
        }

        [Test]
        public void AllTestLevels([ValueSourceAttribute(nameof(_usingVariants))] (UsingType, string) usingHeader)
        {
            var receiver = RunTest(usingHeader.Item2, string.Join('\n', AlwaysCorrectAllTypes), "", AlwaysCorrectAllTypes.Length);

            Assert.AreEqual(Enum.GetValues(typeof(LogCallKind)).Length, receiver.LogCallsLevel.Count);

            var dictActual = new Dictionary<LogCallKind, int>();
            foreach (LogCallKind i in Enum.GetValues(typeof(LogCallKind)))
                dictActual[i] = 0;

            foreach (var logLevel in receiver.LogCallsLevel)
                dictActual[logLevel]++;

            foreach (var v in dictActual)
                Assert.AreEqual(1, v.Value);
        }

        private LogCallFinder RunTest(string usingHeader, string callsA, string callsB, int expectedCalls)
        {
            var testData = $@"
            {usingHeader}
            namespace N1
            {{
                public struct T1{{}}
                namespace N2
                {{
                    public struct T2
                    {{
                        static void A()
                        {{
                            {callsA}
                        }}
                    }}
                }}
            }}
            namespace N1.N2.N3
            {{
                public struct T3
                {{
                    void A()
                    {{
                        {callsB}
                    }}
                }}
            }}";

            var receiver = ParseCode(testData);
            Assert.AreEqual(expectedCalls, receiver.LogCalls.Count, testData);
            Assert.AreEqual(expectedCalls, receiver.LogCallsLevel.Count, testData);

            return receiver;
        }

        [Test]
        public void RunTestNamespace_NoDetect()
        {
            var testData = $@"
            namespace N1
            {{
                public struct T1{{}}
                namespace Unity.Logging
                {{
                    public struct T2
                    {{
                        static void A()
                        {{
                            Log.Info(""Hi"");
                        }}
                    }}
                }}
            }}";

            var receiver = ParseCode(testData);
            Assert.AreEqual(0, receiver.LogCalls.Count, testData);
            Assert.AreEqual(0, receiver.LogCallsLevel.Count, testData);
        }

        [Test]
        public void RunTestNamespace_DetectFull()
        {
            var testData = $@"
            namespace Unity.Logging
            {{
                public struct T1{{}}
                namespace N1
                {{
                    public struct T2
                    {{
                        static void A()
                        {{
                            var log2 = new LoggerConfig().MinimumLevel.Verbose()
                                                    .OutputTemplate(TemplateTestPattern1.PatternString)
                                                    .WriteTo.StringLogger()
                                                    .WriteTo.JsonFile(fileName2)
                                                    .CreateLogger();

                            Log.Info(""Hi"");
                        }}
                    }}
                }}
            }}";

            var receiver = ParseCode(testData);
            Assert.AreEqual(1, receiver.LogCalls.Count, testData);
            Assert.AreEqual(1, receiver.LogCallsLevel.Count, testData);
        }

        [Test]
        public void RunTestNamespace_DetectFullMore()
        {
            var testData = $@"
            namespace Unity.Logging.Something
            {{
                public struct T1{{}}
                namespace N1
                {{
                    public struct T2
                    {{
                        static void A()
                        {{
                            Unity.Logging.Log.Info(""Hi from Full"");
                            Log.Info(""Hi"");
                        }}
                    }}
                }}
            }}";

            var receiver = ParseCode(testData);
            Assert.AreEqual(2, receiver.LogCalls.Count, testData);
            Assert.AreEqual(2, receiver.LogCallsLevel.Count, testData);
        }

        [Test]
        public void RunTestNamespace_DetectFullPartial()
        {
            var testData = $@"
            namespace Unity
            {{
                public struct T1{{}}
                namespace Logging.A
                {{
                    public struct T2
                    {{
                        static void A()
                        {{
                            Log.Info(""Hi"");
                        }}
                    }}
                }}
            }}";

            var receiver = ParseCode(testData);
            Assert.AreEqual(1, receiver.LogCalls.Count, testData);
            Assert.AreEqual(1, receiver.LogCallsLevel.Count, testData);
        }

        [Test]
        public void RunTestNamespace_DetectFullPartialMore()
        {
            var testData = $@"
            namespace Unity
            {{
                namespace Logging
                {{
                    public struct T1{{}}
                    namespace ASD
                    {{
                        public struct T2
                        {{
                            static void A()
                            {{
                                Log.Info(""Hi"");
                            }}
                        }}
                    }}
                }}
            }}";

            var receiver = ParseCode(testData);
            Assert.AreEqual(1, receiver.LogCalls.Count, testData);
            Assert.AreEqual(1, receiver.LogCallsLevel.Count, testData);
        }

        public static LogCallFinder ParseCode(string testData)
        {
            var receiver = new LogCallFinder();
            var walker = new TestSyntaxWalker {Receiver = receiver};

            var tree = CSharpSyntaxTree.ParseText(testData);
            tree.GetCompilationUnitRoot().Accept(walker);

            return receiver;
        }
    }
}
