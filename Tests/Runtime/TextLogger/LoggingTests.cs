using System;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;

namespace Unity.Logging.Tests
{
    // Context data structs used in Logging test
    //
    // NOTE: These structs are very simple as these tests are intended to validate the end-to-end functionality and not necessarily
    // parsing of context struct. The TextParserTest will provide test cases for more complex structs and test data.
    public struct ContextTestStruct1
    {
        public int Field1;

        public override string ToString()
        {
            return $"[{Field1}]";
        }
    }

    public struct ContextTestStruct2
    {
        public float Field1;
        public int Field2;

        public override string ToString()
        {
            return $"[{Field1}, {Field2}]";
        }
    }

    public struct ContextTestStruct3
    {
        public float Field1;
        public int Field2;
        public bool Field3;

        public override string ToString()
        {
            return $"[{Field1}, {Field2}, {Field3}]";
        }
    }

    public struct ContextOverloadStruct
    {
        public double Field1;
        public ulong Field2;

        public override string ToString()
        {
            return $"[{Field1}, {Field2}]";
        }
    }


    [TestFixture]
    public class TextLoggerLoggerTest : LoggingTestFixture
    {
        private Unity.Mathematics.Random Rand;

        // Since TextLogger depends on source generation, all the test data must be explicit Log.Info calls using
        // arguments with explicit types, i.e. type is known at compile time. Therefore we'll use delegates to execute
        // the individual log calls.

        private static LogInvocation[] TestData;

        // Allocate separate arrays for each context struct type
        private static ContextTestStruct1[] C1;
        private static ContextTestStruct2[] C2;
        private static ContextTestStruct3[] C3;

        // NOTE: At this time, boxing each struct type won't work with TextLogger APIs (without an explicit cast).
        // SourceGenerator needs to be expanded to determine the underlying boxed type and provide an updated
        // conversion operator to handle it. For now the tests will just use separate arrays.

        internal struct LogInvocation
        {
            public delegate void InvokeLogging();
            public InvokeLogging Invoke;
            public string ExpectedOutput;
        }

        internal struct LogMessageJob : IJob
        {
            public int testDataIndex;

            public void Execute()
            {
                ExecuteLogCall(TestData[testDataIndex]);
            }
        }

        public override void Setup()
        {
            base.Setup();

            StringSink.AssertNoSinks();

            Log.Logger = new LoggerConfig()
                .WriteTo.Console()
                .WriteTo.StringLogger().CreateLogger();
        }

        public override void TearDown()
        {
            Log.Logger = null;
            base.TearDown();
        }

        [TestCase(true, TestName = "LogBasicMessages_WithLiterals")]
        [TestCase(false, TestName = "LogBasicMessages_WithVariables")]
        public void LogBasicMessages(bool useLiterals)
        {
            if (useLiterals)
            {
                InitBasicTestDataUsingLiterals();
            }
            else
            {
                InitBasicTestDataUsingVariables();
            }

            foreach (var test in TestData)
            {
                //ResetLogState();
                ExecuteAndValidateLogCall(test);
            }

            for (var i = 0; i < TestData.Length; i++)
            {
                ExecuteAndValidateLogCallFromJob(TestData[i], i);
            }
        }

        public struct LogComplexA
        {
            public struct LogComplexB
            {
                public long A1;
            }
            public struct LogComplexC
            {
                public FixedString32Bytes C1;
            }
            public struct LogComplexD
            {
            }

            public struct LogComplexFS512
            {
                public FixedString512Bytes String512;
            }
            public struct LogComplexF
            {
                public LogComplexB bb;
                public LogComplexC cc;
                public LogComplexD dd;
            }

            public long A1;
            public LogComplexB A2;
            public LogComplexC A3;
            public LogComplexD A4;
            public LogComplexF A5;
        }

