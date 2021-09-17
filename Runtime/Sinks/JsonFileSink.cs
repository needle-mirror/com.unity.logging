#if UNITY_DOTSRUNTIME
#define USE_BASELIB
#define USE_BASELIB_FILEIO
#endif

using Unity.Logging;
using Unity.Logging.Sinks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using Unity.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging.Internal;
#if USE_BASELIB_FILEIO
using static Unity.Baselib.LowLevel.Binding;
[assembly: RegisterGenericJobType(typeof(SinkJob<JsonFileSinkLogger<FileOperationsBaselib>>))]
#else
using System.Threading.Tasks;
[assembly: RegisterGenericJobType(typeof(SinkJob<JsonFileSinkLogger<FileOperationsFileStream>>))]
#endif


namespace Unity.Logging.Sinks
{
    [BurstCompile]
#if USE_BASELIB_FILEIO
    [BurstCompatible(GenericTypeArguments = new [] {typeof(FileOperationsBaselib)})]
#else
    [BurstCompatible(GenericTypeArguments = new [] {typeof(FileOperationsFileStream)})]
#endif

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct JsonFileSinkLogger<TWriter> : ILogger where TWriter : struct, IFileOperations
    {
        internal TWriter fileOps;

        public void OnLogMessage(in LogMessage logEvent, in FixedString512Bytes outTemplate, ref LogMemoryManager memoryManager)
        {
            var message = TextLoggerParser.ParseMessageToJson(logEvent, outTemplate, ref memoryManager);
            if (message.IsCreated)
            {
                try
                {
                    unsafe
                    {
                        var data = message.GetUnsafePtr();
                        var length = message.Length;
                        var newLine = true;
                        fileOps.Append(data, (ulong)length, newLine);
                    }
                }
                finally
                {
                    message.Dispose();
                }
            }
        }
    }


    // NOTE: Don't inherit from FileSinkSystemConfig, <JsonFileSinkSystem> is important part
    internal class JsonFileSinkSystemConfig : SinkConfiguration<JsonFileSinkSystem>
    {
        public FixedString512Bytes FileName;
        public FixedString32Bytes FileExt;
        public long MaxFileSizeBytes;
        public TimeSpan MaxTimeSpan;
        public int MaxRoll;
    }

    public static class JsonFileSinkSystemExt
    {
        /// <summary>
        /// Write structured logs to the json file
        /// </summary>
        /// <param name="writeTo">Logger config</param>
        /// <param name="fileName">Absolute file path to the log file</param>
        /// <param name="maxFileSizeBytes">Threshold of file size in bytes after which new file should be created (rolling). 0 if no rolling by file size is needed</param>
        /// <param name="maxRoll">Max amount of rolls after which old files will be rewritten</param>
        /// <param name="maxTimeSpan">Threshold of time after which new file should be created (rolling). 'default' if no rolling by time is needed</param>
        /// <param name="captureStackTrace">True if stack traces should be captured</param>
        /// <param name="minLevel">Minimal level of logs for this particular sink. Null if common level should be used</param>
        /// <param name="outputTemplate">Output message template for this particular sink. Null if common template should be used</param>
        /// <returns>Logger config</returns>
        public static LoggerConfig JsonFile(this LoggerWriterConfig writeTo, string fileName, long maxFileSizeBytes = 0, TimeSpan maxTimeSpan = default, int maxRoll = 15, bool captureStackTrace = false, LogLevel? minLevel = null, FixedString512Bytes? outputTemplate = null)
        {
            FileUtils.MakeSureDirectoryExistsForFile(fileName);

            var ext = FileUtils.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext) == false)
            {
                fileName = fileName.Substring(0, fileName.Length - ext.Length);
            }

            return writeTo.AddSinkConfig(new JsonFileSinkSystemConfig
            {
                CaptureStackTraces = captureStackTrace,
                FileName = fileName,
                FileExt = ext,
                MaxFileSizeBytes = maxFileSizeBytes,
                MinLevelOverride = minLevel,
                OutputTemplateOverride = outputTemplate,
                MaxRoll = maxRoll,
                MaxTimeSpan = maxTimeSpan
            });
        }
    }

