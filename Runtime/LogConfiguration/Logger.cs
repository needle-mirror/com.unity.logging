using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;

namespace Unity.Logging
{
    /// <summary>
    /// Main class of Logging library.
    /// Contains Sinks, unique identifier <see cref="LoggerHandle"/>
    /// </summary>
    public class Logger : IDisposable
    {
        /// <summary>
        /// Config that was used in Logger construction
        /// </summary>
        public readonly LoggerConfig Config;

        private readonly List<ISinkSystemInterface> m_Sinks;
        private bool m_HasNoSinks;
        private LogLevel m_MinimalLogLevelAcrossAllSinks = LogLevel.Fatal;

        /// <summary>
        /// Unique id
        /// </summary>
        public readonly LoggerHandle Handle;

        /// <summary>
        /// Minimal <see cref="LogLevel"/> that this <see cref="Logger"/> will process (means it has sinks for it)
        /// </summary>
        public LogLevel MinimalLogLevelAcrossAllSystems => m_MinimalLogLevelAcrossAllSinks;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Contains configuration of the logger</param>
        public Logger(LoggerConfig config)
        {
            ThreadGuard.AssertRunningOnMainThread();

            Config = config;

            Handle = LoggerManager.RegisterLogger(this, ref config.MemoryManagerParameters);

            Handle.MustBeValid();

            m_Sinks = new List<ISinkSystemInterface>(config.SinkConfigs.Count);
            foreach (var sc in config.SinkConfigs)
            {
                AddSink(sc);
            }

            UpdateMinimalLogLevelAcrossAllSinks();

            // call onNewLoggerCreatedEvent
            LoggerManager.LoggerCreated(this);

            if (LoggerManager.Logger == null)
                LoggerManager.Logger = this;
        }

        /// <summary>
        /// Creates new sink using <see cref="SinkConfiguration"/>
        /// </summary>
        /// <param name="sc">Configuration to create a sink</param>
        /// <returns>Newly created sink</returns>
        public ISinkSystemInterface AddSink(SinkConfiguration sc)
        {
            Handle.MustBeValid();
            ThreadGuard.EnsureRunningOnMainThread();

            var sink = sc.CreateSinkInstance(this);
            m_Sinks.Add(sink);

            return sink;
        }

        /// <summary>
        /// Returns the sink of type T. Will return first one if there are several ones of the type T
        /// </summary>
        /// <typeparam name="T">ISinkSystemInterface sink type</typeparam>
        /// <returns>Existing sink of type T or default</returns>
        public T GetSink<T>() where T : ISinkSystemInterface, new()
        {
            Handle.MustBeValid();
            ThreadGuard.EnsureRunningOnMainThread();

            var n = SinksCount;
            for (int i = 0; i < n; i++)
            {
                var s = GetSink(i);

                if (s is T res)
                    return res;
            }

            return default;
        }

        /// <summary>
        /// Get or create sink of type T
        /// </summary>
        /// <param name="sc">Configuration of the sink</param>
        /// <typeparam name="T">ISinkSystemInterface</typeparam>
        /// <returns>Existing or created sink</returns>
        public T GetOrCreateSink<T>(SinkConfiguration<T> sc) where T : ISinkSystemInterface, new()
        {
            Handle.MustBeValid();
            ThreadGuard.EnsureRunningOnMainThread();

            var n = SinksCount;
            for (int i = 0; i < n; i++)
            {
                var s = GetSink(i);

                if (s is T res)
                    return res;
            }

            return (T)AddSink(sc);
        }

        /// <summary>
        /// Changes LogLevel for all sinks in the logger. No need to call <see cref="UpdateMinimalLogLevelAcrossAllSinks"/> after this.
        /// Update will take effect only after logger's update because of async nature of the logging. So if you want to do the change synchronously - please call <see cref="LoggerManager.FlushAll"/>>
        /// </summary>
        /// <param name="newLogLevel">LogLevel to set to all sinks in the logger</param>
        public void SetMinimalLogLevelAcrossAllSinks(LogLevel newLogLevel)
        {
            Handle.MustBeValid();
            ThreadGuard.EnsureRunningOnMainThread();

            var n = m_Sinks.Count;
            for (var i = 0; i < n; i++)
            {
                m_Sinks[i].SetMinimalLogLevel(newLogLevel);
            }
            m_MinimalLogLevelAcrossAllSinks = newLogLevel;

            using var scopedLock = LogControllerScopedLock.Create(Handle);
            ref var logManager = ref scopedLock.GetLogController();
            logManager.MustBeValid();
            logManager.HasSink = HasSinkStruct.FromLogger(this);
        }

