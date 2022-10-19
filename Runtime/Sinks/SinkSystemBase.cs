using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Logging.Internal;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Base Sink class that implements SinkSystemBase
    /// </summary>
    [BurstCompile]
    public abstract class SinkSystemBase : IDisposable
    {
        /// <summary>
        /// Id of this sink in the logger
        /// </summary>
        public int SinkId;

        /// <summary>
        /// True if Sink was initialized and can run 'Update'
        /// </summary>
        protected bool IsInitialized;

        /// <summary>
        /// Logger's handle that owns the sink
        /// </summary>
        protected LoggerHandle Handle;

        /// <summary>
        /// <see cref="SinkConfiguration"/> that was used to setup the sink
        /// </summary>
        protected SinkConfiguration SystemConfig;

        /// <summary>
        /// SinkStruct that is burst-compatible struct that represents this sink
        /// </summary>
        /// <returns>SinkStruct struct of this sink</returns>
        public virtual LogController.SinkStruct ToSinkStruct()
        {
            return new LogController.SinkStruct
            {
                LastTimestamp = long.MinValue,

                MinimalLevel = SystemConfig.MinLevel,
                OutputTemplate = SystemConfig.OutputTemplate,
                CaptureStackTracesBytes = SystemConfig.CaptureStackTraces ? 1 : 0,
                Formatter = SystemConfig.LogFormatter
            };
        }

        /// <summary>
        /// See <see cref="IDisposable"/>. Used to dispose all the resources associated with this sink.
        /// </summary>
        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Schedule update for this sink. Usually schedules an internal SinkJob.
        /// </summary>
        /// <param name="lock">Lock to access to LogController</param>
        /// <param name="dependency">Input dependency that should be done before this job</param>
        /// <returns>Job handle for the SinkJob</returns>
        public virtual JobHandle ScheduleUpdate(LogControllerScopedLock @lock, JobHandle dependency)
        {
            if (IsInitialized == false || SinkId < 0)
                return dependency;

            var executeJob = new SinkJob
            {
                SinkId = SinkId,
                Lock = @lock,
            }.Schedule(dependency);

            return executeJob;
        }

        /// <summary>
        /// Method to initialize the sink
        /// </summary>
        /// <param name="logger">Parent <see cref="Logging.Logger"/></param>
        /// <param name="systemConfig"><see cref="SinkConfiguration"/>-inherited class that contains specialized configurations for the sink</param>
        /// /// <exception cref="Exception">If logger.Handle.IsValid is false</exception>
        public virtual void Initialize(Logger logger, SinkConfiguration systemConfig)
        {
            Handle = logger.Handle;
            SystemConfig = systemConfig;

            if (Handle.IsValid == false)
                throw new Exception("Logger must have a valid handle");

            IsInitialized = true;
        }


        /// <summary>
        /// Returns true if this sink is interested in the stack traces.
        /// </summary>
        /// <returns>Returns true if this sink is interested in the stack traces.</returns>
        public bool NeedsStackTrace()
        {
            return SystemConfig.CaptureStackTraces;
        }

        /// <summary>
        /// Set minimal log level that this Sink is interested in
        /// </summary>
        /// <param name="minimalLevel"><see cref="LogLevel"/> to set as a minimal level</param>
        public void SetMinimalLogLevel(LogLevel minimalLevel)
        {
            SystemConfig.MinLevel = minimalLevel;

            using var scopedLock = LogControllerScopedLock.Create(Handle);
            ref var logManager = ref scopedLock.GetLogController();
            logManager.MustBeValid();

            logManager.SetMinimalLogLevelForSink(SinkId, minimalLevel);
        }

        /// <summary>
        /// Get minimal log level that this Sink is interested in
        /// </summary>
        /// <returns>Minimal log level that this Sink is interested in</returns>
        public LogLevel GetMinimalLogLevel()
        {
            return SystemConfig.MinLevel;
        }

        /// <summary>
        /// If any error happens - this method will self-log it and set IsInitialized to false, disabling the sink
        /// </summary>
        /// <param name="reason">User facing error message</param>
        protected void OnSinkFatalError(FixedString512Bytes reason)
        {
            Internal.Debug.SelfLog.OnSinkFatalError(this, reason);

            IsInitialized = false;
        }
    }
}
