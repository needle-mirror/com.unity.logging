#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

#if UNITY_CONSOLE_API && UNITY_EDITOR
#define USE_CONSOLE_SINK

#if UNITY_HAS_UTF8_VIEW
using UTF8View = UnityEngine.UTF8StringView;
#else
using UTF8View = UnityEditor.LogEntryUTF8StringView;
#endif

#endif

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Logging.Internal;
using Unity.Logging.Internal.Debug;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Extension class for LoggerWriterConfig .UnityEditorConsole
    /// </summary>
    public static class UnityEditorConsoleSinkExt
    {
        /// <summary>
        /// Write logs to the UnityEditor's Console window. Does nothing in a standalone build or in Unity prior to 2022.2
        /// </summary>
        /// <param name="writeTo">Logger config</param>
        /// <param name="captureStackTrace">True if stack traces should be captured</param>
        /// <param name="minLevel">Minimal level of logs for this particular sink. Null if common level should be used</param>
        /// <param name="outputTemplate">Output message template for this particular sink. Null if common template should be used</param>
        /// <returns>Logger config</returns>
        public static LoggerConfig UnityEditorConsole(this LoggerWriterConfig writeTo,
                                                    bool? captureStackTrace = null,
                                                    LogLevel? minLevel = null,
                                                    FixedString512Bytes? outputTemplate = null)
        {
            var config = new UnityEditorConsoleSink.Configuration(writeTo, captureStackTrace, minLevel, outputTemplate);

            return writeTo.AddSinkConfig(config);
        }
    }

    /// <summary>
    /// UnityEditor's Console sink class
    /// </summary>
    [BurstCompile]
    public class UnityEditorConsoleSink : SinkSystemBase
    {
        /// <summary>
        /// Configuration for string sink
        /// </summary>
        public class Configuration : SinkConfiguration
        {
            /// <summary>
            /// Creates the UnityEditorConsoleSink
            /// </summary>
            /// <param name="logger">Logger that owns sink</param>
            /// <returns>SinkSystemBase</returns>
            public override SinkSystemBase CreateSinkInstance(Logger logger) => CreateAndInitializeSinkInstance<UnityEditorConsoleSink>(logger, this);

            /// <summary>
            /// Constructor for the configuration
            /// </summary>
            /// <param name="writeTo">Logger config</param>
            /// <param name="captureStackTraceOverride">True if stack traces should be captured. Null if default</param>
            /// <param name="minLevelOverride">Minimal level of logs for this particular sink. Null if common level should be used</param>
            /// <param name="outputTemplateOverride">Output message template for this particular sink. Null if common template should be used</param>
            public Configuration(LoggerWriterConfig writeTo,
                                 bool? captureStackTraceOverride = null, LogLevel? minLevelOverride = null, FixedString512Bytes? outputTemplateOverride = null)
                : base(writeTo, formatter: default, captureStackTraceOverride, minLevelOverride, outputTemplateOverride)
            {}
        }

        /// <summary>
        /// Creates <see cref="LogController.SinkStruct"/>
        /// </summary>
        /// <returns>SinkStruct</returns>
        public override LogController.SinkStruct ToSinkStruct()
        {
#if USE_CONSOLE_SINK
            var s = base.ToSinkStruct();
            s.Formatter = LogFormatterText.Formatter;
            unsafe
            {
                s.UserData = new IntPtr(&s.Formatter);
            }

            s.OnLogMessageEmit = new OnLogMessageEmitDelegate(OnLogMessageEmitFunc);
            return s;
#else
            return default; // disabled in this case - native Console sink exists
#endif
        }

#if USE_CONSOLE_SINK
        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(OnLogMessageEmitDelegate.Delegate))]
        internal static void OnLogMessageEmitFunc(in LogMessage logEvent, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator)
        {
            unsafe
            {
                try
                {
                    messageBuffer.Length = 0;

                    ref var memAllocator = ref LogMemoryManager.FromPointer(memoryManager);
                    var headerData = HeaderData.Parse(in logEvent, ref memAllocator);

                    if (headerData.Error != ErrorCodes.NoError)
                        return;

                    FixedString512Bytes errorMessage = default;


                    var messageStart = messageBuffer.Length;

                    ref var formatter = ref UnsafeUtility.AsRef<FormatterStruct>(userData.ToPointer());
                    var success = LogFormatterText.WriteMessage(in logEvent, ref formatter, ref headerData, ref messageBuffer, ref errorMessage, ref memAllocator);

                    var messageLength = messageBuffer.Length - messageStart;

                    if (success == false)
                        return;

                    var timestampStart = messageBuffer.Length;
                    messageBuffer.Append('[');
                    LogWriterUtils.WriteFormattedTimestampLocalTimeZoneForConsole(logEvent.Timestamp, ref messageBuffer);
                    messageBuffer.Append(']');
                    var timestampLength = messageBuffer.Length - timestampStart;

                    var stacktraceStart = messageBuffer.Length;
                    ManagedStackTraceWrapper.AppendToUnsafeText(logEvent.StackTraceId, ref messageBuffer);
                    var stacktraceLength = messageBuffer.Length - stacktraceStart;

                    var mode = GetLogMessageFlags(in logEvent);

                    var logEntry = new UnityEditor.LogEntryStruct
                    {
                        mode = mode,
                        timestamp = new UTF8View(&messageBuffer.GetUnsafePtr()[timestampStart], timestampLength),
                        callstack = new UTF8View(&messageBuffer.GetUnsafePtr()[stacktraceStart], stacktraceLength),
                        message = new UTF8View(&messageBuffer.GetUnsafePtr()[messageStart], messageLength),
                    };

                    UnityEditor.ConsoleWindow.AddMessage(ref logEntry);
                }
                finally
                {
                    messageBuffer.Length = 0;
                }
            }
        }

        private static UnityEditor.LogMessageFlags GetLogMessageFlags(in LogMessage logEvent)
        {
            switch (logEvent.Level)
            {
                case LogLevel.Warning:
                    return UnityEditor.LogMessageFlags.DebugWarning;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    return UnityEditor.LogMessageFlags.DebugError;
            }
            return UnityEditor.LogMessageFlags.DebugLog;
        }
#endif
    }
}