#if USE_BASELIB_FILEIO
    public class JsonFileSinkSystem : JsonFileSinkSystemBase<FileOperationsBaselib> {}
#else
    public class JsonFileSinkSystem : JsonFileSinkSystemBase<FileOperationsFileStream>
    {
        public override void Initialize(in Logger logger, in SinkConfiguration systemConfig)
        {
            ManagedFileOperationsFunctions.Initialize();
            base.Initialize(logger, systemConfig);
        }
    }
#endif

    [BurstCompile]
    public class JsonFileSinkSystemBase<T> : SinkSystemBase<JsonFileSinkLogger<T>> where T : struct, IFileOperations
    {
        public override void Initialize(in Logger logger, in SinkConfiguration systemConfig)
        {
            base.Initialize(logger, systemConfig);
            IsInitialized = false;

            LoggerImpl.fileOps = new T();

            var config = (JsonFileSinkSystemConfig)SystemConfig;

            FixedString4096Bytes prefix = "[";
            FixedString64Bytes separator = ",";
            FixedString4096Bytes postfix = "]";
            LoggerImpl.fileOps.Initialize(ref prefix, ref separator, ref postfix);

            if (LoggerImpl.fileOps.OpenFileForLogging(ref config.FileName, ref config.FileExt, config.MaxFileSizeBytes, config.MaxTimeSpan, config.MaxRoll))
            {
                IsInitialized = true;
            }
            else
            {
                OnSinkFatalError($"Cannot open file '{config.FileName}{config.FileExt}' for write");
            }
        }

        public override JobHandle ScheduleUpdate(LogControllerScopedLock @lock, JobHandle dependency)
        {
            if (IsInitialized == false)
                return dependency;

            var executeJob = new JsonSinkJob<T>
            {
                Logger = LoggerImpl,
                Lock = @lock,
                FilterLevel = HasSinkStruct.FromMinLogLevel(MinimalLevel)
            }.Schedule(dependency);

            return executeJob;
        }

        public override void Dispose()
        {
            if (IsInitialized)
                LoggerImpl.fileOps.Dispose();
        }

        public void Flush()
        {
            if (IsInitialized)
                LoggerImpl.fileOps.Flush();
        }
    }

    // Customized version of SinkJob that adds commas in between logs
    [BurstCompile]
#if USE_BASELIB_FILEIO
    [BurstCompatible(GenericTypeArguments = new [] {typeof(FileOperationsBaselib)})]
#else
    [BurstCompatible(GenericTypeArguments = new [] {typeof(FileOperationsFileStream)})]
#endif

    internal struct JsonSinkJob<T> : IJob  where T : struct, IFileOperations
    {
        /// <summary>
        /// Logger that implements <see cref="ILogger.OnLogMessage"/> to process every <see cref="LogMessage"/>
        /// </summary>
        [ReadOnly] public JsonFileSinkLogger<T> Logger;

        /// <summary>
        /// <see cref="LogControllerScopedLock"/> to access LogController
        /// </summary>
        [ReadOnly] public LogControllerScopedLock Lock;

        /// <summary>
        /// <see cref="HasSinkStruct"/> struct that can answer - is this <see cref="LogLevel"/> supported by this Sink.
        /// </summary>
        [ReadOnly] public HasSinkStruct FilterLevel;

        public void Execute()
        {
            ref var logController = ref Lock.GetLogController();

            try
            {
                var reader = logController.DispatchQueue.BeginRead();

                var n = reader.Length;
                for (var position = 0; position < n; ++position)
                {
                    unsafe
                    {
                        var elem = UnsafeUtility.ReadArrayElement<LogMessage>(reader.Ptr, position);
                        if (FilterLevel.Has(elem.Level))
                        {
                            Logger.OnLogMessage(elem, default, ref logController.MemoryManager);
                        }
                    }
                }
            }
            finally
            {
                logController.DispatchQueue.EndRead();
            }
        }
    }
}