        /// <summary>
        /// Method that updates internal cached MinimalLogLevelAcrossAllSinks, and HasNoSinks please call it if you update sink's MinimalLogLevel or add/remove sinks
        /// Update will take effect only after logger's update because of async nature of the logging. So if you want to do the change synchronously - please call <see cref="LoggerManager.FlushAll"/>>
        /// </summary>
        public void UpdateMinimalLogLevelAcrossAllSinks()
        {
            Handle.MustBeValid();
            ThreadGuard.EnsureRunningOnMainThread();

            m_MinimalLogLevelAcrossAllSinks = LogLevel.Fatal;
            var n = m_Sinks.Count;
            m_HasNoSinks = n == 0;
            var captureStackTrace = false;
            for (var i = 0; i < n; i++)
            {
                var sinkLevel = m_Sinks[i].GetMinimalLogLevel();
                if (m_MinimalLogLevelAcrossAllSinks > sinkLevel)
                    m_MinimalLogLevelAcrossAllSinks = sinkLevel;
                captureStackTrace = captureStackTrace || m_Sinks[i].NeedsStackTrace();
            }

            using var scopedLock = LogControllerScopedLock.Create(Handle);
            ref var logManager = ref scopedLock.GetLogController();
            logManager.MustBeValid();
            logManager.HasSink = HasSinkStruct.FromLogger(this);
            logManager.NeedsStackTraceByte = (byte)(captureStackTrace ? 1 : 0);
        }

        /// <summary>
        /// Returns count of sinks that this Logger has
        /// </summary>
        public int SinksCount => m_Sinks.Count;

        /// <summary>
        /// Returns sink number i. Used for debugging
        /// </summary>
        /// <param name="i">index of the sink</param>
        /// <returns>Returns sink number i</returns>
        public ISinkSystemInterface GetSink(int i) => m_Sinks[i];

        /// <summary>
        /// Any in-flight Update will be tracked in here
        /// </summary>
        private JobHandle m_CurrentUpdateJobHandle;

        /// <summary>
        /// Disposes the Logger. See <see cref="IDisposable"/>
        /// </summary>
        public void Dispose()
        {
            ThreadGuard.EnsureRunningOnMainThread();
            Handle.MustBeValid();

            m_CurrentUpdateJobHandle.Complete();

            LoggerManager.OnDispose(this);

            if (m_Sinks != null)
            {
                for (var i = m_Sinks.Count - 1; i >= 0; i--)
                    m_Sinks[i].Dispose();
            }
        }

        /// <summary>
        /// Returns true if this Logger can process messages with <see cref="LogLevel"/> level.
        /// </summary>
        /// <param name="level"></param>
        /// <returns>True if this Logger can process messages with <see cref="LogLevel"/> level.</returns>
        public bool HasSinksFor(LogLevel level)
        {
            if (m_HasNoSinks)
                return false;

            //  res     level       min
            //  0       ver         info
            //  0       debug       info
            //  1       info        info
            //  1       warn        info
            //  1       error       info
            //  1       fatal       info

            return level >= m_MinimalLogLevelAcrossAllSinks;
        }