        [Test]
        public void LogComplex()
        {
            var ca = new LogComplexA();
            ca.A1 = 1;
            ca.A2 = new LogComplexA.LogComplexB {A1 = 2};
            ca.A3 = new LogComplexA.LogComplexC {C1 = "LogComplexA.LogComplexC"};
            ca.A5 = new LogComplexA.LogComplexF
            {bb = new LogComplexA.LogComplexB {A1 = 11}, cc = new LogComplexA.LogComplexC {C1 = "C1"}};

            var ac = new LogComplexA.LogComplexC {C1 = "LogComplexA.LogComplexC"};
            var ac512 = new LogComplexA.LogComplexFS512 {String512 = "String512"};
            var ac512empty = new LogComplexA.LogComplexFS512 {String512 = ""};

            var data1 = (Id : 42, Position : (5, 6, 7));
            TestData = new[]
            {
                new LogInvocation
                {
                    Invoke = () => { Log.Info("T: {SomeName}", ac512empty); },
                    ExpectedOutput = String.Format("T: []"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("T: {0}", ac512); },
                    ExpectedOutput = String.Format("T: [String512]"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("T: {0}", ac); },
                    ExpectedOutput = String.Format("T: [LogComplexA.LogComplexC]"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("T: {Named}", ca.A3); },
                    ExpectedOutput = String.Format("T: [LogComplexA.LogComplexC]"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("T: {0}", ca); },
                    ExpectedOutput = String.Format("T: [1, [2], [LogComplexA.LogComplexC], [], [[11], [C1], []]]"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("T: {a412}", (101, (201, 202, 203))); },
                    ExpectedOutput = String.Format("T: {0}", (101, (201, 202, 203))).Replace("(", "[").Replace(")", "]"),
                },
#pragma warning disable CS8123
                new LogInvocation
                {
                    Invoke = () => { Log.Info("T: {0}", (Id: 10, Position: (1, 2, 3))); },
                    ExpectedOutput = String.Format("T: {0}", (Id: 10, Position: (1, 2, 3))).Replace("(", "[").Replace(")", "]"),
                },

                new LogInvocation
                {
                    Invoke = () =>
                    {
                        var person = (Id : 1, FirstName : (FixedString32Bytes)"dawda", LastName : (FixedString32Bytes)"dafwsgwegfsa");
                        Log.Info("T: {0}", person);
                    },
                    ExpectedOutput = String.Format("T: {0}", (Id: 1, FirstName: (FixedString32Bytes)"dawda", LastName: (FixedString32Bytes)"dafwsgwegfsa")).Replace("(", "[").Replace(")", "]"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("T: {0}", (a: 1, b: 2, c: (z: 42, y: 11))); },
                    ExpectedOutput = String.Format("T: {0}", (a: 1, b: 2, c: (z: 42, y: 11))).Replace("(", "[").Replace(")", "]"),
                },

#pragma warning restore CS8123
                new LogInvocation
                {
                    Invoke = () => { Log.Info("T: {0}", data1); },
                    ExpectedOutput = String.Format("T: {0}", data1).Replace("(", "[").Replace(")", "]"),
                },
            };

            foreach (var test in TestData)
            {
                ExecuteAndValidateLogCall(test);
            }

            for (var i = 0; i < TestData.Length; i++)
            {
                ExecuteAndValidateLogCallFromJob(TestData[i], i);
            }
        }

        [Test]
        public void LogSpecialTypes()
        {
            sbyte p1 = -120;
            byte p2 = 255;
            short p3 = -32760;
            ushort p4 = 65530;

            int p5 = 0;
            uint p6 = 0;
            long p7 = 0;
            ulong p8 = 0;
            char p9 = '*';
            float p10 = 12.12f;
            double p11 = 14.41;
            bool p12 = true;

            FixedString32Bytes s1 = "S32";
            FixedString64Bytes s2 = "S64";
            FixedString128Bytes s3 = "S128";
            FixedString512Bytes s4 = "S512";
            FixedString4096Bytes s5 = "S4096";

            TestData = new[]
            {
                new LogInvocation
                {
                    Invoke = () => { Log.Info("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11}",
                        p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12); },
                    ExpectedOutput = String.Format("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11}",
                        p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("{0} {1} {2} {3} {4}",
                        s1, s2, s3, s4, s5); },
                    ExpectedOutput = String.Format("{0} {1} {2} {3} {4}",
                        s1, s2, s3, s4, s5),
                },
            };

            foreach (var test in TestData)
            {
                ExecuteAndValidateLogCall(test);
            }

            for (var i = 0; i < TestData.Length; i++)
            {
                ExecuteAndValidateLogCallFromJob(TestData[i], i);
            }
        }

