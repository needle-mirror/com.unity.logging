using System;
using Unity.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Extension class for LoggerWriterConfig .StdOut
    /// </summary>
    public static class StdOutSinkSystemExt
    {
        /// <summary>
        /// Write logs to the standard output
        /// </summary>
        /// <param name="writeTo">Logger config</param>
        /// <param name="formatter">Formatter that should be used by this sink. Text is default</param>
        /// <param name="captureStackTrace">True if stack traces should be captured</param>
        /// <param name="minLevel">Minimal level of logs for this particular sink. Null if common level should be used</param>
        /// <param name="outputTemplate">Output message template for this particular sink. Null if common template should be used</param>
        /// <returns>Logger config</returns>
        public static LoggerConfig StdOut(this LoggerWriterConfig writeTo,
                                           FormatterStruct formatter = default,
                                           bool? captureStackTrace = null,
                                           LogLevel? minLevel = null,
                                           FixedString512Bytes? outputTemplate = null)
        {
            if (formatter.IsCreated == false)
                formatter = LogFormatterText.Formatter;

            return writeTo.AddSinkConfig(new StdOutSinkSystem.Configuration(writeTo, formatter, captureStackTrace, minLevel, outputTemplate));
        }
    }

    /// <summary>
    /// Standard Output Sink System that defines how to update it
    /// </summary>
    [BurstCompile]
    public class StdOutSinkSystem : SinkSystemBase
    {
        /// <summary>
        /// Configuration for StdOut sink
        /// </summary>
        public class Configuration : SinkConfiguration
        {
            /// <summary>
            /// Creates the StdOutSinkSystem
            /// </summary>
            /// <param name="logger">Logger that owns sink</param>
            /// <returns>SinkSystemBase</returns>
            public override SinkSystemBase CreateSinkInstance(Logger logger) => CreateAndInitializeSinkInstance<StdOutSinkSystem>(logger, this);

            /// <summary>
            /// Constructor for the configuration
            /// </summary>
            /// <param name="writeTo">Logger config</param>
            /// <param name="formatter">Formatter that should be used by this sink. Text is default</param>
            /// <param name="captureStackTraceOverride">True if stack traces should be captured. Null if default</param>
            /// <param name="minLevelOverride">Minimal level of logs for this particular sink. Null if common level should be used</param>
            /// <param name="outputTemplateOverride">Output message template for this particular sink. Null if common template should be used</param>
            public Configuration(LoggerWriterConfig writeTo, FormatterStruct formatter,
                                 bool? captureStackTraceOverride = null, LogLevel? minLevelOverride = null, FixedString512Bytes? outputTemplateOverride = null)
                : base(writeTo, formatter, captureStackTraceOverride, minLevelOverride, outputTemplateOverride)
            {}
        }

        /// <summary>
        /// Creates <see cref="LogController.SinkStruct"/>
        /// </summary>
        /// <returns>SinkStruct</returns>
        public override LogController.SinkStruct ToSinkStruct()
        {
            var s = base.ToSinkStruct();
            s.OnBeforeSink = new OnBeforeSinkDelegate(OnBeforeSinkFunc);
            s.OnLogMessageEmit = new OnLogMessageEmitDelegate(OnLogMessageEmitFunc);
            s.OnAfterSink = new OnAfterSinkDelegate(OnAfterSinkFunc);
            return s;
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(OnBeforeSinkDelegate.Delegate))]
        internal static void OnBeforeSinkFunc(IntPtr userData)
        {
            Console.BeginBatch();
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(OnLogMessageEmitDelegate.Delegate))]
        internal static void OnLogMessageEmitFunc(in LogMessage logEvent, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator)
        {
            unsafe
            {
                try
                {
                    var data = messageBuffer.GetUnsafePtr();
                    byte newLine = 1;
                    Console.Write(data, messageBuffer.Length, newLine);
                }
                finally
                {
                    messageBuffer.Length = 0;
                }
            }
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(OnAfterSinkDelegate.Delegate))]
        internal static void OnAfterSinkFunc(IntPtr userData)
        {
            Console.EndBatch();
        }
    }
}
