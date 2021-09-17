using System;
using System.Threading;
using AOT;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Collections;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
using UnityEngine;

namespace Unity.Logging.Tests
{
    [TestFixture]
    public class TimestampTests : LoggingTestFixture
    {
        private static DateTime s_MockDateTime;

        void ValidateConversion(DateTime dateTime)
        {
            var nano = TimeStampWrapper.DateTimeTicksToNanosec(dateTime.Ticks);
            var date = new DateTime(TimeStampWrapper.NanosecToDateTimeTicks(nano));
            Assert.AreEqual(dateTime, date, "ValidateConversion failed");
        }

        [Test]
        public void TimestampManagerTest()
        {
            var sleepMs = 100;

            TimeStampManagerManaged.Initialize();
            var time1 = TimeStampManagerManaged.GetTimeStamp();
            var utcNow1 = DateTime.UtcNow;

            Thread.Sleep(sleepMs + 16);

            var time2 = TimeStampManagerManaged.GetTimeStamp();
            var utcNow2 = DateTime.UtcNow;

            var dateTime1 = new DateTime(TimeStampWrapper.NanosecToDateTimeTicks(time1));
            var dateTime2 = new DateTime(TimeStampWrapper.NanosecToDateTimeTicks(time2));

            var format = "yyyy/MM/dd HH:mm:ss.fff";
            Debug.Log(dateTime1.ToString(format));
            Debug.Log(utcNow1.ToString(format));

            Debug.Log(dateTime2.ToString(format));
            Debug.Log(utcNow2.ToString(format));

            var diff1 = utcNow1.Subtract(dateTime1);
            var diff2 = utcNow2.Subtract(dateTime2);

            Debug.Log($"Diff1: {diff1.TotalSeconds} s");
            Debug.Log($"Diff2: {diff2.TotalSeconds} s");

            Assert.IsTrue(diff1.TotalSeconds < 1);
            Assert.IsTrue(diff2.TotalSeconds < 1);

            var diffDateTime = dateTime2.Subtract(dateTime1);
            var diffManager = utcNow2.Subtract(utcNow1);

            Debug.Log($"Diff between datetime: {diffDateTime.TotalMilliseconds} ms");
            Debug.Log($"Diff between manager: {diffManager.TotalMilliseconds} ms");

            Assert.IsTrue(diffDateTime.TotalMilliseconds > sleepMs, $"We slept for {sleepMs} between time, why diff is less {diffDateTime.TotalMilliseconds} than that?");
            Assert.IsTrue(diffManager.TotalMilliseconds > sleepMs, $"We slept for {sleepMs} between time, why diff is less {diffManager.TotalMilliseconds} than that?");

            var diffMs = Math.Abs(diffManager.TotalMilliseconds - diffDateTime.Milliseconds);

            Debug.Log($"Diff between them: {diffMs} ms");

            Assert.IsTrue(diffMs < 20.0, "Difference should be almost 0 (max 20 msec), but was " + diffMs);
        }

#if USE_BASELIB

        [Test]
        public void TimestampBaselibTest()
        {
            var sleepMs = 100;

            TimeStampManagerBaselib.Initialize();
            var time1 = TimeStampManagerBaselib.GetTimeStamp();
            var utcNow1 = DateTime.UtcNow;

            Thread.Sleep(sleepMs + 16);

            var time2 = TimeStampManagerBaselib.GetTimeStamp();
            var utcNow2 = DateTime.UtcNow;

            var dateTime1 = new DateTime(TimeStampWrapper.NanosecToDateTimeTicks(time1));
            var dateTime2 = new DateTime(TimeStampWrapper.NanosecToDateTimeTicks(time2));

            var format = "yyyy/MM/dd HH:mm:ss.fff";
            Debug.Log(dateTime1.ToString(format));
            Debug.Log(utcNow1.ToString(format));

            Debug.Log(dateTime2.ToString(format));
            Debug.Log(utcNow2.ToString(format));

            var diff1 = utcNow1.Subtract(dateTime1);
            var diff2 = utcNow2.Subtract(dateTime2);

            Debug.Log($"Diff1: {diff1.TotalSeconds} s");
            Debug.Log($"Diff2: {diff2.TotalSeconds} s");

            Assert.IsTrue(diff1.TotalSeconds < 1);
            Assert.IsTrue(diff2.TotalSeconds < 1);

            var diffDateTime = dateTime2.Subtract(dateTime1);
            var diffManager = utcNow2.Subtract(utcNow1);

            Debug.Log($"Diff between datetime: {diffDateTime.TotalMilliseconds} ms");
            Debug.Log($"Diff between manager: {diffManager.TotalMilliseconds} ms");

            Assert.IsTrue(diffDateTime.TotalMilliseconds > sleepMs, $"We slept for {sleepMs} between time, why diff is less {diffDateTime.TotalMilliseconds} than that?");
            Assert.IsTrue(diffManager.TotalMilliseconds > sleepMs, $"We slept for {sleepMs} between time, why diff is less {diffManager.TotalMilliseconds} than that?");

            var diffMs = Math.Abs(diffManager.TotalMilliseconds - diffDateTime.Milliseconds);

            Debug.Log($"Diff between them: {diffMs} ms");

            Assert.IsTrue(diffMs < 20.0, "Difference should be almost 0 (max 20 msec), but was " + diffMs);
        }
#endif

