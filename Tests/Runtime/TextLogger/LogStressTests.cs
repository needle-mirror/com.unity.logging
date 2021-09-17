#if !UNITY_DOTSRUNTIME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

#pragma warning disable 8123

namespace Unity.Logging.Tests
{
    [BurstCompile]
    public class LogStressTests : LoggingTestFixture
    {
        public enum TestParallelType
        {
            SingleThreaded,
            Parallel
        }

        [Test, Performance]
        [TestCase(TestParallelType.SingleThreaded)]
        [TestCase(TestParallelType.Parallel)]
        public void Stress1(TestParallelType testType)
        {
            LoggerManager.AssertNoLocks();
            TimeStampWrapper.SetHandlerForTimestamp(RawGetTimestampHandlerAsCounter, true);

            var mMainName = $"{testType} Stress test";

            var mEmit1Name = $"[{testType}] Stress test. Emitting Logs 1";
            var mEmit2Name = $"[{testType}] Stress test. Emitting Logs 2";
            var mSyncPoint1Name = $"[{testType}] Stress test. Setting new Logger - sync point 1";
            var mSyncPoint2Name = $"[{testType}] Stress test. Setting new Logger - sync point 2";
            var mSyncPoint3Name = $"[{testType}] Stress test. Setting new Logger - sync point 3";

            var mEmit1OldWayName = $"[{testType}] Stress test. Emitting Logs UnityEngine.Debug.Log 1";
            var mEmit2OldWayName = $"[{testType}] Stress test. Emitting Logs UnityEngine.Debug.Log 2";


            string[] markerNames = {mMainName, mEmit1Name, mEmit1OldWayName, mEmit2Name, mEmit2OldWayName, mSyncPoint1Name, mSyncPoint2Name, mSyncPoint3Name};

            var mEmit1 = new ProfilerMarker(mEmit1Name);
            var mEmit2 = new ProfilerMarker(mEmit2Name);
            var mEmitOldWay1 = new ProfilerMarker(mEmit1OldWayName);
            var mEmitOldWay2 = new ProfilerMarker(mEmit2OldWayName);
            var mSyncPoint1 = new ProfilerMarker(mSyncPoint1Name);
            var mSyncPoint2 = new ProfilerMarker(mSyncPoint2Name);
            var mSyncPoint3 = new ProfilerMarker(mSyncPoint3Name);

            using var tmpFile1 = new TempFileGenerator();
            using var tmpFile2 = new TempFileGenerator();

            const int n = 2000;

            var oldStackTrace = Application.GetStackTraceLogType(LogType.Log);
            try
            {
                Log.Logger = null; // sync point

                GC.Collect(0, GCCollectionMode.Forced, true, true);

                Logger log1 = default;
                Logger log2 = default;

                Measure.Method(() => {
                    using var m = new ProfilerMarker(mMainName).Auto();

                    TextLoggerParser.SetOutputHandlerForTimestamp(RawTimestampHandler, true);

                    LogMemoryManagerParameters.GetDefaultParameters(out var parameters);
                    parameters.InitialBufferCapacity *= 64;
                    parameters.OverflowBufferSize *= 32;


                    using (var _ = mSyncPoint1.Auto())
                    {
                        log1 = Log.Logger = new LoggerConfig().MinimumLevel.Debug()
                            .OutputTemplate(TemplateTestPattern1.PatternString)
                            .WriteTo.Console()
                            .WriteTo.File(tmpFile1.FilePath)
                            .WriteTo.StringLogger()
                            .CreateLogger(parameters);
                    }

                    if (testType == TestParallelType.Parallel)
                    {
                        using (var _ = mEmit1.Auto())
                        {
                            Parallel.For(0, n, (int i) =>
                            {
                                Log.Verbose("Hi! <{0}> this is {Level} message. Привет, {1}!", i, "%Username%"); // ignored
                                Log.Debug("So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.Info("<{0}> Info msg.. {1}", i, 42);
                                Log.Warning("Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Log.Error("Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.Fatal("Unfortunately, {Level} == FATAL <{0}>", i);
                            });
                        }
                    }
                    else
                    {
                        using (var _ = mEmit1.Auto())
                        {
                            for (int i = 0; i < n; i++)
                            {
                                Log.Verbose("Hi! <{0}> this is {Level} message. Привет, {1}!", i, "%Username%"); // ignored
                                Log.Debug("So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.Info("<{0}> Info msg.. {1}", i, 42);
                                Log.Warning("Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Log.Error("Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.Fatal("Unfortunately, {Level} == FATAL <{0}>", i);
                            }
                        }
                    }

                    using (var _ = mSyncPoint2.Auto())
                    {
                        log2 = Log.Logger = new LoggerConfig().MinimumLevel.Info()
                            .OutputTemplate(TemplateTestPattern2.PatternString)
                            .WriteTo.Console()
                            .WriteTo.File(tmpFile2.FilePath)
                            .WriteTo.StringLogger()
                            .CreateLogger(parameters);
                    }

                    if (testType == TestParallelType.Parallel)
                    {
                        using (var _ = mEmit2.Auto())
                        {
                            Parallel.For(0, n, (int i) =>
                            {
                                Log.Verbose("#2 Hi! <{0}> this is {Level} message. Привет, {1}!", i, "%Username%");                                  // ignored
                                Log.Debug("#2 So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i));  // ignored
                                Log.Info("#2 <{0}> Info msg.. {1}", i, 42);
                                Log.Warning("#2 Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Log.Error("#2 Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.Fatal("#2 Unfortunately, {Level} == FATAL <{0}>", i);
                            });
                        }
                    }
                    else
                    {
                        using (var _ = mEmit2.Auto())
                        {
                            for (int i = 0; i < n; i++)
                            {
                                Log.Verbose("#2 Hi! <{0}> this is {Level} message. Привет, {1}!", i, "%Username%");                                  // ignored
                                Log.Debug("#2 So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i));  // ignored
                                Log.Info("#2 <{0}> Info msg.. {1}", i, 42);
                                Log.Warning("#2 Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Log.Error("#2 Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.Fatal("#2 Unfortunately, {Level} == FATAL <{0}>", i);
                            }
                        }
                    }

                    using (var _ = mSyncPoint3.Auto())
                    {
                        Log.Logger = null; // sync point
                    }

                    // classic way
                    // disable stack traces
                    Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

                    if (testType == TestParallelType.Parallel)
                    {
                        using (var _ = mEmitOldWay1.Auto())
                        {
                            Parallel.For(0, n, (int i) =>
                            {
                                Debug.LogFormat("So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i));
                                Debug.LogFormat("<{0}> Info msg.. {1}", i, 42);
                                Debug.LogFormat("Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Debug.LogFormat("Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Debug.LogFormat("Unfortunately, {1} == FATAL <{0}>", i, "FATAL");
                            });
                        }
                    }
                    else
                    {
                        using (var _ = mEmitOldWay1.Auto())
                        {
                            for (int i = 0; i < n; i++)
                            {
                                Debug.LogFormat("So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i));
                                Debug.LogFormat("<{0}> Info msg.. {1}", i, 42);
                                Debug.LogFormat("Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Debug.LogFormat("Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Debug.LogFormat("Unfortunately, {1} == FATAL <{0}>", i, "FATAL");
                            }
                        }
                    }

                    if (testType == TestParallelType.Parallel)
                    {
                        using (var _ = mEmitOldWay2.Auto())
                        {
                            Parallel.For(0, n, (int i) =>
                            {
                                Debug.LogFormat("#2 <{0}> Info msg.. {1}", i, 42);
                                Debug.LogFormat("#2 Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Debug.LogFormat("#2 Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Debug.LogFormat("#2 Unfortunately, {1} == FATAL <{0}>", i, "FATAL");
                            });
                        }
                    }
                    else
                    {
                        using (var _ = mEmitOldWay2.Auto())
                        {
                            for (int i = 0; i < n; i++)
                            {
                                Debug.LogFormat("#2 <{0}> Info msg.. {1}", i, 42);
                                Debug.LogFormat("#2 Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Debug.LogFormat("#2 Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Debug.LogFormat("#2 Unfortunately, {1} == FATAL <{0}>", i, "FATAL");
                            }
                        }
                    }
                }).ProfilerMarkers(markerNames)
                    .MeasurementCount(1)
                    .WarmupCount(0)
                    .IterationsPerMeasurement(1)
                    .Run();

                Update();
                UpdateComplete();

                var memS1 = GetStringFromFirstTestSink(log1);
                var memS2 = GetStringFromFirstTestSink(log2);

                LoggerManager.DeleteAllLoggers();
                var s1 = tmpFile1.ReadAllLines();
                var s2 = tmpFile2.ReadAllLines();

                ValidateResults(s1, s2, memS1, memS2, n, timestampsSorted: true, null);
            }
            finally
            {
                Application.SetStackTraceLogType(LogType.Log, oldStackTrace);
                LoggerManager.DeleteAllLoggers();
            }

            LoggerManager.AssertNoLocks();
        }

        [Test, Performance]
        [TestCase(TestParallelType.SingleThreaded)]
        [TestCase(TestParallelType.Parallel)]
        public void Stress2(TestParallelType testType)
        {
            LoggerManager.AssertNoLocks();
            TimeStampWrapper.SetHandlerForTimestamp(RawGetTimestampHandlerAsCounter, true);

            var mMainName = $"{testType} Stress test";

            var mEmit1Name = $"[{testType}] Stress test. Emitting Logs 1";
            var mSyncPoint1Name = $"[{testType}] Stress test. Setting new Logger - sync point 1";
            var mCreateLogger1Name = $"[{testType}] Stress test. Create logger 1";
            var mCreateLogger2Name = $"[{testType}] Stress test. Create logger 2";


            string[] markerNames = {mMainName, mEmit1Name, mSyncPoint1Name, mCreateLogger1Name, mCreateLogger2Name};

            var mEmit1 = new ProfilerMarker(mEmit1Name);
            var mSyncPoint1 = new ProfilerMarker(mSyncPoint1Name);
            var mCreateLogger1 = new ProfilerMarker(mCreateLogger1Name);
            var mCreateLogger2 = new ProfilerMarker(mCreateLogger2Name);

            using var tmpFile1 = new TempFileGenerator();
            using var tmpFile1_copy = new TempFileGenerator();
            using var tmpFile2 = new TempFileGenerator();

            const int n = 2000;

            try
            {
                Log.Logger = null; // sync point

                GC.Collect(0, GCCollectionMode.Forced, true, true);

                Logger log1 = default;
                Logger log2 = default;

                Measure.Method(() => {
                    using var m = new ProfilerMarker(mMainName).Auto();

                    LogMemoryManagerParameters.GetDefaultParameters(out var parameters);
                    parameters.InitialBufferCapacity *= 64;
                    parameters.OverflowBufferSize *= 32;

                    TextLoggerParser.SetOutputHandlerForTimestamp(RawTimestampHandler, true);

                    using (var _ = mSyncPoint1.Auto())
                        Log.Logger = null;


                    using (var _ = mCreateLogger1.Auto())
                    {
                        log1 = new LoggerConfig().MinimumLevel.Debug()
                            .OutputTemplate(TemplateTestPattern1.PatternString)
                            .WriteTo.Console()
                            .WriteTo.File(tmpFile1.FilePath)
                            .WriteTo.File(tmpFile1_copy.FilePath)
                            .WriteTo.StringLogger()
                            .CreateLogger(parameters);
                    }

                    using (var _ = mCreateLogger2.Auto())
                    {
                        log2 = new LoggerConfig().MinimumLevel.Info()
                            .OutputTemplate(TemplateTestPattern2.PatternString)
                            .WriteTo.Console()
                            .WriteTo.File(tmpFile2.FilePath)
                            .WriteTo.StringLogger()
                            .CreateLogger(parameters);
                    }

                    {
                        using var lock1 = LogControllerScopedLock.Create(log1.Handle);

                        ref var l1 = ref lock1.GetLogController();
                        Assert.IsFalse(l1.HasSinksFor(LogLevel.Verbose));
                        Assert.IsTrue(l1.HasSinksFor(LogLevel.Debug));
                        Assert.IsTrue(l1.HasSinksFor(LogLevel.Info));
                        Assert.IsTrue(l1.HasSinksFor(LogLevel.Warning));
                        Assert.IsTrue(l1.HasSinksFor(LogLevel.Error));
                        Assert.IsTrue(l1.HasSinksFor(LogLevel.Fatal));
                    }

                    {
                        using var lock2 = LogControllerScopedLock.Create(log2.Handle);

                        ref var l2 = ref lock2.GetLogController();
                        Assert.IsFalse(l2.HasSinksFor(LogLevel.Verbose));
                        Assert.IsFalse(l2.HasSinksFor(LogLevel.Debug));
                        Assert.IsTrue(l2.HasSinksFor(LogLevel.Info));
                        Assert.IsTrue(l2.HasSinksFor(LogLevel.Warning));
                        Assert.IsTrue(l2.HasSinksFor(LogLevel.Error));
                        Assert.IsTrue(l2.HasSinksFor(LogLevel.Fatal));
                    }

                    if (testType == TestParallelType.Parallel)
                    {
                        using (var _ = mEmit1.Auto())
                        {
                            Parallel.For(0, n, (int i) =>
                            {
                                Log.To(log1).Verbose("Hi! <{0}> this is {Level} message. Привет, {1}!", i, "%Username%");            // ignored
                                Log.To(log2.Handle).Verbose("#2 Hi! <{0}> this is {Level} message. Привет, {1}!", i, "%Username%"); // ignored
                                Log.To(log1).Debug("So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.To(log2.Handle).Debug("#2 So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i)); // ignored
                                Log.To(log1.Handle).Info("<{0}> Info msg.. {1}", i, 42);
                                Log.To(log2).Info("#2 <{0}> Info msg.. {1}", i, 42);
                                Log.To(log1).Warning("Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Log.To(log2.Handle).Warning("#2 Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Log.To(log1.Handle).Error("Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.To(log2).Error("#2 Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.To(log1).Fatal("Unfortunately, {Level} == FATAL <{0}>", i);
                                Log.To(log2.Handle).Fatal("#2 Unfortunately, {Level} == FATAL <{0}>", i);
                            });
                        }
                    }
                    else
                    {
                        using (var _ = mEmit1.Auto())
                        {
                            for (int i = 0; i < n; i++)
                            {
                                Log.To(log1).Verbose("Hi! <{0}> this is {Level} message. Привет, {1}!", i, "%Username%"); // ignored
                                Log.To(log2).Verbose("#2 Hi! <{0}> this is {Level} message. Привет, {1}!", i, "%Username%");         // ignored
                                Log.To(log1.Handle).Debug("So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.To(log2.Handle).Debug("#2 So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i)); // ignored
                                Log.To(log1.Handle).Info("<{0}> Info msg.. {1}", i, 42);
                                Log.To(log2).Info("#2 <{0}> Info msg.. {1}", i, 42);
                                Log.To(log1).Warning("Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Log.To(log2).Warning("#2 Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                                Log.To(log1.Handle).Error("Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.To(log2).Error("#2 Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                                Log.To(log1.Handle).Fatal("Unfortunately, {Level} == FATAL <{0}>", i);
                                Log.To(log2.Handle).Fatal("#2 Unfortunately, {Level} == FATAL <{0}>", i);
                            }
                        }
                    }

                    {
                        using var lock1 = LogControllerScopedLock.Create(log1.Handle);
                        ref var l1 = ref lock1.GetLogController();
                        Assert.AreEqual(n * 5, l1.LogDispatched(), "LogDispatched for log1 is wrong");
                    }

                    {
                        using var lock2 = LogControllerScopedLock.Create(log2.Handle);
                        ref var l2 = ref lock2.GetLogController();
                        Assert.AreEqual(n * 4, l2.LogDispatched(), "LogDispatched for log2 is wrong");
                    }

                    Update();
                    UpdateComplete();

                    {
                        using var lock1 = LogControllerScopedLock.Create(log1.Handle);
                        ref var l1 = ref lock1.GetLogController();
                        Assert.AreEqual(0, l1.LogDispatched(), "LogDispatched for log1 should be empty");
                    }

                    {
                        using var lock2 = LogControllerScopedLock.Create(log2.Handle);
                        ref var l2 = ref lock2.GetLogController();
                        Assert.AreEqual(0, l2.LogDispatched(), "LogDispatched for log2 should be empty");
                    }
                }).ProfilerMarkers(markerNames)
                    .MeasurementCount(1)
                    .WarmupCount(0)
                    .IterationsPerMeasurement(1)
                    .Run();

                var memS1 = GetStringFromFirstTestSink(log1);
                var memS2 = GetStringFromFirstTestSink(log2);

                LoggerManager.DeleteAllLoggers();

                var s1text = tmpFile1.ReadAllText();
                tmpFile1_copy.SameText(s1text);

                var s1 = tmpFile1.ReadAllLines();
                var s2 = tmpFile2.ReadAllLines();

                ValidateResults(s1, s2, memS1, memS2, n, timestampsSorted: true, null);
            }
            finally
            {
                LoggerManager.DeleteAllLoggers();
            }

            LoggerManager.AssertNoLocks();
        }

        private void ValidateResults(string[] s1, string[] s2, string memS1, string memS2, int n, bool timestampsSorted, (int logger1MessagePerIteration, int logger2MessagePerIteration)? expectedIndexToBeOneByOne)
        {
            static int ParseIntInBrackets(string message)
            {
                var m = message.Split('<')[1];
                var intStr = m.Split('>')[0];

                return int.Parse(intStr);
            }

            static void ValidateOrderForIndexInBrackets(Dictionary<int, LogLevel> dictI1, int indxInBrackets, LogLevel firstNotIgnoredLevel, LogLevel level)
            {
                // since we're logging like this:
                //
                // Parallel.For(0, n, (int i) =>
                // {
                //     Log.Verbose("#2 Hi! <{0}> this is {Level} message. Привет, {1}!", i, "%Username%");                                 // ignored
                //     Log.Debug("#2 So this is some debug message <{0}> Some data: {1}!", i, (name: (FixedString32Bytes)"something", num: i)); // ignored
                //     Log.Info("#2 <{0}> Info msg.. {1}", i, 42);
                //     Log.Warning("#2 Warning! <{0}> <{0}> <{0}> <{0}> <{0}>", i);
                //     Log.Error("#2 Error <{0}>!", i, (name: (FixedString32Bytes)"something", num: i));
                //     Log.Fatal("#2 Unfortunately, {Level} == FATAL <{0}>", i);
                // });
                //
                // it is safe to assume LogLevel order within same 'i' aka indxInBrackets

                if (level == firstNotIgnoredLevel)
                {
                    Assert.IsTrue(dictI1.ContainsKey(indxInBrackets) == false, $"{firstNotIgnoredLevel} must be first message for indx <{indxInBrackets}>");
                    dictI1.Add(indxInBrackets, level);
                }
                else
                {
                    Assert.IsTrue(dictI1.ContainsKey(indxInBrackets), $"Expect to see Debug before {level} for indx <{indxInBrackets}>");
                    var currentLevel = dictI1[indxInBrackets];
                    Assert.AreEqual(currentLevel + 1, level, $"Expect to see one level higher than {currentLevel}, but was {level} for indx <{indxInBrackets}>");
                    dictI1[indxInBrackets] = level;
                }
            }

            // to validate order for each <i> chunk, that each next LogLevel is > that previous one
            var dictI1 = new Dictionary<int, LogLevel>();
            var dictI2 = new Dictionary<int, LogLevel>();

            // to count logs by type
            var dict1 = new Dictionary<LogLevel, int>();
            var dict2 = new Dictionary<LogLevel, int>();
            foreach (var lvl in TestLogData.AllLevels)
            {
                dict1[lvl] = 0;
                dict2[lvl] = 0;
            }

            var indx = 0;
            long prevTime = -1;
            foreach (var line1 in s1)
            {
                var m = TemplateTestPattern1.PatternParse(line1);

                var time = m.timestamp;
                var level = m.level;
                var message = m.message;

                dict1[level]++;

                Assert.AreNotEqual(LogLevel.Verbose, level);

                if (timestampsSorted)
                {
                    if (prevTime != -1)
                        Assert.IsTrue(prevTime <= time, $"Timestamps are not sorted: {prevTime}, then {time}");
                    prevTime = time;
                }

                var indxInBrackets = ParseIntInBrackets(message);

                ValidateOrderForIndexInBrackets(dictI1, indxInBrackets, LogLevel.Debug, level);

                if (expectedIndexToBeOneByOne != null)
                {
                    var expectedIndxInBrackets = indx / expectedIndexToBeOneByOne.Value.logger1MessagePerIteration;
                    Assert.AreEqual(expectedIndxInBrackets, indxInBrackets);
                }

                ++indx;
            }

            indx = 0;
            prevTime = -1;
            foreach (var line2 in s2)
            {
                var m = TemplateTestPattern2.PatternParse(line2);

                var time = m.timestamp;
                var level = m.level;
                var message = m.message;

                dict2[level]++;

                Assert.AreNotEqual(LogLevel.Verbose, level);
                Assert.AreNotEqual(LogLevel.Debug, level);

                if (timestampsSorted)
                {
                    if (prevTime != -1)
                        Assert.IsTrue(prevTime <= time, $"Timestamps are not sorted: {prevTime}, then {time}");
                    prevTime = time;
                }

                var indxInBrackets = ParseIntInBrackets(message);

                ValidateOrderForIndexInBrackets(dictI2, indxInBrackets, LogLevel.Info, level);

                if (expectedIndexToBeOneByOne != null)
                {
                    var expectedIndxInBrackets = indx / expectedIndexToBeOneByOne.Value.logger2MessagePerIteration;
                    Assert.AreEqual(expectedIndxInBrackets, indxInBrackets);
                }

                ++indx;
            }

            Assert.AreEqual(0, dict1[LogLevel.Verbose]);
            Assert.AreEqual(n, dict1[LogLevel.Debug]);
            Assert.AreEqual(n, dict1[LogLevel.Info]);
            Assert.AreEqual(n, dict1[LogLevel.Warning]);
            Assert.AreEqual(n, dict1[LogLevel.Error]);
            Assert.AreEqual(n, dict1[LogLevel.Fatal]);

            Assert.AreEqual(0, dict2[LogLevel.Verbose]);
            Assert.AreEqual(0, dict2[LogLevel.Debug]);
            Assert.AreEqual(n, dict2[LogLevel.Info]);
            Assert.AreEqual(n, dict2[LogLevel.Warning]);
            Assert.AreEqual(n, dict2[LogLevel.Error]);
            Assert.AreEqual(n, dict2[LogLevel.Fatal]);

            Assert.AreEqual(n * (TestLogData.AllLevels.Length - 1), s1.Length);
            Assert.AreEqual(n * (TestLogData.AllLevels.Length - 2), s2.Length); // INFO is default

            if (string.IsNullOrEmpty(memS1) == false)
            {
                var ms1 = memS1.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
                Assert.IsTrue(s1.SequenceEqual(ms1));
            }

            if (string.IsNullOrEmpty(memS2) == false)
            {
                var ms2 = memS2.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
                Assert.IsTrue(s2.SequenceEqual(ms2));
            }
        }
    }
}
#endif
