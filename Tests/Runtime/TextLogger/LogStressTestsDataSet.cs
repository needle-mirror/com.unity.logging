#if !UNITY_DOTSRUNTIME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
using Unity.PerformanceTesting;
using Unity.Profiling;
using Random = UnityEngine.Random;

#pragma warning disable 8123

namespace Unity.Logging.Tests
{
    [BurstCompile]
    public class LogStressTestsDataSet : LoggingTestFixture
    {
        [Timeout(100000)]
        [Test, Performance]
        [TestCase(LoggingUpdateTypeInTests.Parallel, TestLoggerChange.DontChangeLogger, TestName = "Parallel without changing Loggers. 10 times")]
        [TestCase(LoggingUpdateTypeInTests.Serial, TestLoggerChange.DontChangeLogger, TestName = "Serial without changing Loggers. 10 times")]
        [TestCase(LoggingUpdateTypeInTests.Parallel, TestLoggerChange.ChangeLoggerConfigMidTest, TestName = "Parallel with changing Loggers. 10 times")]
        [TestCase(LoggingUpdateTypeInTests.Serial, TestLoggerChange.ChangeLoggerConfigMidTest, TestName = "Serial with changing Loggers. 10 times")]
        public void Stress3For10Times(LoggingUpdateTypeInTests loggingTypeInTests, TestLoggerChange testLoggerChangeLoggerConfigs)
        {
            // used in Stress3
            var mMainName = StressTest3MarkerName(loggingTypeInTests, testLoggerChangeLoggerConfigs);

            Measure.Method(() =>
            {
                for (var i = 0; i < 10; ++i)
                {
                    Stress3(loggingTypeInTests, testLoggerChangeLoggerConfigs, 1241512);
                }
            }).ProfilerMarkers(mMainName)
                .MeasurementCount(1)
                .WarmupCount(0)
                .IterationsPerMeasurement(1)
                .Run();
        }

        private static string StressTest3MarkerName(LoggingUpdateTypeInTests loggingTypeInTests, TestLoggerChange testLoggerChangeLoggerConfigs)
        {
            return $"{loggingTypeInTests} changeLoggerConfigs={testLoggerChangeLoggerConfigs} Stress3 test";
        }

        public enum TestLoggerChange
        {
            DontChangeLogger,
            ChangeLoggerConfigMidTest
        }

