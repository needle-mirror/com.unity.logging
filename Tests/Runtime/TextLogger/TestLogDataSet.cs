using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Unity.Logging.Tests
{
    public class TestLogDataSet : IDisposable
    {
        [BurstCompile]
        struct LogTestDataJob : IJob
        {
            [DeallocateOnJobCompletion]
            [ReadOnly] public NativeArray<TestLogData> logTestData;

            public void Execute()
            {
                for (var i = 0; i < logTestData.Length; i++)
                {
                    var data = logTestData[i];
                    ExecuteData(data);
                }
            }

            public static void ExecuteData(in TestLogData data)
            {
                data.CallNewLog();
            }
        }

        [BurstCompile]
        struct LogTestDataJobWithHandle : IJob
        {
            [DeallocateOnJobCompletion]
            [ReadOnly] public NativeArray<TestLogData> logTestData;
            [ReadOnly] public LoggerHandle handle;

            public void Execute()
            {
                for (var i = 0; i < logTestData.Length; i++)
                {
                    var data = logTestData[i];
                    ExecuteData(data, handle);
                }
            }

            public static void ExecuteData(in TestLogData data, in LoggerHandle logHandle)
            {
                data.CallNewLog(logHandle);
            }
        }

        public static void Execute(object objLogTestData)
        {
            var logTestData = (TestLogDataSet)objLogTestData;

            for (var i = 0; i < logTestData.data.Length; i++)
            {
                var data = logTestData.data[i];
                LogTestDataJob.ExecuteData(data);
            }
        }

        public void LogExpect()
        {
            var n = data.Length;
            for (int i = 0; i < n; i++)
            {
                var elem = data[i];
                UnityEngine.TestTools.LogAssert.Expect(elem.ToLogType(), new Regex(Regex.Escape(elem.ToString())));
            }
        }

        public readonly string UniquePrefix;

        private readonly TestLogData[] data;
        private bool wasValidated;

        private static int id = 1;

        public readonly int Length;

        public TestLogDataSet(int n, Allocator allocator)
        {
            Length = n;
            wasValidated = false;
            data = new TestLogData[n];
            UniquePrefix = Interlocked.Increment(ref id).ToString();

            for (int i = 0; i < n; i++)
            {
                var logDataType = TestLogData.AllLogDataTypes[Random.Range(0, TestLogData.AllLogDataTypes.Length)];

                var complexType = default(TestLogData.ComplexType);

                var integer = 0;

                if (logDataType == TestLogData.LogDataType.MessageAndComplexType)
                {
                    complexType = TestLogData.ComplexType.Random();
                }
                else if (logDataType == TestLogData.LogDataType.MessageAndInt)
                {
                    integer = Random.Range(-10000, 10000);
                }

                Assert.IsTrue(UnsafeUtility.IsBlittable<TestLogData>(), "TestLogData must be blittable");

                data[i] = new TestLogData
                {
                    level = TestLogData.AllLevels[Random.Range(0, TestLogData.AllLevels.Length)],
                    dataType = logDataType,
                    integer = integer,
                    messageWithPrefix = UniquePrefix + GenerateRandomMessage(i, n, logDataType)
                };
            }
        }

        public void Set(int i, string customString)
        {
            data[i] = new TestLogData
            {
                level = TestLogData.AllLevels[Random.Range(0, TestLogData.AllLevels.Length)],
                dataType = TestLogData.LogDataType.MessageAndComplexType,
                integer = Random.Range(-10000, 10000),
                messageWithPrefix = UniquePrefix + customString
            };
        }

        public TestLogData GetElement(int i)
        {
            return data[i];
        }

        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        static string GenerateRandom(int length) => new string(Enumerable.Repeat(chars, length).Select(s => s[Random.Range(0, s.Length)]).ToArray());

        private static FixedString512Bytes GenerateRandomMessage(int i, int n, TestLogData.LogDataType logDataType)
        {
            var prefix = $"[{(i+1):0000000} out of {n:0000000}]_";
            switch (logDataType)
            {
                case TestLogData.LogDataType.JustMessage:
                    return prefix + GenerateRandom(180);
                case TestLogData.LogDataType.MessageAndInt:
                    return prefix + GenerateRandom(80) + "{0}" + GenerateRandom(50);
                case TestLogData.LogDataType.MessageAndComplexType:
                    return prefix + GenerateRandom(80) + "{0}" + GenerateRandom(50) + "{0}";
            }

            throw new Exception("forgot to add new element to the switch?");
        }

        public void Dispose()
        {
            Assert.IsTrue(wasValidated, $"Forgot to call Validate? {UniquePrefix}");
        }

        public JobHandle ScheduleExecute(JobHandle inputDeps = default)
        {
            return new LogTestDataJob
            {
                logTestData = new NativeArray<TestLogData>(data, Allocator.TempJob)
            }.Schedule(inputDeps);
        }

        public JobHandle ScheduleExecute(LoggerHandle handle, JobHandle inputDeps = default)
        {
            return new LogTestDataJobWithHandle
            {
                handle = handle,
                logTestData = new NativeArray<TestLogData>(data, Allocator.TempJob)
            }.Schedule(inputDeps);
        }

        public void RunExecute()
        {
            Execute(this);
        }

        public void ValidateJson(JsonEntryElement[] container)
        {
            var uniquePrefix = UniquePrefix;
            var lines = container.Where(s => s.Message.StartsWith(uniquePrefix)).ToArray();

            var n = data.Length;
            Assert.AreEqual(n, lines.Length, $"Validate json failed - there were wrong number of log entities. data.Length = {data.Length}. Lines count = {lines.Length}");

            long prevTimestamp = -1;
            var m = lines.Length;
            for (var i = 0; i < m; i++)
            {
                var obj = lines[i];
                var timestamp = (long)obj.Timestamp;
                Assert.IsTrue(prevTimestamp < timestamp, "Validate json failed - Timestamps are not sorted");
                prevTimestamp = timestamp;
            }

            for (var i = 0; i < m; i++)
            {
                data[i].Validate(lines[i]);
            }

            wasValidated = true;
        }

        public void ValidateLines(IEnumerable<TemplateParsedMessage> linesAll)
        {
            var uniquePrefix = UniquePrefix;
            var lines = linesAll.Where(s => s.message.StartsWith(uniquePrefix)).ToArray();

            var n = data.Length;
            Assert.AreEqual(n, lines.Length, "Validate string failed - there were wrong number of log entities");

            long prevTimestamp = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var timestamp = data[i].Validate(line);

                Assert.IsTrue(prevTimestamp <= timestamp, "Validate string failed - Timestamps are not sorted");
                prevTimestamp = timestamp;
            }

            wasValidated = true;
        }

        public Thread StartThread()
        {
            var thread1 = new Thread(Execute);
            thread1.Start(this);
            return thread1;
        }
    }
}
