using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Logging.Internal;
using Unity.Logging.Internal.Debug;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging.Sinks;

namespace Unity.Logging.Tests
{
    [BurstCompile]
    public abstract class LoggingTestFixture
    {
        public enum LoggingUpdateTypeInTests
        {
            Serial,
            Parallel
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(TextLoggerParser.OutputWriterTimestampHandler))]
        internal static TextLoggerParser.ContextWriteResult RawTimestampHandler(ref UnsafeText hstring, long timestamp)
        {
            hstring.Append(timestamp);
            return TextLoggerParser.ContextWriteResult.Success;
        }

        struct RawGetTimestampHandlerKey {}
        static readonly SharedStatic<long> s_Counter = SharedStatic<long>.GetOrCreate<RawGetTimestampHandlerKey>();

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(TimeStampWrapper.CustomGetTimestampHandler))]
        internal static long RawGetTimestampHandlerAsCounter()
        {
            return Interlocked.Increment(ref s_Counter.Data);
        }

        [SetUp]
        public virtual void Setup()
        {
            // BUG DST-459: this is a workaround - calling static cctor from the main thread. Remove it and some tests will deadlock
            var a = Log.Logger;

            s_Counter.Data = 0;
            ManagedStackTraceWrapper.ForceClearAll();

            TimeStampWrapper.TimestampFormat = TimeStampWrapper.TimestampFormatDefault;
            TimeStampWrapper.SetHandlerForTimestamp(null);
            TextLoggerParser.SetOutputHandlerForTimestamp(null);
            TextLoggerParser.SetOutputHandlerForLevel(null);

            SelfLog.SetMode(SelfLog.Mode.EnabledInUnityEngineDebugLogError);

            LoggerManager.DeleteAllLoggers();
            LoggerManager.ClearOnNewLoggerCreatedEvent();
            StringSink.AssertNoSinks();
        }

        [TearDown]
        public virtual void TearDown()
        {
            TimeStampWrapper.TimestampFormat = TimeStampWrapper.TimestampFormatDefault;
            TimeStampWrapper.SetHandlerForTimestamp(null);
            TextLoggerParser.SetOutputHandlerForTimestamp(null);
            TextLoggerParser.SetOutputHandlerForLevel(null);

            LoggerManager.DeleteAllLoggers();

            ManagedStackTraceWrapper.AssertNoAllocatedResources();
        }

        public JobHandle ScheduleUpdate(LoggingUpdateTypeInTests updateTypeInTests, JobHandle logJobHandle)
        {
            if (updateTypeInTests == LoggingUpdateTypeInTests.Serial)
                logJobHandle.Complete();

            logJobHandle = LoggerManager.ScheduleUpdateLoggers(logJobHandle);

            if (updateTypeInTests == LoggingUpdateTypeInTests.Serial)
                logJobHandle.Complete();

            return logJobHandle;
        }

        public JobHandle ScheduleUpdateForLogger(LoggingUpdateTypeInTests updateTypeInTests, Logger log, JobHandle logJobHandle)
        {
            if (updateTypeInTests == LoggingUpdateTypeInTests.Serial)
                logJobHandle.Complete();

            logJobHandle = log.ScheduleUpdate(logJobHandle);

            if (updateTypeInTests == LoggingUpdateTypeInTests.Serial)
                logJobHandle.Complete();

            return logJobHandle;
        }

        public static Logger CreateTempLogger()
        {
            return new LoggerConfig().MinimumLevel.Verbose()
                .OutputTemplate(TemplateTestPattern2.PatternString)
                .WriteTo.StringLogger()
                .CreateLogger();
        }

        public static TestLogDataSet CreateDataSet(int n)
        {
            return new TestLogDataSet(n, Allocator.Persistent);
        }

        public class TempFileGenerator : IDisposable
        {
            private static int s_FilePathCounter;

            public readonly string FilePath;
            public readonly string AbsPath;

            public TempFileGenerator(string prefix = "")
            {
                var st = new StackTrace();
                var sf = st.GetFrame(1);

                for (var i = 0; i < 100; i++)
                {
                    FilePath = $@"{sf.GetMethod().Name}_{prefix}_{DateTime.Now.Ticks}_{++s_FilePathCounter}.txt";

                    AbsPath = Path.GetFullPath(FilePath);
                    if (File.Exists(AbsPath))
                        continue;

                    return;
                }

                throw new Exception("TempFileGenerator's GenerateFilePath failed");
            }

            public void Dispose()
            {
                try
                {
                    if (File.Exists(AbsPath))
                        File.Delete(AbsPath);
                }
                catch
                {
                    // ignore
                }
            }

            public string ReadAllText()
            {
                return File.ReadAllText(AbsPath);
            }

            public string[] ReadAllLines()
            {
                return File.ReadAllLines(AbsPath);
            }

            public string RollingFileAllText(int roll)
            {
                var filename = Path.GetFileNameWithoutExtension(AbsPath);

                if (roll > 0)
                    filename += "_" + roll;

                filename += Path.GetExtension(AbsPath);

                return File.ReadAllText(filename);
            }

            public void SameText(string json2)
            {
                Assert.AreEqual(json2, ReadAllText());
            }
        }

        public static void AssertExpectedToHaveLogDispatched(in LoggerHandle loggerHandle, int number)
        {
            using var lock1 = LogControllerScopedLock.Create(loggerHandle);

            ref DispatchQueue queue = ref lock1.GetLogController().DispatchQueue;
            Assert.IsTrue(queue.IsCreated, "DispatchQueue is not created!");
            Assert.AreEqual(number, queue.TotalLength, "DispatchQueue has wrong amount of log messages in it");
        }

        public static void AssertNoLogDispatchedExpected()
        {
            var total = LoggerManager.GetTotalDispatchedMessages();
            Assert.AreEqual(0, total, $"DispatchQueue is not empty as expected - {total} messages found");
        }

        public static void AssertNoLogDispatchedExpected(in LoggerHandle loggerHandle)
        {
            using var lock1 = LogControllerScopedLock.Create(loggerHandle);

            ref DispatchQueue queue = ref lock1.GetLogController().DispatchQueue;
            Assert.IsTrue(queue.IsCreated, "DispatchQueue is not created!");
            Assert.AreEqual(0, queue.TotalLength, $"DispatchQueue is not empty as expected - {queue.TotalLength} messages found");
        }

        internal void AssertExpectedToHaveSink<T>(Logger logger) where T : ISinkSystemInterface, new()
        {
            Assert.IsTrue(logger.SinksCount > 0, "logger doesn't have sinks");
            var sink = logger.GetSink<T>();
            UnityEngine.Assertions.Assert.IsTrue(sink != null, $"{typeof(T).FullName} is not found in logger");
        }

        internal void AssertExpectedToNotHaveSink<T>(Logger logger) where T : ISinkSystemInterface, new()
        {
            var sink = logger.GetSink<T>();
            UnityEngine.Assertions.Assert.IsTrue(sink == null, $"{typeof(T).FullName} is not Expected to be in logger");
        }

        internal string GetStringFromFirstTestSink(Logger log)
        {
            var sink = log.GetSink<StringSink>();
            if (sink != null)
            {
                return sink.GetString();
            }

            throw new Exception($"TextLoggerTestSink is not found in the logger = {log.Handle.Value}");
        }

        public void Update(JobHandle input = default)
        {
            LoggerManager.ScheduleUpdateLoggers(input);
        }

        public void UpdateComplete()
        {
            // schedule another one to make sure 2 buffers are proceeded
            LoggerManager.ScheduleUpdateLoggers().Complete();
        }
    }



}