        [Test]
        [Timeout(100000)]
        public void Stress3([Values(LoggingUpdateTypeInTests.Serial, LoggingUpdateTypeInTests.Parallel)] LoggingUpdateTypeInTests loggingTypeInTests,
            [Values(TestLoggerChange.DontChangeLogger, TestLoggerChange.ChangeLoggerConfigMidTest)] TestLoggerChange testLoggerChangeLoggerConfigs,
            [Values(7, 1241512)] int seed)
        {
            LoggerManager.AssertNoLocks();
            Random.InitState(seed);
            TimeStampWrapper.SetHandlerForTimestamp(RawGetTimestampHandlerAsCounter, true);

            var data1 = new TestLogDataSet(32, Allocator.Persistent);
            var data2 = new TestLogDataSet(256, Allocator.Persistent);
            var data3 = new TestLogDataSet(1024, Allocator.Persistent);

            var data4 = new TestLogDataSet(32, Allocator.Persistent);
            var data5 = new TestLogDataSet(256, Allocator.Persistent);
            var data6 = new TestLogDataSet(1024, Allocator.Persistent);

            var data7 = new TestLogDataSet(1024, Allocator.Persistent);
            var data8 = new TestLogDataSet(256, Allocator.Persistent);
            var data9 = new TestLogDataSet(1024, Allocator.Persistent);

            var data10 = new TestLogDataSet(256, Allocator.Persistent);

            var dataSetsThread = new List<TestLogDataSet>
            {
                CreateDataSet(32),
                data2,
                CreateDataSet(32),
                CreateDataSet(32),
                data1,
                data3
            };

            var dataSetsParallelFor = new List<TestLogDataSet>
            {
                data4,
                data5,
                data6
            };

            var dataSetsJobs = new List<TestLogDataSet>
            {
                CreateDataSet(32),
                data7,
                CreateDataSet(32),
                data8,
                CreateDataSet(32),
                data9
            };

            var dataSetsJobs2 = new List<TestLogDataSet>
            {
                data1,
                data3,
                data9
            };

            var allData = new List<TestLogDataSet>(dataSetsThread);
            allData.AddRange(dataSetsParallelFor);
            allData.AddRange(dataSetsJobs);
            allData.Add(data10);
            var total = allData.Sum(d => d.Length);

            var allData2 = new List<TestLogDataSet>(dataSetsJobs2);
            var total2 = allData.Sum(d => d.Length);

            using var tmpFile1 = new TempFileGenerator();
            using var tmpFile2 = new TempFileGenerator();
            try
            {
                Logger log;
                Logger log2;
                {
                    var name = StressTest3MarkerName(loggingTypeInTests, testLoggerChangeLoggerConfigs);
                    using var m = new ProfilerMarker(name).Auto();

                    var tempLogger0 = new List<Logger>
                    {
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger()
                    };

                    var tempLogger1 = new List<Logger>
                    {
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger()
                    };

                    LogMemoryManagerParameters.GetDefaultParameters(out var parameters);
                    parameters.InitialBufferCapacity *= 64;
                    parameters.OverflowBufferSize *= 32;
                    parameters.DispatchQueueSize = total;

                    TextLoggerParser.SetOutputHandlerForTimestamp(RawTimestampHandler, true);
                    log = new LoggerConfig().MinimumLevel.Verbose()
                        .OutputTemplate(TemplateTestPattern3.PatternString)
                        .WriteTo.StringLogger()
                        .WriteTo.JsonFile(tmpFile1.FilePath)
                        .CreateLogger(parameters);
                    Log.Logger = log;

                    parameters.DispatchQueueSize = total2;
                    log2 = new LoggerConfig().MinimumLevel.Verbose()
                        .OutputTemplate(TemplateTestPattern1.PatternString)
                        .WriteTo.JsonFile(tmpFile2.FilePath)
                        .WriteTo.StringLogger()
                        .CreateLogger(parameters);

                    var toDeleteLogger = CreateTempLogger();

                    var tempLogger2 = new List<Logger>
                    {
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger()
                    };

                    var logJobHandle = default(JobHandle);

                    logJobHandle = ScheduleUpdate(loggingTypeInTests, logJobHandle);

                    if (testLoggerChangeLoggerConfigs == TestLoggerChange.ChangeLoggerConfigMidTest)
                    {
                        foreach (var l in tempLogger0)
                            l.Dispose();
                        tempLogger0.Clear();

                        toDeleteLogger.Dispose();
                    }

                    var threads = dataSetsThread.Select(data => data.StartThread()).ToArray();

                    var jobs2Array = dataSetsJobs2.Select(data => data.ScheduleExecute(log2.Handle)).ToArray();

                    logJobHandle = ScheduleUpdate(loggingTypeInTests, logJobHandle);

                    Parallel.For(0, dataSetsParallelFor.Count, i =>
                    {
                        TestLogDataSet.Execute(dataSetsParallelFor[i]);
                    });

                    logJobHandle = ScheduleUpdate(loggingTypeInTests, logJobHandle);

                    var jobsArray = dataSetsJobs.Select(data => data.ScheduleExecute()).ToArray();

                    if (testLoggerChangeLoggerConfigs == TestLoggerChange.ChangeLoggerConfigMidTest)
                    {
                        foreach (var l in tempLogger1)
                            l.Dispose();
                        tempLogger1.Clear();
                    }

                    var jobs = new JobHandle();
                    foreach (var j in jobsArray)
                        jobs = JobHandle.CombineDependencies(jobs, j);
                    foreach (var j in jobs2Array)
                        jobs = JobHandle.CombineDependencies(jobs, j);

                    if (testLoggerChangeLoggerConfigs == TestLoggerChange.ChangeLoggerConfigMidTest)
                    {
                        foreach (var l in tempLogger2)
                            l.Dispose();
                        tempLogger2.Clear();
                    }

                    while (jobs.IsCompleted == false || threads.Any(t => t.IsAlive))
                    {
                        Thread.Sleep(2);

                        logJobHandle = ScheduleUpdate(loggingTypeInTests, logJobHandle);
                    }

                    jobs.Complete();
                    foreach (var t in threads)
                        t.Join();

                    data10.ScheduleExecute().Complete();

                    logJobHandle = ScheduleUpdate(loggingTypeInTests, logJobHandle);
                    logJobHandle = ScheduleUpdate(loggingTypeInTests, logJobHandle);

                    logJobHandle.Complete();
                }

                LoggerManager.FlushAll();

                var memS1 = GetStringFromFirstTestSink(log);
                var memS2 = GetStringFromFirstTestSink(log2);

                LoggerManager.DeleteAllLoggers();


                Parallel.Invoke(() =>
                {
                    var parsed = memS1.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(l => TemplateTestPattern3.PatternParse(l)).ToArray();
                    foreach (var data in allData)
                        data.ValidateLines(parsed);
                }, () =>
                {
                    var parsed2 = memS2.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(l => TemplateTestPattern1.PatternParse(l)).ToArray();
                    foreach (var data in allData2)
                        data.ValidateLines(parsed2);
                }, () =>
                {
                    var json = tmpFile1.ReadAllText();
                    var elements = JsonConvert.DeserializeObject<JsonEntryElement[]>(json);
                    foreach (var data in allData)
                        data.ValidateJson(elements);
                },
                () =>
                {
                    var json2 = tmpFile2.ReadAllText();
                    var elements2 = JsonConvert.DeserializeObject<JsonEntryElement[]>(json2);
                    foreach (var data in allData2)
                        data.ValidateJson(elements2);
                });
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
            LoggerManager.AssertNoLocks();
        }


        [Test]
        [Timeout(100000)]
        public void StressLogDuringUpdate([Values(TestLoggerChange.DontChangeLogger, TestLoggerChange.ChangeLoggerConfigMidTest)] TestLoggerChange testLoggerChangeLoggerConfigs,
            [Values(7, 1241512)] int seed)
        {
            LoggerManager.AssertNoLocks();
            Random.InitState(seed);
            TimeStampWrapper.SetHandlerForTimestamp(RawGetTimestampHandlerAsCounter, true);

            var data1 = new TestLogDataSet(32, Allocator.Persistent);
            var data2 = new TestLogDataSet(256, Allocator.Persistent);
            var data3 = new TestLogDataSet(1024, Allocator.Persistent);

            var data4 = new TestLogDataSet(32, Allocator.Persistent);
            var data5 = new TestLogDataSet(256, Allocator.Persistent);
            var data6 = new TestLogDataSet(1024, Allocator.Persistent);

            var data7 = new TestLogDataSet(1024, Allocator.Persistent);
            var data8 = new TestLogDataSet(256, Allocator.Persistent);
            var data9 = new TestLogDataSet(1024, Allocator.Persistent);

            var data10 = new TestLogDataSet(256, Allocator.Persistent);

            var dataSetsJobs = new List<TestLogDataSet>
            {
                data1,
                CreateDataSet(32),
                data2,
                data3,
                CreateDataSet(32),
                CreateDataSet(32),
                data4,
                data5,
                data6,
                CreateDataSet(32),
                data7,
                CreateDataSet(32),
                data8,
                CreateDataSet(32),
                data9
            };

            var dataSetsJobs2 = new List<TestLogDataSet>
            {
                data1,
                data3,
                data9
            };

            var allData = new List<TestLogDataSet>(dataSetsJobs);
            allData.Add(data10);
            var total = allData.Sum(d => d.Length);

            var allData2 = new List<TestLogDataSet>(dataSetsJobs2);
            var total2 = allData.Sum(d => d.Length);

            using var tmpFile1 = new TempFileGenerator();
            using var tmpFile2 = new TempFileGenerator();
            try
            {
                Logger log;
                Logger log2;
                {
                    var name = $"ChangeLoggerConfigs={testLoggerChangeLoggerConfigs} StressLogDuringUpdate test";;
                    using var m = new ProfilerMarker(name).Auto();

                    var tempLogger0 = new List<Logger>
                    {
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger()
                    };

                    var tempLogger1 = new List<Logger>
                    {
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger()
                    };

                    LogMemoryManagerParameters.GetDefaultParameters(out var parameters);
                    parameters.InitialBufferCapacity *= 64;
                    parameters.OverflowBufferSize *= 32;
                    parameters.DispatchQueueSize = total;

                    TextLoggerParser.SetOutputHandlerForTimestamp(RawTimestampHandler, true);
                    log = new LoggerConfig().MinimumLevel.Verbose()
                        .OutputTemplate(TemplateTestPattern3.PatternString)
                        .WriteTo.StringLogger()
                        .WriteTo.JsonFile(tmpFile1.FilePath)
                        .CreateLogger(parameters);
                    Log.Logger = log;

                    parameters.DispatchQueueSize = total2;
                    log2 = new LoggerConfig().MinimumLevel.Verbose()
                        .OutputTemplate(TemplateTestPattern1.PatternString)
                        .WriteTo.JsonFile(tmpFile2.FilePath)
                        .WriteTo.StringLogger()
                        .CreateLogger(parameters);

                    var toDeleteLogger = CreateTempLogger();

                    var tempLogger2 = new List<Logger>
                    {
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger(),
                        CreateTempLogger()
                    };

                    var logJobHandle = default(JobHandle);

                    for (int i = 0; i < 64; i++)
                        logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);

                    if (testLoggerChangeLoggerConfigs == TestLoggerChange.ChangeLoggerConfigMidTest)
                    {
                        foreach (var l in tempLogger0)
                        {
                            logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);
                            l.Dispose();
                        }
                        tempLogger0.Clear();
                        toDeleteLogger.Dispose();
                    }

                    for (int i = 0; i < 64; i++)
                        logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);

                    var jobs2Array = dataSetsJobs2.Select(data => data.ScheduleExecute(log2.Handle)).ToArray();

                    for (int i = 0; i < 64; i++)
                        logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);

