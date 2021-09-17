using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
using UnityEngine.TestTools;

namespace Unity.Logging.Tests
{
    public class UnityDebugLogSinkTest : LoggingTestFixture
    {
        [Test]
        public void DebugLogTest()
        {
            UnityEngine.Random.InitState(42);
            var data1 = new TestLogDataSet(256, Allocator.Persistent);

            try
            {
                TimeStampWrapper.SetHandlerForTimestamp(RawGetTimestampHandlerAsCounter, true);
                TextLoggerParser.SetOutputHandlerForTimestamp(RawTimestampHandler, true);
                var log = new LoggerConfig().MinimumLevel.Verbose()
                                            .OutputTemplate(TemplateTestPattern2.PatternString)
                                            .WriteTo.UnityDebugLog()
                                            .CreateLogger();

                LoggerManager.ScheduleUpdateLoggers();

                data1.LogExpect();

                var logJob = data1.ScheduleExecute(log.Handle);
                LoggerManager.ScheduleUpdateLoggers();

                logJob.Complete();

                LoggerManager.FlushAll();

                LoggerManager.DeleteAllLoggers();
            }
            finally
            {
                try
                {
                    data1.Dispose();
                }
                catch
                {
                    // Ignore. We need to dispose all the Native containers
                }
            }
        }
    }
}