        /// <summary>
        /// Schedules Update job for this Logger. It will sort all messages by timestamp, then call all Sinks in parallel.
        /// After all sinks are done: Cleanup job that will clean and flip <see cref="DispatchQueue"/> will be called.
        /// And then <see cref="LogMemoryManager"/>'s Update job will be executed.
        /// </summary>
        /// <param name="dependency">Any dependency that should be executed before this Update</param>
        /// <returns>JobHandle for the Update job</returns>
        public JobHandle ScheduleUpdate(JobHandle dependency)
        {
            ThreadGuard.AssertRunningOnMainThread();
            Handle.MustBeValid();

            var @lock = LogControllerScopedLock.Create(Handle);

            dependency = JobHandle.CombineDependencies(m_CurrentUpdateJobHandle, dependency);

            // external dependency -> sort -> all sinks in parallel -> cleanup -> logManagerUpdate
            //                             -> all sinks in parallel ->

            var sortJobHandle = new SortTimestampsJob { Lock = @lock }.Schedule(dependency);

            var chain = sortJobHandle;
            // run sinks in parallel after 'dependency' is done.
            foreach (var sink in m_Sinks)
                chain = JobHandle.CombineDependencies(chain, sink.ScheduleUpdate(@lock, sortJobHandle));

            m_CurrentUpdateJobHandle = new CleanupUpdateDisposeLockJob { LockToDispose = @lock }.Schedule(chain);

            return m_CurrentUpdateJobHandle;
        }

        internal JobHandle ScheduleUpdateWithoutLock(JobHandle dependency)
        {
            ThreadGuard.AssertRunningOnMainThread();
            Handle.MustBeValid();
            LogControllerWrapper.MustBeReadLocked(Handle);

            dependency = JobHandle.CombineDependencies(m_CurrentUpdateJobHandle, dependency);

            var @lock = LogControllerScopedLock.CreateAlreadyUnderLock(Handle);

            // external dependency -> sort -> all sinks in parallel -> cleanup -> logManagerUpdate
            //                             -> all sinks in parallel ->

            var sortJobHandle = new SortTimestampsJob { Lock = @lock }.Schedule(dependency);

            var chain = sortJobHandle;
            // run sinks in parallel after 'dependency' is done.
            foreach (var sink in m_Sinks)
                chain = JobHandle.CombineDependencies(chain, sink.ScheduleUpdate(@lock, sortJobHandle));

            m_CurrentUpdateJobHandle = new CleanupUpdateDisposeLockJob { LockToDispose = @lock }.Schedule(chain);

            return m_CurrentUpdateJobHandle;
        }

        [BurstCompile]
        private struct SortTimestampsJob : IJob
        {
            [ReadOnly] public LogControllerScopedLock Lock;

            public void Execute()
            {
                ref var logController = ref Lock.GetLogController();
                logController.DispatchQueue.Sort();
            }
        }

        [BurstCompile]
        private struct CleanupUpdateDisposeLockJob : IJob
        {
            public LogControllerScopedLock LockToDispose;

            public void Execute()
            {
                try
                {
                    ref var logController = ref LockToDispose.GetLogController();

                    try
                    {
                        var reader = logController.DispatchQueue.BeginRead();

                        var n = reader.Length;

                        for (int position = 0; position < n; ++position)
                        {
                            unsafe
                            {
                                var elem = UnsafeUtility.ReadArrayElement<LogMessage>(reader.Ptr, position);

                                //
                                // TODO: Add check to see if we need to force release of a Payload buffer to safeguard against
                                // poorly behaving Systems, i.e. a Sink fails to release a lock, don't want to "leak" buffers.
                                //
                                bool shouldForce = false;

                                // Attempt to release the buffer, and if it's not Locked, then delete the Entity referencing it
                                // NOTE: Buffer may have been manually released by some other system and need to still remove the data
                                logController.MemoryManager.ReleasePayloadBuffer(elem.Payload, out var result, shouldForce);

                                ManagedStackTraceWrapper.Free(elem.StackTraceId);
                            }
                        }
                    }
                    finally
                    {
                        logController.DispatchQueue.EndReadClearAndFlip();
                    }

                    logController.MemoryManager.Update();
                }
                finally
                {
                    LockToDispose.Dispose();
                }
            }
        }

        /// <summary>
        /// Force release all decorations
        /// </summary>
        internal void DisposeAllDecorators()
        {
            using var scopedLock = LogControllerScopedLock.Create(Handle);
            ref var logController = ref scopedLock.GetLogController();
            logController.DisposeAllDecorators();
        }
    }
}