        class LogFuncCallAndPropertyClass
        {
            public static FixedString64Bytes ReturnFixedString64()
            {
                return "Hello From Static";
            }

            public long ReturnLong()
            {
                // max int is
                //     2147483647
                // max uint is
                //     4294967295
                return 9758231231;
            }

            public sbyte ReturnSByte()
            {
                return -23;
            }

            public double SomeDouble => 4432313.23;
            public float SomeFloat => 775123.23f;
        }

        [Test]
        public void LogFuncCallAndProperty()
        {
            var c = new LogFuncCallAndPropertyClass();
            TestData = new[]
            {
                new LogInvocation
                {
                    Invoke = () => { Log.Info("T1: {0}", LogFuncCallAndPropertyClass.ReturnFixedString64()); },
                    ExpectedOutput = String.Format("T1: {0}", LogFuncCallAndPropertyClass.ReturnFixedString64()),
                },

                new LogInvocation
                {
                    Invoke = () => { Log.Info("T2: {0}", c.ReturnLong()); },
                    ExpectedOutput = String.Format("T2: {0}", c.ReturnLong()),
                },

                new LogInvocation
                {
                    // TODO: DST-456 doubles are not supported currently - they are converted to floats loosing precision.
                    Invoke = () => { Log.Info("T3: {0}", c.SomeDouble); },
                    ExpectedOutput = String.Format("T3: {0}", (float)c.SomeDouble),
                },

                new LogInvocation
                {
                    Invoke = () => { Log.Info("T4: {0}", c.SomeFloat); },
                    ExpectedOutput = String.Format("T4: {0}", c.SomeFloat),
                },

                new LogInvocation
                {
                    Invoke = () => { Log.Info("T5: {0}", c.ReturnSByte()); },
                    ExpectedOutput = String.Format("T5: {0}", c.ReturnSByte()),
                },
            };

            foreach (var test in TestData)
            {
                ExecuteAndValidateLogCall(test);
            }

            for (var i = 0; i < TestData.Length; i++)
            {
                ExecuteAndValidateLogCallFromJob(TestData[i], i);
            }
        }

        [Test]
        public void LogBasicNonStructLiterals()
        {
            // sbyte, byte, short, ushort, int, uint, long, ulong, char, float, double, decimal, or bool
            TestData = new[]
            {
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with bool {0}", true); },
                    ExpectedOutput = String.Format("Short message with bool {0}", true),
                },

                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with sbyte {0}", (sbyte)42); },
                    ExpectedOutput = String.Format("Short message with sbyte {0}", (sbyte)42),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with byte {0}", (byte)42); },
                    ExpectedOutput = String.Format("Short message with byte {0}", (byte)42),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with short {0}", (short)42); },
                    ExpectedOutput = String.Format("Short message with short {0}", (short)42),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with ushort {0}", (ushort)42); },
                    ExpectedOutput = String.Format("Short message with ushort {0}", (ushort)42),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with int {0}", (int)42); },
                    ExpectedOutput = String.Format("Short message with int {0}", (int)42),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with uint {0}", (uint)42); },
                    ExpectedOutput = String.Format("Short message with uint {0}", (uint)42),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with long {0}", (long)42); },
                    ExpectedOutput = String.Format("Short message with long {0}", (long)42),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with ulong {0}", (ulong)42); },
                    ExpectedOutput = String.Format("Short message with ulong {0}", (ulong)42),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with char {0}", (char)42); },
                    ExpectedOutput = String.Format("Short message with char {0}", (char)42),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with float {0}", (float)42.3f); },
                    ExpectedOutput = String.Format("Short message with float {0}", (float)42.3f),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with double {0}", (double)42.23); },
                    ExpectedOutput = String.Format("Short message with double {0}", (double)42.23),
                },
                // new LogInvocation {
                //     Invoke = () => { Log.Info("Short message with decimal {0}", (decimal)42.42); },
                //     ExpectedOutput = String.Format("Short message with decimal {0}", (decimal)42.42),
                // },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with FixedString64 {0}", (FixedString64Bytes)"String64"); },
                    ExpectedOutput = String.Format("Short message with FixedString64 {0}", (FixedString64Bytes)"String64"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with FixedString32 {0}", (FixedString32Bytes)"String32"); },
                    ExpectedOutput = String.Format("Short message with FixedString32 {0}", (FixedString32Bytes)"String32"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with FixedString512 {0}", (FixedString512Bytes)"String512"); },
                    ExpectedOutput = String.Format("Short message with FixedString512 {0}", (FixedString512Bytes)"String512"),
                },
            };

