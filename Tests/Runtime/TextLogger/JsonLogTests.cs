using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
using Assert = UnityEngine.Assertions.Assert;
using Random = Unity.Mathematics.Random;

namespace Unity.Logging.Tests
{
    public struct JsonEntryElement
    {
        public ulong Timestamp;
        public string Level;
        public string Message;
        public JsonEntryProperties Properties;
    }

    public struct JsonEntryProperties
    {
        public string arg0;
    }


    [BurstCompile]
    public struct LogOnceJob : IJob
    {
        public void Execute()
        {
            using (Log.Decorate("Global decorator From Jobs", 1))
            {
                Log.Info("[Prefix]Hello from LogOnceJob! some int = {0}", 928713);
            }
        }
    }

    [BurstCompile]
    public static class DecoratorFunctions
    {
        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(LoggerManager.OutputWriterDecorateHandler))]
        public static void DecoratorFixedStringInt(in LogContextWithDecorator d)
        {
            Log.To(d).Decorate("SomeInt", 321);

            // re-entry deadlock bug
            //Log.Info("[DecoratorFixedStringInt] What if I log from here?");
        }

        [AOT.MonoPInvokeCallback(typeof(LoggerManager.OutputWriterDecorateHandler))]
        public static void DecoratorThreadId(in LogContextWithDecorator d)
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            Log.To(d).Decorate("ThreadId", threadId);

            // re-entry deadlock bug
            //Log.Info("[ThreadId] What if I log from here?");
        }

        [AOT.MonoPInvokeCallback(typeof(LoggerManager.OutputWriterDecorateHandler))]
        public static void DecoratorRandomInt(in LogContextWithDecorator d)
        {
            Unity.Mathematics.Random rnd;
            unsafe
            {
                rnd = Random.CreateFromIndex(42);
            }

            Log.To(d).Decorate("Rnd", rnd.NextInt());
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(LoggerManager.OutputWriterDecorateHandler))]
        public static void DecoratorThatIsCalledForJob(in LogContextWithDecorator d)
        {
            Log.To(d).Decorate("Job", "FromJobOnly");
        }
    }

    public class JsonLogTests : LoggingTestFixture
    {
        static string OurJsonEscape(string s)
        {
            unsafe
            {
                var sAsUnsafeText = new UnsafeText(512, Allocator.Persistent);
                sAsUnsafeText.Append(s);

                var unsafeText = new UnsafeText(s.Length, Allocator.Persistent);
                Unity.Logging.TextLoggerParser.AppendEscapedJsonString(ref unsafeText, sAsUnsafeText.GetUnsafePtr(), sAsUnsafeText.Length);

                var result = unsafeText.ToString();

                unsafeText.Dispose();
                sAsUnsafeText.Dispose();

                return result;
            }
        }

        [Test]
        [TestCase("{\"foo\": \"bar\", \"baz\": \"boo\"}")]
        [TestCase("/'dlawdj!@#!@!4jfhef89-")]
        [TestCase("Text with special character /\"\'\b\f\t\r\n.")]
        [TestCase(@"@#!$Y&(UAJDOINW@H$G801923ui @{їє_v_при,-50:##.0;При}!#
\u0001 \u0002 \u0003 \u0004 \u0005 \u0006 \u0007 \u0008
            ඖaĔĔaĔඖĔ,42:+aǊǊǊ𒀖Ǒඖ \"" \"" 𒀖 \"" \"" \"" dawdwa")]
        public void JsonEscapeTests(string s)
        {
            var wasValidJson = IsStringValidJson(s);

            var escaped = OurJsonEscape(s);

            var isValidJson = IsStringValidJson(escaped);

            Assert.IsTrue(isValidJson, $"wasValidJson = {wasValidJson}. isValidJson = {isValidJson}.\ns = <{s}>\nescaped = <{escaped}>");
        }

        private bool IsStringValidJson(string str)
        {
            try
            {
                str = $"{{\"str\": \"{str}\"}}";

                var obj = JObject.Parse(str);

                return obj != null;
            }
            catch
            {
                return false;
            }
        }

        [Test]
        public void JsonEscapeTestWithControl()
        {
            string generatedString = "";
            for (int i = 1; i < 0xFF; i++)
            {
                generatedString += (char)i;
            }

            JsonEscapeTests(generatedString);
        }


        [Test]
        public void JsonLoggerTest()
        {
            var data1 = new TestLogDataSet(256, Allocator.Persistent);
            data1.Set(0, @"1 This [ is ] Json
 check
 "" String \ / <{0}> !");
            data1.Set(1, @"
2 This [ is ] Json
 check
 "" String \ / <{0}> !");

            var allData = new List<TestLogDataSet> {data1};

            using var tmpFile1 = new TempFileGenerator();

            try
            {
                TimeStampWrapper.SetHandlerForTimestamp(RawGetTimestampHandlerAsCounter, true);
                TextLoggerParser.SetOutputHandlerForTimestamp(RawTimestampHandler, true);
                var log = new LoggerConfig().MinimumLevel.Verbose()
                    .OutputTemplate(TemplateTestPattern3.PatternString)
                    .WriteTo.StringLogger()
                    .WriteTo.JsonFile(tmpFile1.FilePath)
                    .CreateLogger();

                LoggerManager.ScheduleUpdateLoggers();
                var logJob = data1.ScheduleExecute(log.Handle);
                LoggerManager.ScheduleUpdateLoggers();

                logJob.Complete();

                LoggerManager.FlushAll();

                LoggerManager.DeleteAllLoggers();

                var json = tmpFile1.ReadAllText();
                var elements = JsonConvert.DeserializeObject<JsonEntryElement[]>(json);

                foreach (var data in allData)
                    data.ValidateJson(elements);
            }
            finally
            {
                foreach (var data in allData)
                {
                    try
                    {
                        data.Dispose();
                    }
                    catch
                    {
                        // Ignore. We need to dispose all the Native containers
                    }
                }
            }
        }

        [Test]
        public void JsonLoggerDecorationTest8Times()
        {
            // sometimes decorators can be released. need to extra check that few times

            for (int i = 0; i < 8; i++)
            {
                JsonLoggerDecorationTest(false);
            }
        }

        [Test]
        public void JsonLoggerDecorationTest8TimesWithStackTrace()
        {
            // sometimes decorators can be released. need to extra check that few times

            for (int i = 0; i < 8; i++)
            {
                JsonLoggerDecorationTest(true);
            }
        }

        public void JsonLoggerDecorationTest(bool stackTrace)
        {
            using var tmpFile1 = new TempFileGenerator();
            using var tmpFile2 = new TempFileGenerator();
            using var tmpFile2_copy1 = new TempFileGenerator();
            using var tmpFile2_copy2 = new TempFileGenerator();
            using var tmpFile2_copy3 = new TempFileGenerator();
            using var tmpFile2_copy4 = new TempFileGenerator();

            Logger log;
            Logger log2;
            JsonLogValidator logValidator;
            JsonLogValidator log2Validator;
            {
                TextLoggerParser.SetOutputHandlerForTimestamp(RawTimestampHandler, true);
                log = new LoggerConfig().MinimumLevel.Verbose()
                                        .OutputTemplate(TemplateTestPattern3.PatternString)
                                        .WriteTo.StringLogger()
                                        .WriteTo.JsonFile(tmpFile1.FilePath, captureStackTrace: stackTrace)
                                        .CreateLogger();

                logValidator = new JsonLogValidator(log, stackTrace);

                using var decorConst1 = Log.To(log).Decorate("ConstantExampleLog1", 999999);
                logValidator.RegisterDecoratorStart("ConstantExampleLog1", 999999);

                using var decor1 = Log.To(log).Decorate("SomeInt", DecoratorFunctions.DecoratorFixedStringInt, true);
                logValidator.RegisterDecoratorStart("SomeInt");

                log2 = new LoggerConfig().MinimumLevel.Verbose()
                                         .OutputTemplate(TemplateTestPattern1.PatternString)
                                         .WriteTo.StringLogger()
                                         .WriteTo.JsonFile(tmpFile2.FilePath, captureStackTrace: stackTrace)
                                         .WriteTo.JsonFile(tmpFile2_copy1.FilePath, captureStackTrace: stackTrace)
                                         .WriteTo.JsonFile(tmpFile2_copy2.FilePath, captureStackTrace: stackTrace)
                                         .WriteTo.JsonFile(tmpFile2_copy3.FilePath, captureStackTrace: stackTrace)
                                         .WriteTo.JsonFile(tmpFile2_copy4.FilePath, captureStackTrace: stackTrace)
                                         .CreateLogger();
                log2Validator = new JsonLogValidator(log2, stackTrace);

                using var decorConst2 = Log.To(log2).Decorate("ConstantExample", 419841);
                log2Validator.RegisterDecoratorStart("ConstantExample", 419841);

                using var decor2 = Log.To(log2).Decorate("ThreadId", DecoratorFunctions.DecoratorThreadId, false);
                log2Validator.RegisterDecoratorStart("ThreadId");

                LoggerManager.ScheduleUpdateLoggers();
                LoggerManager.ScheduleUpdateLoggers();
                LoggerManager.ScheduleUpdateLoggers();
                Thread.Sleep(10);
                LoggerManager.CompleteUpdateLoggers();

                var cstruct = TestLogData.ComplexType.Random();

                using var _ = Log.To(log2).Decorate("Decorator for Log2", cstruct);
                log2Validator.RegisterDecoratorStart("Decorator for Log2");

                using (var decor3 = Log.Decorate("Rnd", DecoratorFunctions.DecoratorRandomInt, false))
                {
                    using var l11 = logValidator.RegisterDecoratorStart("Rnd");
                    using var l12 = log2Validator.RegisterDecoratorStart("Rnd");

                    using (var aaaa = Log.Decorate("Global decorator", 1))
                    {
                        using var l21 = logValidator.RegisterDecoratorStart("Global decorator");
                        using var l22 = log2Validator.RegisterDecoratorStart("Global decorator");

                        LoggerManager.ScheduleUpdateLoggers();
                        LoggerManager.ScheduleUpdateLoggers();
                        LoggerManager.ScheduleUpdateLoggers();
                        LoggerManager.ScheduleUpdateLoggers();
                        Thread.Sleep(10);
                        LoggerManager.CompleteUpdateLoggers();

                        Log.Info("[Prefix]Test1 {0}. String = {1}", 2, "SomeString");
                        logValidator.Info("[Prefix]Test1 {0}. String = {1}", 2, "SomeString");

                        Log.To(log2).Info("[Prefix]Test2 {0}. String = {1}", -2, (FixedString512Bytes)"SomeString2");
                        log2Validator.Info("[Prefix]Test2 {0}. String = {1}", -2, (FixedString512Bytes)"SomeString2");

                        {
                            using var l31 = logValidator.RegisterDecoratorStart("Global decorator From Jobs", 1);

                            using (Log.Decorate("Job", DecoratorFunctions.DecoratorThatIsCalledForJob, true))
                            {
                                using var a = Log.Decorate("ThreadId", DecoratorFunctions.DecoratorThreadId, true);

                                using var l41 = logValidator.RegisterDecoratorStart("Job", "FromJobOnly");
                                using var l51 = logValidator.RegisterDecoratorStart("ThreadId");

                                LoggerManager.ScheduleUpdateLoggers();
                                LoggerManager.ScheduleUpdateLoggers();
                                Thread.Sleep(10);

                                new LogOnceJob().Schedule().Complete();
                                logValidator.Info("[Prefix]Hello from LogOnceJob! some int = {0}", 928713);

                                LoggerManager.ScheduleUpdateLoggers();
                                LoggerManager.ScheduleUpdateLoggers();
                                LoggerManager.CompleteUpdateLoggers();
                            }
                        }
                    }

                    Log.Info("[Prefix]Test3 {NamedArg}. String = {1}", 42, "SomeString3");
                    logValidator.Info("[Prefix]Test3 {NamedArg}. String = {1}", new JsonLogValidator.NamedArgument {Name = "NamedArg", Obj = 42}, "SomeString3");
                    Log.Info("[Prefix]Test4 {0}. String = {1}", 72, "SomeString4");
                    logValidator.Info("[Prefix]Test4 {0}. String = {1}", 72, "SomeString4");
                    Log.Info("[Prefix]Test5 {0}. String = {1}", 54, "SomeString5");
                    logValidator.Info("[Prefix]Test5 {0}. String = {1}", 54, "SomeString5");

                    LoggerManager.ScheduleUpdateLoggers();
                    LoggerManager.ScheduleUpdateLoggers();
                    LoggerManager.ScheduleUpdateLoggers();
                    LoggerManager.CompleteUpdateLoggers();

                    Log.To(log2).Info("[Prefix]Test6 {0}. String = {1}", 732, "SomeString6");
                    log2Validator.Info("[Prefix]Test6 {0}. String = {1}", 732, "SomeString6");
                    Log.To(log2).Info("[Prefix]Test7 {0}. String = {1}", 4352, "SomeString7");
                    log2Validator.Info("[Prefix]Test7 {0}. String = {1}", 4352, "SomeString7");
                }

                Log.Info("[Prefix]Test8 {0}. String = {1}", -3462, "SomeString8");
                logValidator.Info("[Prefix]Test8 {0}. String = {1}", -3462, "SomeString8");

                LoggerManager.ScheduleUpdateLoggers();
                LoggerManager.CompleteUpdateLoggers();

                Log.To(log2).Info("[Prefix]Test9 {0}. String = {1}", -63462, "SomeString9");
                log2Validator.Info("[Prefix]Test9 {0}. String = {1}", -63462, "SomeString9");
            }

            LoggerManager.ScheduleUpdateLoggers();
            LoggerManager.CompleteUpdateLoggers();

            LoggerManager.ScheduleUpdateLoggers();
            LoggerManager.CompleteUpdateLoggers();

            {
                using var lock1 = LogControllerScopedLock.Create(log.Handle);
                using var lock2 = LogControllerScopedLock.Create(log2.Handle);

                ref var lc1 = ref lock1.GetLogController();
                ref var lc2 = ref lock2.GetLogController();

                Assert.AreEqual(0, LoggerManager.GlobalDecorateHandlerCount());
                Assert.AreEqual(0, lc1.DecorateHandlerCount());
                Assert.AreEqual(0, lc2.DecorateHandlerCount());

                Assert.AreEqual(0, LoggerManager.GlobalDecoratePayloadsCount());
                Assert.AreEqual(0, lc1.DecoratePayloadsCount());
                Assert.AreEqual(0, lc2.DecoratePayloadsCount());

                Assert.AreEqual((uint)0, lc1.MemoryManager.GetCurrentDefaultBufferUsage(), lc1.MemoryManager.DebugStateString("Log1").ToString());
                Assert.AreEqual((uint)0, lc2.MemoryManager.GetCurrentDefaultBufferUsage(), lc2.MemoryManager.DebugStateString("Log2").ToString());

                ref var gmem = ref LoggerManager.GetGlobalDecoratorMemoryManager();
                Assert.AreEqual((uint)0, gmem.GetCurrentDefaultBufferUsage(), gmem.DebugStateString("Global").ToString());
            }

            LoggerManager.DeleteAllLoggers();

            var json1 = tmpFile1.ReadAllText();
            var arr1 = JArray.Parse(json1);

            logValidator.Validate(arr1);

            var json2 = tmpFile2.ReadAllText();
            var arr2 = JArray.Parse(json2);

            log2Validator.Validate(arr2);

            tmpFile2_copy1.SameText(json2);
            tmpFile2_copy2.SameText(json2);
            tmpFile2_copy3.SameText(json2);
            tmpFile2_copy4.SameText(json2);
        }

        private void AssertJsonProperty(JToken p0, string arg0, string p2)
        {
            Assert.AreEqual(p2, p0["Properties"][arg0].Value<string>());
        }

        private void AssertJsonProperty(JToken p0, string arg0, int p2)
        {
            Assert.AreEqual(p2, p0["Properties"][arg0].Value<int>());
        }

        private void AssertJsonPropertyAll(JArray p0, string propName)
        {
            foreach (var elem in p0)
            {
                Assert.IsNotNull(elem["Properties"][propName]);
            }
        }

        private void AssertJsonPropertyAll(JArray p0, string propName, int v)
        {
            foreach (var elem in p0)
            {
                Assert.AreEqual(v, elem["Properties"][propName].Value<int>());
            }
        }

        private void AssertJsonPropertyCount(JToken p0, int p1)
        {
            Assert.AreEqual(p1, p0["Properties"].Count());
        }

        private void AssertJson(JToken jToken, LogLevel info, string mess)
        {
            UnityEngine.Assertions.Assert.AreEqual(info.ToString().ToUpper(), jToken["Level"].Value<string>());
            UnityEngine.Assertions.Assert.AreEqual(mess, jToken["Message"].Value<string>());
        }

        [Test]
        public void JsonParseTest1()
        {
            var json = @"{""Timestamp"":1404185116266,""Level"":""DEBUG"",""Message"":""764A15IKAEDCPXX91U63M4YN22MP77FXOKB6CBOCQKKL4M15ITFFAI7PBHI8T2MD9QYEV160G3MCTNTPARGO19WFMV443PTX7HMOYWJ{0}RWP9G546YY28CV8L93S2A6B60KYFLBF1NHZ2M9QKRZFGARQB86{0}""}";

            var elem = JsonConvert.DeserializeObject<JsonEntryElement>(json);

            Assert.AreEqual((ulong)1404185116266, elem.Timestamp);
            Assert.AreEqual("DEBUG", elem.Level);
            Assert.AreEqual("764A15IKAEDCPXX91U63M4YN22MP77FXOKB6CBOCQKKL4M15ITFFAI7PBHI8T2MD9QYEV160G3MCTNTPARGO19WFMV443PTX7HMOYWJ{0}RWP9G546YY28CV8L93S2A6B60KYFLBF1NHZ2M9QKRZFGARQB86{0}", elem.Message);
        }

        [Test]
        public void JsonParseTest2()
        {
            var json = @"{""Timestamp"":1404185180166,""Level"":""INFO"",""Message"":""764D0OLYR2YCYOW5GL6ISZVPFY7UVOYK89TPNTI15RLIF5LKVSEIY4ONBC7CQ1DDDNEY5GSWCFNBOMKJNX8DJQWDVFNKWGKDOHE11WP{0}O9RGTRN9Y2XY3293X1ADZ3HA55JWGHSS0ITD2KDWHGYS8SQLON{0}"",""Properties"":{""arg0"":""[False, [, 1453964252]""}}";

            var elem = JsonConvert.DeserializeObject<JsonEntryElement>(json);

            Assert.AreEqual((ulong)1404185180166, elem.Timestamp);
            Assert.AreEqual("INFO", elem.Level);
            Assert.AreEqual("764D0OLYR2YCYOW5GL6ISZVPFY7UVOYK89TPNTI15RLIF5LKVSEIY4ONBC7CQ1DDDNEY5GSWCFNBOMKJNX8DJQWDVFNKWGKDOHE11WP{0}O9RGTRN9Y2XY3293X1ADZ3HA55JWGHSS0ITD2KDWHGYS8SQLON{0}", elem.Message);
            Assert.AreEqual("[False, [, 1453964252]", elem.Properties.arg0);
        }

        [Test]
        public void JsonParseTest3()
        {
            var json1 = @"{""Timestamp"":1404185438966,""Level"":""WARNING"",""Message"":""7643XUC1XGMEH1S5E41JFH7OIYZ1DM9DMMY9Z4VTETA8RHG08LXZA01OPCV84LYKHJ15CSFKJHEVH86AR821ZJCWVKUXJWZ3500HPLY{0}QFNQ2VFHQFVR5S6V8GFJ18SBWSL0QIYUFKMVL2KAQDDSXF3L8F{0}"",""Properties"":{""arg0"":""[True, `, 632420808]""}}";
            var json2 = @"{""Timestamp"":1404185447866,""Level"":""FATAL"",""Message"":""755MOB7SQZ484MGWZ5STVH3OTR6JY12BG5PDPJJBNUYIGRX57SG3JIWM7337EX4Z62187DHPVQGBSUHC4BMFU928JCFWQM7ZZ9LNZY7{0}4K1H3BV4NV0DCLYE793Z58U8JSRLTI3NFDS43196XI1B0J5OB4{0}"",""Properties"":{}}";

            var json = $"[{json1},{json2}]";

            var elements = JsonConvert.DeserializeObject<JsonEntryElement[]>(json);

            Assert.AreEqual(2, elements.Length);

            Assert.AreEqual((ulong)1404185438966, elements[0].Timestamp);
            Assert.AreEqual("WARNING", elements[0].Level);
            Assert.AreEqual("7643XUC1XGMEH1S5E41JFH7OIYZ1DM9DMMY9Z4VTETA8RHG08LXZA01OPCV84LYKHJ15CSFKJHEVH86AR821ZJCWVKUXJWZ3500HPLY{0}QFNQ2VFHQFVR5S6V8GFJ18SBWSL0QIYUFKMVL2KAQDDSXF3L8F{0}", elements[0].Message);
            Assert.AreEqual("[True, `, 632420808]", elements[0].Properties.arg0);

            Assert.AreEqual((ulong)1404185447866, elements[1].Timestamp);
            Assert.AreEqual("FATAL", elements[1].Level);
            Assert.AreEqual("755MOB7SQZ484MGWZ5STVH3OTR6JY12BG5PDPJJBNUYIGRX57SG3JIWM7337EX4Z62187DHPVQGBSUHC4BMFU928JCFWQM7ZZ9LNZY7{0}4K1H3BV4NV0DCLYE793Z58U8JSRLTI3NFDS43196XI1B0J5OB4{0}", elements[1].Message);
            Assert.AreEqual(null, elements[1].Properties.arg0);
        }
    }
}
