using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging.Internal;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Interface for a struct that can be used in the SinkJob<T> as a T. This way it will remain burst-friendly.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// OnLogMessage is a main sink function that takes as an input <see cref="LogMessage"/> and does something with it
        /// for instance converts into a string and writes the string to the console (see <see cref="ConsoleSinkSystem"/>)
        /// </summary>
        /// <param name="logEvent">LogMessage that should be processed</param>
        /// <param name="outTemplate">General template that instructs how to convert the message into string. Example: '{Timestamp} | {Level} | {Message}'</param>
        /// <param name="memoryManager">Memory manager that contains all the binary info that you can request with LogMessage struct</param>
        /// <returns></returns>
        void OnLogMessage(in LogMessage logEvent, in FixedString512Bytes outTemplate, ref LogMemoryManager memoryManager);
    }

    /// <summary>
    /// General SinkJob structure that calls <see cref="ILogger.OnLogMessage"/> on every LogMessage for this sink
    /// </summary>
    /// <typeparam name="TLogger"></typeparam>
    [BurstCompile]
    internal struct SinkJob<TLogger> : IJob where TLogger : struct, ILogger
    {
        /// <summary>
        /// Logger that implements <see cref="ILogger.OnLogMessage"/> to process every <see cref="LogMessage"/>
        /// </summary>
        public TLogger Logger;

        /// <summary>
        /// General template that instructs how to convert the message into string. Example: '{Timestamp} | {Level} | {Message}'
        /// </summary>
        [ReadOnly] public FixedString512Bytes OutTemplate;

        /// <summary>
        /// <see cref="HasSinkStruct"/> struct that can answer - is this <see cref="LogLevel"/> supported by this Sink.
        /// </summary>
        [ReadOnly] public HasSinkStruct FilterLevel;

        [ReadOnly]
        public bool unblittableWorkaroundForMarshalBug;

        /// <summary>
        /// <see cref="LogControllerScopedLock"/> to access LogController
        /// </summary>
        public LogControllerScopedLock Lock;

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
                            Logger.OnLogMessage(elem, OutTemplate, ref logController.MemoryManager);
                    }
                }
            }
            finally
            {
                logController.DispatchQueue.EndRead();
            }
        }
    }

    /// <summary>
    /// Interface for a Sink
    /// </summary>
    public interface ISinkSystemInterface : IDisposable
    {
        /// <summary>
        /// Method to initialize the sink
        /// </summary>
        /// <param name="logger">Parent <see cref="Logger"/></param>
        /// <param name="systemConfig"><see cref="SinkConfiguration"/>-inherited class that contains specialized configurations for the sink</param>
        void Initialize(in Logger logger, in SinkConfiguration systemConfig);

        /// <summary>
        /// Returns minimal <see cref="LogLevel"/> that this sink is interested in
        /// </summary>
        /// <returns>Minimal <see cref="LogLevel"/> that this sink is interested in</returns>
        LogLevel GetMinimalLogLevel();

        /// <summary>
        /// Returns true if stack traces are required
        /// </summary>
        /// <returns>True if stack traces are required</returns>
        bool NeedsStackTrace();

        /// <summary>
        /// Set minimal log level that this Sink is interested in
        /// </summary>
        /// <param name="minimalLevel"><see cref="LogLevel"/> to set as a minimal level</param>
        void SetMinimalLogLevel(LogLevel minimalLevel);

        /// <summary>
        /// Schedule update for this sink. Usually schedules internal <see cref="SinkJob{TLogger}"/>
        /// </summary>
        /// <param name="logControllerScopedLock"></param>
        /// <param name="dependency">Input dependency that should be done before this job</param>
        /// <returns>Job handle for the SinkJob</returns>
        JobHandle ScheduleUpdate(LogControllerScopedLock logControllerScopedLock, JobHandle dependency);
    }

    /// <summary>
    /// Base Sink class that implements ISinkSystemInterface
    /// </summary>
    /// <typeparam name="TLoggerImpl">Implementation of <see cref="ILogger"/></typeparam>
    [BurstCompile]
    public class SinkSystemBase<TLoggerImpl> : ISinkSystemInterface where TLoggerImpl : struct, ILogger
    {
        protected TLoggerImpl LoggerImpl;
        protected bool IsInitialized;
        protected Logger Logger;
        protected SinkConfiguration SystemConfig;
        protected FixedString512Bytes OutputTemplate;
        protected LogLevel MinimalLevel;
        protected bool CaptureStackTraces;

        /// <summary>
        /// See <see cref="IDisposable"/>
        /// </summary>
        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Schedule update for this sink. Usually schedules internal <see cref="SinkJob{TLoggerImpl}"/>
        /// </summary>
        /// <param name="@lock">Lock to access to LogController</param>
        /// <param name="dependency">Input dependency that should be done before this job</param>
        /// <returns>Job handle for the SinkJob</returns>
        public virtual JobHandle ScheduleUpdate(LogControllerScopedLock @lock, JobHandle dependency)
        {
            if (IsInitialized == false)
                return dependency;

            var executeJob = new SinkJob<TLoggerImpl>
            {
                Logger = LoggerImpl,
                OutTemplate = OutputTemplate,
                Lock = @lock,
                FilterLevel = HasSinkStruct.FromMinLogLevel(MinimalLevel)
            }.Schedule(dependency);

            return executeJob;
        }

        /// <summary>
        /// Method to initialize the sink
        /// </summary>
        /// <param name="logger">Parent <see cref="Logging.Logger"/></param>
        /// <param name="systemConfig"><see cref="SinkConfiguration"/>-inherited class that contains specialized configurations for the sink</param>
        /// /// <exception cref="Exception">If logger.Handle.IsValid is false</exception>
        public virtual void Initialize(in Logger logger, in SinkConfiguration systemConfig)
        {
            Logger = logger;
            LoggerImpl = new TLoggerImpl();
            SystemConfig = systemConfig;
            CaptureStackTraces = systemConfig.CaptureStackTraces;
            MinimalLevel = (LogLevel)systemConfig.MinLevelOverride;
            OutputTemplate = (FixedString512Bytes)systemConfig.OutputTemplateOverride;

            if (Logger.Handle.IsValid == false)
                throw new Exception("Logger must have a valid handle");

            IsInitialized = true;
        }

        public bool NeedsStackTrace()
        {
            return CaptureStackTraces;
        }

        /// <summary>
        /// Set minimal log level that this Sink is interested in
        /// </summary>
        /// <param name="minimalLevel"><see cref="LogLevel"/> to set as a minimal level</param>
        public void SetMinimalLogLevel(LogLevel minimalLevel)
        {
            MinimalLevel = minimalLevel;
        }

        /// <summary>
        /// Get minimal log level that this Sink is interested in
        /// </summary>
        /// <returns>Minimal log level that this Sink is interested in</returns>
        public LogLevel GetMinimalLogLevel()
        {
            return MinimalLevel;
        }

        protected void OnSinkFatalError(FixedString512Bytes reason)
        {
            Internal.Debug.SelfLog.OnSinkFatalError(this, reason);

            IsInitialized = false;
        }
    }
}