            foreach (var test in TestData)
            {
                ExecuteAndValidateLogCall(test);
            }

            for (var i = 0; i < TestData.Length; i++)
            {
                ExecuteAndValidateLogCallFromJob(TestData[i], i);
            }
        }

        [TestCase(true, TestName = "LogBasicMessagesToFile_WithLiterals")]
        [TestCase(false, TestName = "LogBasicMessagesToFile_WithVariables")]
        public void LogBasicMessagesToFile(bool useLiterals)
        {
            using var tmpFile1 = new TempFileGenerator();

            LogBasicMessagesToFileOnce(tmpFile1.FilePath, tmpFile1.AbsPath, useLiterals);
        }

        [TestCase(true, TestName = "LogBasicMessagesTo2Files_WithLiterals")]
        [TestCase(false, TestName = "LogBasicMessagesTo2Files_WithVariables")]
        public void LogBasicMessagesTo2Files(bool useLiterals)
        {
            // write to one file, reconfigure and write to the another
            using var tmpFile1 = new TempFileGenerator();
            using var tmpFile2 = new TempFileGenerator();

            LogBasicMessagesToFileOnce(tmpFile1.FilePath, tmpFile1.AbsPath, useLiterals);
            LogBasicMessagesToFileOnce(tmpFile2.FilePath, tmpFile2.AbsPath, useLiterals);
        }

