#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Logging;
using Unity.Logging.Internal.Debug;
using Unity.Logging.Sinks;

namespace Unity.Logging.Sinks
{
    public static class UnityDebugLogSinkExt
    {
        public static LoggerConfig UnityDebugLog(this LoggerWriterConfig writeTo,
                                                 FormatterStruct formatter = default,
                                                 bool? captureStackTrace = null,
                                                 LogLevel? minLevel = null,
                                                 FixedString512Bytes? outputTemplate = null)
        {
            if (formatter.IsCreated == false)
                formatter = LogFormatterText.Formatter;

            return writeTo.AddSinkConfig(new UnityDebugLogSink.Configuration(writeTo, formatter, captureStackTrace, minLevel, outputTemplate));
        }
    }

    [BurstCompile]
    public class UnityDebugLogSink : SinkSystemBase
    {
        public class Configuration : SinkConfiguration
        {
            public override SinkSystemBase CreateSinkInstance(Logger logger) => CreateAndInitializeSinkInstance<UnityDebugLogSink>(logger, this);

            public Configuration(LoggerWriterConfig writeTo, FormatterStruct formatter,
                                 bool? captureStackTraceOverride = null, LogLevel? minLevelOverride = null, FixedString512Bytes? outputTemplateOverride = null)
                : base(writeTo, formatter, captureStackTraceOverride, minLevelOverride, outputTemplateOverride)
            {}
        }

        public override LogController.SinkStruct ToSinkStruct()
        {
            var s = base.ToSinkStruct();
            s.OnLogMessageEmit = new OnLogMessageEmitDelegate(OnLogMessageEmitFunc);
            return s;
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
                    ManagedUnityEngineDebugLogWrapper.Write(logEvent.Level, data, messageBuffer.Length);
                }
                finally
                {
                    messageBuffer.Length = 0;
                }
            }
        }

        public override void Initialize(Logger logger, SinkConfiguration systemConfig)
        {
            ManagedUnityEngineDebugLogWrapper.Initialize();
            base.Initialize(logger, systemConfig);
        }
    }

    internal static class ManagedUnityEngineDebugLogWrapper
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal unsafe delegate void WriteDelegate(LogLevel level, byte* data, int length);

        private struct ManagedUnityEngineDebugLogWrapperKey {}

        private static bool s_IsInitialized;

        internal static void Initialize()
        {
            if (s_IsInitialized) return;
            s_IsInitialized = true;

            unsafe
            {
                Burst2ManagedCall<WriteDelegate, ManagedUnityEngineDebugLogWrapperKey>.Init(WriteFunc);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(WriteDelegate))]
        private static unsafe void WriteFunc(LogLevel level, byte* data, int length)
        {
            var str = System.Text.Encoding.UTF8.GetString(data, length);

            switch (level)
            {
                case LogLevel.Verbose:
                case LogLevel.Debug:
                case LogLevel.Info:
                    UnityEngine.Debug.Log(str);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(str);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    UnityEngine.Debug.LogError(str);
                    break;
                default:
                    throw new Exception("Unknown LogLevel");
            }
        }

        // called from burst or not burst
        public static unsafe void Write(LogLevel level, byte* data, int length)
        {
            var ptr = Burst2ManagedCall<WriteDelegate, ManagedUnityEngineDebugLogWrapperKey>.Ptr();
#if LOGGING_USE_UNMANAGED_DELEGATES
            ((delegate * unmanaged[Cdecl] <LogLevel, byte*, int, void>)ptr.Value)(level, data, length);
#else
            ptr.Invoke(level, data, length);
#endif
        }
    }
}
