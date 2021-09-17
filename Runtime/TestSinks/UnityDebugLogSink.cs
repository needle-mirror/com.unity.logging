using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging;
using Unity.Logging.Internal.Debug;
using Unity.Logging.Sinks;

[assembly: RegisterGenericJobType(typeof(SinkJob<UnityDebugLogger>))]

namespace Unity.Logging.Sinks
{
    public struct UnityDebugLogger : ILogger
    {
        public void OnLogMessage(in LogMessage logEvent, in FixedString512Bytes outTemplate, ref LogMemoryManager memoryManager)
        {
            var message = default(UnsafeText);
            var errorMessage = default(FixedString512Bytes);

            if (TextLoggerParser.ParseMessage(outTemplate, logEvent, ref message, ref errorMessage, ref memoryManager))
            {
                if (message.IsCreated)
                {
                    try
                    {
                        unsafe
                        {
                            var data = message.GetUnsafePtr();
                            var length = message.Length;

                            ManagedUnityEngineDebugLogWrapper.Write(logEvent.Level, data, length);
                        }
                    }
                    finally
                    {
                        message.Dispose();
                    }
                }
                else
                {
                    throw new Exception(); // this is a test sink
                }
            }
            else
            {
                SelfLog.OnFailedToParseMessage();

                throw new Exception(errorMessage.ToString()); // this is a test sink
            }
        }
    }

    public class UnityDebugLogSink : SinkSystemBase<UnityDebugLogger>
    {
        public override void Initialize(in Logger logger, in SinkConfiguration systemConfig)
        {
            ManagedUnityEngineDebugLogWrapper.Initialize();
            base.Initialize(logger, systemConfig);
        }
    }


    public static class UnityDebugLogSinkExt
    {
        public static LoggerConfig UnityDebugLog(this LoggerWriterConfig writeTo,
                                                 bool captureStackTrace = false,
                                                 LogLevel? minLevel = null,
                                                 FixedString512Bytes? outputTemplate = null)
        {
            return writeTo.AddSinkConfig(new SinkConfiguration<UnityDebugLogSink>
            {
                CaptureStackTraces = captureStackTrace,
                MinLevelOverride = minLevel,
                OutputTemplateOverride = outputTemplate,
            });
        }
    }

    internal static class ManagedUnityEngineDebugLogWrapper
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal unsafe delegate void WriteDelegate(LogLevel level, byte* data, int length);

        // make sure delegates are not collected by GC
        private static WriteDelegate s_WriteDelegate;

        private struct ManagedUnityEngineDebugLogWrapperKey {}
        internal static readonly SharedStatic<FunctionPointer<WriteDelegate>> s_WriteMethod = SharedStatic<FunctionPointer<WriteDelegate>>.GetOrCreate<FunctionPointer<WriteDelegate>, ManagedUnityEngineDebugLogWrapperKey>(16);

        private static bool IsInitialized;

        internal static void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            unsafe
            {
                s_WriteDelegate = WriteFunc;
                s_WriteMethod.Data = new FunctionPointer<WriteDelegate>(Marshal.GetFunctionPointerForDelegate(s_WriteDelegate));
            }
        }

        [AOT.MonoPInvokeCallback(typeof(WriteDelegate))]
        private static unsafe void WriteFunc(LogLevel level, byte* data, int length)
        {
            var str = System.Text.UTF8Encoding.UTF8.GetString(data, length);

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
            s_WriteMethod.Data.Invoke(level, data, length);
        }
    }
}
