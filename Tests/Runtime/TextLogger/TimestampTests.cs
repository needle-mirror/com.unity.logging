using System;
using AOT;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Collections;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;

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
