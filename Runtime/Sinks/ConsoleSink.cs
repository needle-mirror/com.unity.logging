using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Logging;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;

[assembly: RegisterGenericJobType(typeof(SinkJob<ConsoleSinkLogger>))]

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Console sink logic
    /// </summary>
    public struct ConsoleSinkLogger : ILogger
    {
        /// <summary>
        /// Converts log message into string and writes to the console
        /// </summary>
        /// <param name="logEvent">Log message</param>
        /// <param name="outTemplate">Template for the message</param>
        /// <param name="memoryManager">Memory manager to get the binary data from</param>
        public void OnLogMessage(in LogMessage logEvent, in FixedString512Bytes outTemplate, ref LogMemoryManager memoryManager)
        {
            var message = TextLoggerParser.ParseMessageTemplate(logEvent, outTemplate, ref memoryManager);
            if (message.IsCreated)
            {
                try
                {
                    unsafe
                    {
                        var data = message.GetUnsafePtr();
                        var length = message.Length;
                        byte newLine = 1;
                        Console.Write(data, length, newLine);
                    }
                }
                finally
                {
                    message.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Console Sink System that defines how to update it
    /// </summary>
    public class ConsoleSinkSystem : SinkSystemBase<ConsoleSinkLogger>
    {
        public override JobHandle ScheduleUpdate(LogControllerScopedLock @lock, JobHandle dependency)
        {
            dependency = Console.ScheduleBeginBatch(dependency);
            dependency = base.ScheduleUpdate(@lock, dependency);
            return Console.ScheduleEndBatch(dependency);
        }
    }

    public static class ConsoleSinkSystemExt
    {
        public static LoggerConfig Console(this LoggerWriterConfig writeTo, bool captureStackTrace = false, LogLevel? minLevel = null, FixedString512Bytes? outputTemplate = null)
        {
            return writeTo.AddSinkConfig(new SinkConfiguration<ConsoleSinkSystem>
            {
                CaptureStackTraces = captureStackTrace,
                MinLevelOverride = minLevel,
                OutputTemplateOverride = outputTemplate
            });
        }
    }
}
