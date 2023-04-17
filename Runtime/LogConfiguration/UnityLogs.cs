using System;
using Unity.Logging;
using Unity.Logging.Internal;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace Unity.Logging
{
    internal class UnityLogs
    {
#if UNITY_STARTUP_LOGS_API
        internal static void RetrieveStartupLogs(Logger logger)
        {
            foreach (var log in Debug.RetrieveStartupLogs())
            {
                LogUnityLog(logger.Handle, TimeStampWrapper.DateTimeTicksToNanosec(log.timestamp), log.logType, log.message);
            }
        }
#endif

#if !UNITY_DOTSRUNTIME
        internal static void RedirectUnityLogs(Logger logger)
        {
            UnityLogRedirectorManager.BeginRedirection(logger);
        }
#endif

        private static void WriteBurstedCapturedLog(in long timestamp, in LogLevel logLevel, in PayloadHandle msg, ref LogController logController, ref LogControllerScopedLock @lock)
        {
            FixedList512Bytes<PayloadHandle> handles = new FixedList512Bytes<PayloadHandle>();
            PayloadHandle handle;

            ref var memManager = ref logController.MemoryManager;

            if (msg.IsValid)
                handles.Add(msg);

            Unity.Logging.Builder.BuildDecorators(ref logController, @lock, ref handles);

            handle = memManager.CreateDisjointedPayloadBufferFromExistingPayloads(ref handles);
            if (handle.IsValid)
            {
                logController.DispatchMessage(handle, timestamp, 0, logLevel);
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

        public static void LogUnityLog(LoggerHandle handle, long timestamp, LogType logType, string msg)
        {
            var logLevel = LogLevelFromLogType(logType);
            var scopedLock = LogControllerScopedLock.Create(handle);
            try 
            {
                ref var logController = ref scopedLock.GetLogController();
                if (logController.HasSinksFor(logLevel) == false) return;
                PayloadHandle payloadHandle_msg = Unity.Logging.Builder.BuildMessage(msg, ref logController.MemoryManager);
                WriteBurstedCapturedLog(timestamp, logLevel, payloadHandle_msg, ref logController, ref scopedLock);
            }
            finally
            {
                scopedLock.Dispose();
            }
        }
    }
}