                    var jobsArray = dataSetsJobs.Select(data => data.ScheduleExecute()).ToArray();

                    for (int i = 0; i < 64; i++)
                        logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);

                    if (testLoggerChangeLoggerConfigs == TestLoggerChange.ChangeLoggerConfigMidTest)
                    {
                        foreach (var l in tempLogger1)
                        {
                            logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);
                            l.Dispose();
                        }
                        tempLogger1.Clear();
                    }

                    var jobs = new JobHandle();
                    foreach (var j in jobsArray)
                        jobs = JobHandle.CombineDependencies(jobs, j);
                    foreach (var j in jobs2Array)
                        jobs = JobHandle.CombineDependencies(jobs, j);

                    if (testLoggerChangeLoggerConfigs == TestLoggerChange.ChangeLoggerConfigMidTest)
                    {
                        foreach (var l in tempLogger2)
                        {
                            logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);
                            l.Dispose();
                        }

                        tempLogger2.Clear();
                    }

                    while (jobs.IsCompleted == false)
                    {
                        Thread.Sleep(2);

                        logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);
                    }

                    jobs.Complete();

                    for (int i = 0; i < 64; i++)
                        logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);

                    var handle10 = data10.ScheduleExecute();

                    for (int i = 0; i < 64; i++)
                        logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);

                    handle10.Complete();

                    logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);
                    logJobHandle = ScheduleUpdate(LoggingUpdateTypeInTests.Parallel, logJobHandle);

                    logJobHandle.Complete();
                }

                LoggerManager.FlushAll();

                var memS1 = GetStringFromFirstTestSink(log);
                var memS2 = GetStringFromFirstTestSink(log2);

                LoggerManager.DeleteAllLoggers();


                Parallel.Invoke(() =>
                {
                    var parsed = memS1.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(l => TemplateTestPattern3.PatternParse(l)).ToArray();
                    foreach (var data in allData)
                        data.ValidateLines(parsed);
                }, () =>
                {
                    var parsed2 = memS2.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(l => TemplateTestPattern1.PatternParse(l)).ToArray();
                    foreach (var data in allData2)
                        data.ValidateLines(parsed2);
                }, () =>
                {
                    var json = tmpFile1.ReadAllText();
                    var elements = JsonConvert.DeserializeObject<JsonEntryElement[]>(json);
                    foreach (var data in allData)
                        data.ValidateJson(elements);
                },
                () =>
                {
                    var json2 = tmpFile2.ReadAllText();
                    var elements2 = JsonConvert.DeserializeObject<JsonEntryElement[]>(json2);
                    foreach (var data in allData2)
                        data.ValidateJson(elements2);
                });
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
            LoggerManager.AssertNoLocks();
        }
    }
}
#endif