        [Test]
        public void TimestampTest()
        {
            Log.Logger = new LoggerConfig().WriteTo.StringLogger()
                                           .CreateLogger();

            Log.Info("1");
            var utcNow1 = DateTime.UtcNow;
            Log.Info("2");
            var utcNow2 = DateTime.UtcNow;
            Log.Info("3");

            Thread.Sleep(20);

            Log.Info("4");
            var utcNow3 = DateTime.UtcNow;
            Log.Info("5");
            var utcNow4 = DateTime.UtcNow;
            Log.Info("6");

            Update();
            UpdateComplete();

            var time = GetStringFromFirstTestSink(Log.Logger);

            LoggerManager.DeleteAllLoggers();

            Debug.Log(time);
            Debug.Log("DateTime.UtcNow = " + utcNow1.ToString(TimeStampWrapper.TimestampFormat));
            Debug.Log("DateTime.UtcNow = " + utcNow2.ToString(TimeStampWrapper.TimestampFormat));
            Debug.Log("DateTime.UtcNow = " + utcNow3.ToString(TimeStampWrapper.TimestampFormat));
            Debug.Log("DateTime.UtcNow = " + utcNow4.ToString(TimeStampWrapper.TimestampFormat));
        }

        [Test]
        public void TimestampFormatTest()
        {
            s_MockDateTime = DateTime.UtcNow;

            {
                // 9/23/1907 12:12:43 AM --- 4/10/2492 11:47:16 PM
                var minDate = new DateTime(TimeStampWrapper.NanosecToDateTimeTicks(long.MinValue));
                var maxDate = new DateTime(TimeStampWrapper.NanosecToDateTimeTicks(long.MaxValue));
                UnityEngine.Debug.Log($"{minDate} --- {maxDate}");

                ValidateConversion(new DateTime(1970, 1, 1));
                ValidateConversion(s_MockDateTime);
                ValidateConversion(new DateTime(2262, 1, 1));
            }


            TimeStampWrapper.TimestampFormat = "yyyy/MM/dd HH:mm:ss.fff";
            TimeStampWrapper.SetHandlerForTimestamp(GetTimestampCustom, false);

            using var tmpFile1 = new TempFileGenerator("txt");
            using var tmpFile2 = new TempFileGenerator("json");

            Log.Logger = new LoggerConfig().WriteTo.File(tmpFile1.FilePath)
                                           .WriteTo.JsonFile(tmpFile2.FilePath)
                                           .CreateLogger();

            Log.Info("1 {0} end1", (1, (FixedString32Bytes)"adawdawd"));
            Log.Info("2 {0} end2", (1, 235235, (32, (FixedString32Bytes)"hey")));

            LoggerManager.DeleteAllLoggers();

            var a = tmpFile1.ReadAllText();
            var x = tmpFile2.ReadAllText();

            var expectedDateTime = s_MockDateTime.ToString(TimeStampWrapper.TimestampFormat);

            var jsonEscaped = JsonConvert.ToString(expectedDateTime);

            Assert.IsTrue(a.Contains(expectedDateTime), $"<{a}> doesn't contain <{expectedDateTime}>");
            Assert.IsTrue(x.Contains(jsonEscaped), $"<{x}> doesn't contain <{jsonEscaped}>");
        }

        [MonoPInvokeCallback(typeof(TimeStampWrapper.CustomGetTimestampHandler))]
        private static long GetTimestampCustom()
        {
            return TimeStampWrapper.DateTimeTicksToNanosec(s_MockDateTime.Ticks);
        }
    }
}