        private void LogBasicMessagesToFileOnce(string loggingFilePath, string absPath, bool useLiterals)
        {
            var logger = Log.Logger = new Logger(new LoggerConfig()
                .WriteTo.Console()
                .WriteTo.StringLogger(minLevel: LogLevel.Fatal) // to make 'reader' work
                .WriteTo.File(loggingFilePath));

            try
            {
                Assert.True(File.Exists(absPath), $"Logging file '{absPath}' doesn't exist");

                // Open a stream to read file contents and pipe them to our OutputHandler (so ValidateLogMessages can check the output)
                using (var stream = new FileStream(absPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        if (useLiterals)
                        {
                            InitBasicTestDataUsingLiterals();
                        }
                        else
                        {
                            InitBasicTestDataUsingVariables();
                        }

                        foreach (var test in TestData)
                        {
                            ExecuteAndValidateLogCall(test, reader);
                        }

                        for (var i = 0; i < TestData.Length; i++)
                        {
                            ExecuteAndValidateLogCallFromJob(TestData[i], i, reader);
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Assertions.Assert.AreEqual(logger, Log.Logger);
                logger.Dispose();
                UnityEngine.Assertions.Assert.AreEqual(null, Log.Logger);
            }
        }

        [Test]
        public void LogBasicMessagesWithOverloadValidation()
        {
            InitBasicTestDataUsingDifferentStringTypes();

            foreach (var test in TestData)
            {
                ExecuteAndValidateLogCall(test);
            }

            for (var i = 0; i < TestData.Length; i++)
            {
                ExecuteAndValidateLogCallFromJob(TestData[i], i);
            }
        }

        private void InitBasicTestDataUsingLiterals()
        {
            // Source generation has difference behavior depending if log message uses a string literal vs. a string variable.
            // These tests validate the "literal" usage.
            C1 = new ContextTestStruct1[]
            {
                new ContextTestStruct1
                {
                    Field1 = 101,
                },
                new ContextTestStruct1
                {
                    Field1 = 2,
                },
                new ContextTestStruct1
                {
                    Field1 = 3,
                },
                new ContextTestStruct1
                {
                    Field1 = -1,
                },
                new ContextTestStruct1
                {
                    Field1 = 999,
                },
            };

            C2 = new ContextTestStruct2[]
            {
                new ContextTestStruct2
                {
                    Field1 = 0.001f,
                    Field2 = 42,
                },
                new ContextTestStruct2
                {
                    Field1 = 1000.1f,
                    Field2 = 999,
                },
                new ContextTestStruct2
                {
                    Field1 = -2.0f,
                    Field2 = 12345,
                },
            };

            C3 = new ContextTestStruct3[]
            {
                new ContextTestStruct3
                {
                    Field1 = 3.14f,
                    Field2 = 1001,
                    Field3 = true,
                },
                new ContextTestStruct3
                {
                    Field1 = 1.00001f,
                    Field2 = 1234,
                    Field3 = false,
                },
                new ContextTestStruct3
                {
                    Field1 = 123.456f,
                    Field2 = 128,
                    Field3 = true,
                },
            };

            TestData = new LogInvocation[]
            {
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Basic message with context {0}", C1[0]); },
                    ExpectedOutput = String.Format("Basic message with context {0}", C1[0]),
                },

                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short message with no data"); },
                    ExpectedOutput = String.Format("Short message with no data"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Message has exactly 29 chars."); },
                    ExpectedOutput = String.Format("Message has exactly 29 chars."),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Message with 61 characters; the most a FixedString64 can hold"); },
                    ExpectedOutput = String.Format("Message with 61 characters; the most a FixedString64 can hold"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("A much, much, much longer message with exactly 125 characters. This is the longest char string that a FixedString128 can hold"); },
                    ExpectedOutput = String.Format("A much, much, much longer message with exactly 125 characters. This is the longest char string that a FixedString128 can hold"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Portez ce vieux whisky au juge blond qui fume sur son île intérieure, à"); },
                    ExpectedOutput = String.Format("Portez ce vieux whisky au juge blond qui fume sur son île intérieure, à"),
                },

                new LogInvocation
                {
                    Invoke = () => { Log.Info("Message has {0} context struct: {1}", C1[1], C3[0]); },
                    ExpectedOutput = String.Format("Message has {0} context struct: {1}", C1[1], C3[0]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("{0} Context data at start and end {1}", C2[0], C3[2]); },
                    ExpectedOutput = String.Format("{0} Context data at start and end {1}", C2[0], C3[2]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("This message has {0} contexts - {2}{1}", C1[2], C2[1], C3[1]); },
                    ExpectedOutput = String.Format("This message has {0} contexts - {2}{1}", C1[2], C2[1], C3[1]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Portez ce vieux whisky au juge {0} blond qui fume sur son {1} île intérieure, à", C2[0], C3[1]); },
                    ExpectedOutput = String.Format("Portez ce vieux whisky au juge {0} blond qui fume sur son {1} île intérieure, à", C2[0], C3[1]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("{0}", C1[3]); },
                    ExpectedOutput = String.Format("{0}", C1[3]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("", C2[2]); },
                    ExpectedOutput = String.Format("", C2[2]),
                },
            };
        }

        private void InitBasicTestDataUsingVariables()
        {
            // Source generation has difference behavior depending if log message uses a string literal vs. a string variable.
            // These tests validate the "variable" usage

            // As with the context data, message parameter types must be known at compile time
            // TextLogger supports both managed string and FixedString types for the log message.
            var M1 = new string[]
            {
                "Short message; no context",
                "Longer message with {0} contexts structs: {1}",
                "{0} Context data at start and end {1}",
                "{0}",
                "",
            };

            var M2 = new FixedString32Bytes[]
            {
                "Short message with no data",
                "Some data: {0}",
                "More data: {0} - {0} - {1}",
                "{0}",
                "",
            };

            var M3 = new FixedString128Bytes[]
            {
                "A much, much, much longer message that doesn't contain any context data at all.",
                "A much, much, much longer message that also contain {0} context data structs: {1} - {3}",
                "{1} This is a much longer message which contains context data at both the beginning and the end {2}",
                "Portez ce vieux whisky au juge {0} blond qui fume sur son île intérieure, à {1}",
                "{1}",
                "",
            };
            var M4 = new FixedString4096Bytes[]
            {
                "Use some funky message data! {0} \u0414\u0435\u0441\u044F\u0442\u0443\u044E \u041C\u0435\u0436{2}\u0434\u0443\u043D\u0430\u0440\u043E\u0434\u043D\u0443\u044E{1}",
                "Portez ce vieux whisky au juge blond qui fume {0} sur son île intérieure, à\n" +
                "côté de l'alcôve ovoïde, où les bûches se consument dans l'âtre, ce\n" +
                "qui lui permet de {1}{2} penser à la cænogenèse de l'être dont il est question\n" +
                "dans la cause ambiguë entendue à Moÿ, dans un capharnaüm qui,\n" +
                "pense-t - il, diminue çà et là la qualité de son uvre.{3}",
                "{0}",
                "",
            };

            C1 = new ContextTestStruct1[]
            {
                new ContextTestStruct1
                {
                    Field1 = 2,
                },
                new ContextTestStruct1
                {
                    Field1 = 5445,
                },
                new ContextTestStruct1
                {
                    Field1 = 1337,
                },
                new ContextTestStruct1
                {
                    Field1 = 3,
                }
            };

            C2 = new ContextTestStruct2[]
            {
                new ContextTestStruct2
                {
                    Field1 = 1.24f,
                    Field2 = 789,
                },
                new ContextTestStruct2
                {
                    Field1 = 39937.343f,
                    Field2 = 0xEADBEEF,
                },
                new ContextTestStruct2
                {
                    Field1 = 1010.101f,
                    Field2 = 0xF00,
                },
            };

            C3 = new ContextTestStruct3[]
            {
                new ContextTestStruct3
                {
                    Field1 = 0.99f,
                    Field2 = 54321,
                    Field3 = true,
                },
                new ContextTestStruct3
                {
                    Field1 = 000001f,
                    Field2 = 10000001,
                    Field3 = false,
                },
                new ContextTestStruct3
                {
                    Field1 = 0.12345667f,
                    Field2 = 3872,
                    Field3 = true,
                },
                new ContextTestStruct3
                {
                    Field1 = float.NaN,
                    Field2 = 101,
                    Field3 = false,
                },
            };


            TestData = new LogInvocation[]
            {
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M1[0]); },
                    ExpectedOutput = String.Format(M1[0]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M1[1], C1[0], C3[0]); },
                    ExpectedOutput = String.Format(M1[1], C1[0], C3[0]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M1[2], C2[0], C1[1]); },
                    ExpectedOutput = String.Format(M1[2], C2[0], C1[1]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M1[3], C1[0], C3[1]); },
                    ExpectedOutput = String.Format(M1[3], C1[0], C3[1]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M1[4], C3[1]); },
                    ExpectedOutput = String.Format(M1[4], C1[0], C3[1]),
                },

                new LogInvocation
                {
                    Invoke = () => { Log.Info(M2[0]); },
                    ExpectedOutput = String.Format(M2[0].ToString()),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M2[1], C2[1]); },
                    ExpectedOutput = String.Format(M2[1].ToString(), C2[1]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M2[2], C3[2], C1[2]); },
                    ExpectedOutput = String.Format(M2[2].ToString(), C3[2], C1[2]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M2[3], C2[1]); },
                    ExpectedOutput = String.Format(M2[3].ToString(), C2[1]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M2[4], C1[2], C2[0], C3[1]); },
                    ExpectedOutput = String.Format(M2[4].ToString(), C1[2], C2[0], C3[1]),
                },

                new LogInvocation
                {
                    Invoke = () => { Log.Info(M3[0], C2[2]); },
                    ExpectedOutput = String.Format(M3[0].ToString(), C2[2]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M3[1], C1[3], C2[2], C1[0], C3[1]); },
                    ExpectedOutput = String.Format(M3[1].ToString(), C1[3], C2[2], C1[0], C3[1]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M3[2], C3[0], C2[2], C3[1], C3[1]); },
                    ExpectedOutput = String.Format(M3[2].ToString(), C3[0], C2[2], C3[1], C3[1]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M3[3], C2[2], C3[3]); },
                    ExpectedOutput = String.Format(M3[3].ToString(), C2[2], C3[3]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M3[4], C1[0], C2[0]); },
                    ExpectedOutput = String.Format(M3[4].ToString(), C1[0], C2[0]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M3[5], C1[0]); },
                    ExpectedOutput = String.Format(M3[5].ToString(), C1[0]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M4[0], C1[1], C1[2], C2[2]); },
                    ExpectedOutput = String.Format(M4[0].ToString(), C1[1], C1[2], C2[2]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M4[1], C3[2], C1[0], C3[2], C3[3]); },
                    ExpectedOutput = String.Format(M4[1].ToString(), C3[2], C1[0], C3[2], C3[3]),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M4[2], C3[3]); },
                    ExpectedOutput = String.Format(M4[2].ToString(), C3[3]),
                },
            };
        }

        private void InitBasicTestDataUsingDifferentStringTypes()
        {
            var data = new ContextOverloadStruct
            {
                Field1 = 2.71828d,
                Field2 = 0xDEADBEEF000,
            };

            var M1 = new string[]
            {
                "Managed message variable string {0}. This string must be long enough to not fix any of the other FixedString types in order to validate it's being handled properly by source generation.",
                "A shorter managed message variable string {0}.",
            };

            var M2 = new FixedString32Bytes[]
            {
                "Short var => FS32 {0}",
                "Short ver2 {0} => FS32",
            };

            var M3 = new FixedString64Bytes[]
            {
                "A longer message variable string => FixedString64 {0}",
                "Another longer message variable string {0} => FixedString64",
            };

            var M4 = new FixedString128Bytes[]
            {
                "A much,     much,      much,      longer message variable string => FixedString128 {0}",
                "{Level} Another much,      much,     much,      much,      longer message variable string {0} => FixedString128",
            };

            TestData = new LogInvocation[]
            {
                new LogInvocation
                {
                    Invoke = () => {Log.Info(M3[0], data); },
                    ExpectedOutput = String.Format(M3[0].ToString(), data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short lit => FS32 {0}", data); },
                    ExpectedOutput = String.Format("Short lit => FS32 {0}", data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M4[0], data); },
                    ExpectedOutput = String.Format(M4[0].ToString(), data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Longer literal message => FixedString64 {0}", data); },
                    ExpectedOutput = String.Format("Longer literal message => FixedString64 {0}", data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M1[0], data); },
                    ExpectedOutput = String.Format(M1[0], data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("A much, much, much longer literal message => FixedString128 {0}", data); },
                    ExpectedOutput = String.Format("A much, much, much longer literal message => FixedString128 {0}", data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M2[0], data); },
                    ExpectedOutput = String.Format(M2[0].ToString(), data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M2[1], data); },
                    ExpectedOutput = String.Format(M2[1].ToString(), data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Another much, much, much longer literal message2 {0} => FixedString128", data); },
                    ExpectedOutput = String.Format("Another much, much, much longer literal message2 {0} => FixedString128", data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info(M1[1], data); },
                    ExpectedOutput = String.Format(M1[1], data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Longer literal message2 {0} => FixedString64 ", data); },
                    ExpectedOutput = String.Format("Longer literal message2 {0} => FixedString64 ", data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Fatal(M4[1], data); },
                    ExpectedOutput = String.Format(M4[1].ToString().Replace("{Level}", "FATAL"), data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Info("Short {Level} lit2 {0} => FS32", data); },
                    ExpectedOutput = String.Format("Short INFO lit2 {0} => FS32", data),
                },
                new LogInvocation
                {
                    Invoke = () => {Log.Info(M3[1], data); },
                    ExpectedOutput = String.Format(M3[1].ToString(), data),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Warning("A{0}", "B"); },
                    ExpectedOutput = String.Format("A{0}", "B"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Warning("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA{0}", "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"); },
                    ExpectedOutput = String.Format("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA{0}", "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Warning("C{0}", 4); },
                    ExpectedOutput = String.Format("C{0}", 4),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Warning("C{0}", (FixedString32Bytes)"4"); },
                    ExpectedOutput = String.Format("C{0}", (FixedString32Bytes)"4"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Warning("C{0}", (FixedString64Bytes)"5"); },
                    ExpectedOutput = String.Format("C{0}", (FixedString64Bytes)"5"),
                },
                new LogInvocation
                {
                    Invoke = () => { Log.Warning("C{0}", (FixedString128Bytes)"6"); },
                    ExpectedOutput = String.Format("C{0}", (FixedString128Bytes)"6"),
                }
            };
        }

        private void ExecuteAndValidateLogCall(in LogInvocation logParams, TextReader reader = null)
        {
            AssertNoLogDispatchedExpected();
            ExecuteLogCall(in logParams);

            AssertExpectedToHaveLogDispatched(Log.Logger.Handle, 1);

            Update();
            UpdateComplete();

            if (reader != null)
            {
                PipeTextToLoghandler(in logParams, reader);
            }

            ValidateLogMessages(in logParams);
        }

        private void ExecuteAndValidateLogCallFromJob(in LogInvocation logParams, int testDataIndex, TextReader reader = null)
        {
            var job = new LogMessageJob
            {
                testDataIndex = testDataIndex,
            };

            var handle = job.Schedule();
            Update(handle);
            UpdateComplete();

            if (reader != null)
            {
                PipeTextToLoghandler(in logParams, reader);
            }

            ValidateLogMessages(in logParams);
        }

        private static void ExecuteLogCall(in LogInvocation inst)
        {
            inst.Invoke();
        }

        private void ValidateLogMessages(in LogInvocation inst)
        {
            // A newline is automatically appended for each log message
            var output = GetStringFromFirstTestSink(Log.Logger);
            var expected = inst.ExpectedOutput + Environment.NewLine;

            Assert.IsNotEmpty(output, $"Logged output string is empty. expected to be {expected}");

            var parts = output.Split(new[] {" | "}, StringSplitOptions.None);

            // Validate we got both parts, timestamp string isn't empty, and message part matches expected output
            Assert.AreEqual(parts.Length, 3, $"Failed to split out Timestamp part for <{output}>");
            Assert.IsNotEmpty(parts[0], $"Timestamp part of log line is empty for <{output}>");
            Assert.IsNotEmpty(parts[1], $"Level part of log line is empty for <{output}>");
            Assert.AreEqual(expected, parts[2], $"TextLogger output doesn't match expected string");
        }

        private void PipeTextToLoghandler(in LogInvocation logParams, TextReader reader)
        {
            Logger l = Log.Logger;
            var n = l.SinksCount;
            for (var i = 0; i < n; i++)
            {
                var s = l.GetSink(i);
                if (s is FileSinkSystem fileSink)
                {
                    fileSink.Flush();
                }
            }

            var contents = reader.ReadToEnd();

            for (var i = 0; i < n; i++)
            {
                var s = l.GetSink(i);
                if (s is StringSink testSink)
                {
                    testSink.WriteText(contents);
                }
            }
        }
    }
}
