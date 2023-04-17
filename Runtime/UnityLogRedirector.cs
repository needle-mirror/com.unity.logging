#if !UNITY_DOTSRUNTIME
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Logging;
using Unity.Logging.Internal;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace Unity.Logging
{
    /// <summary>
    /// Manages the details of redirection on a per-logger basis.
    ///</summary>
    public static class UnityLogRedirectorManager
    {
        private static byte s_Initialized;
        private static ILogger s_loggerUnity;
        internal static List<Logger> s_loggersRedirectingUnityLogs;
        private static ILogHandler s_logHandlerUnity;
        private static ILogHandler s_logHandlerRedirect;
        private static LogType s_filterLogTypeOriginal;
        private static bool s_logEnabledOriginal;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate void LogErrorDelegate(byte* stringBuffer, int BufferLength);
        private struct LogErrorDelegateKey {}

        [BurstDiscard]
        internal static void Initialize()
        {
            if (s_Initialized != 0)
                return;
            s_Initialized = 1;

            s_logHandlerUnity = UnityEngine.Debug.unityLogger.logHandler;
            s_loggerUnity = new UnityEngine.Logger(s_logHandlerUnity);
            s_logHandlerRedirect = new UnityLogRedirector();
            s_loggersRedirectingUnityLogs = new List<Logger>();

            unsafe {
                Burst2ManagedCall<LogErrorDelegate, LogErrorDelegateKey>.Init(ReportError);
            }
        }

        internal static void BeginRedirection(Logger logger)
        {
            if (s_loggersRedirectingUnityLogs.Count == 0)
                BeginRedirection();
            s_loggersRedirectingUnityLogs.Add(logger);
        }

        internal static void EndRedirection(Logger logger)
        {
            s_loggersRedirectingUnityLogs.Remove(logger);
            if (s_loggersRedirectingUnityLogs.Count == 0)
                EndRedirection();
        }

        private static void BeginRedirection()
        {
            s_logHandlerUnity = UnityEngine.Debug.unityLogger.logHandler;
            s_filterLogTypeOriginal = UnityEngine.Debug.unityLogger.filterLogType;
            s_logEnabledOriginal = UnityEngine.Debug.unityLogger.logEnabled;
            UnityEngine.Debug.unityLogger.logHandler = s_logHandlerRedirect;
            UnityEngine.Debug.unityLogger.filterLogType = LogType.Log;
            UnityEngine.Debug.unityLogger.logEnabled = true;
        }

        private static void EndRedirection()
        {
            UnityEngine.Debug.unityLogger.logHandler = s_logHandlerUnity;
            UnityEngine.Debug.unityLogger.filterLogType = s_filterLogTypeOriginal;
            UnityEngine.Debug.unityLogger.logEnabled = s_logEnabledOriginal;
        }

        [AOT.MonoPInvokeCallback(typeof(LogErrorDelegate))]
        internal static unsafe void ReportError(byte *stringBuffer, int bufferLength)
        {
            s_loggerUnity.LogError("", System.Text.Encoding.UTF8.GetString(stringBuffer, bufferLength));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void UnityLogError(ref FixedString4096Bytes message)
        {
            var data = message.GetUnsafePtr();
            var length = message.Length;
            var ptr = Burst2ManagedCall<LogErrorDelegate, LogErrorDelegateKey>.Ptr();
            ptr.Invoke(data, length);
        }
    }

    /// <summary>
    /// Implements ILogHandler for redirection of Unity logs.
    /// </summary>
    [HideInStackTrace]
    public class UnityLogRedirector: UnityEngine.ILogHandler
    {
        private static void WriteBurstedRedirectedLog(in LogLevel logLevel, in PayloadHandle msg, ref LogController logController, ref LogControllerScopedLock @lock)
        {
            FixedList512Bytes<PayloadHandle> handles = new FixedList512Bytes<PayloadHandle>();
            PayloadHandle handle;

            ref var memManager = ref logController.MemoryManager;

            if (msg.IsValid)
                handles.Add(msg);

            var stackTraceId = logController.NeedsStackTrace ? ManagedStackTraceWrapper.Capture() : 0;

            Unity.Logging.Builder.BuildDecorators(ref logController, @lock, ref handles);

            handle = memManager.CreateDisjointedPayloadBufferFromExistingPayloads(ref handles);
            if (handle.IsValid)
            {
                logController.DispatchMessage(handle, stackTraceId, logLevel);
            }
            else
            {
                Unity.Logging.Internal.Debug.SelfLog.OnFailedToCreateDisjointedBuffer();
                Unity.Logging.Builder.ForceReleasePayloads(handles, ref memManager);
            }
        }

        private static LogLevel LogLevelFromLogType(LogType logType)
        {
            if (logType == LogType.Error)
                return LogLevel.Error;
            if (logType == LogType.Assert)
                return LogLevel.Fatal;
            if (logType == LogType.Warning)
                return LogLevel.Warning;
            if (logType == LogType.Log)
                return LogLevel.Info;
            if (logType == LogType.Exception)
                return LogLevel.Fatal;
            if ((int)logType == 5) // Managed and Native differ here
                return LogLevel.Debug;

            return LogLevel.Fatal;
        }

        private static void LogRedirectedLog(LoggerHandle handle, LogType logType, string msg)
        {
            var logLevel = LogLevelFromLogType(logType);
            var scopedLock = LogControllerScopedLock.Create(handle);
            try 
            {
                ref var logController = ref scopedLock.GetLogController();
                if (logController.HasSinksFor(logLevel) == false) return;
                PayloadHandle payloadHandle_msg = Unity.Logging.Builder.BuildMessage(msg, ref logController.MemoryManager);
                WriteBurstedRedirectedLog(logLevel, payloadHandle_msg, ref logController, ref scopedLock);
            }
            finally
            {
                scopedLock.Dispose();
            }
        }

        /// <summary>
        /// Redirect Unity log
        /// </summary>
        /// <param name="logType">The type of the log message </param>
        /// <param name="context">Object to which the message applies</param>
        /// <param name="format">A composite format string</param>
        /// <param name="args">Format arguments</param>
        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            foreach (var logger in UnityLogRedirectorManager.s_loggersRedirectingUnityLogs)
                LogRedirectedLog(logger.Handle, logType, String.Format(format, args));
        }

        /// <summary>
        /// Redirect Unity exception log
        /// </summary>
        /// <param name="exception">Runtime Exception</param>
        /// <param name="context">Object to which the message applies</param>
        public void LogException(Exception exception, UnityEngine.Object context)
        {
            foreach (var logger in UnityLogRedirectorManager.s_loggersRedirectingUnityLogs)
                LogRedirectedLog(logger.Handle, LogType.Exception, exception.Message);
        }
    }
}
#endif
