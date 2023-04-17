using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

        private readonly List<SinkSystemBase> m_Sinks;
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

        static Logger()
        {
            LoggerManager.Initialize();
        }

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

            m_Sinks = new List<SinkSystemBase>(config.SinkConfigs.Count);

            LogControllerScopedLock @lock = default;
            try
            {
                @lock = LogControllerScopedLock.Create(Handle);

                foreach (var sc in config.SinkConfigs)
                {
                    AddSink(sc, ref @lock);
                }

                @lock.GetLogController().SyncMode = config.SyncMode.Get;
            }
            finally
            {
                @lock.Dispose();
            }

            UpdateMinimalLogLevelAcrossAllSinks();

            // call onNewLoggerCreatedEvent
            LoggerManager.LoggerCreated(this);

            if (LoggerManager.Logger == null)
                LoggerManager.Logger = this;

#if UNITY_STARTUP_LOGS_API
            if (config.GetRetrieveStartupLogs())
                UnityLogs.RetrieveStartupLogs(this);
#endif
#if !UNITY_DOTSRUNTIME
            if (config.GetRedirectUnityLogs())
                UnityLogs.RedirectUnityLogs(this);
#endif
        }

        /// <summary>
        /// Creates new sink using <see cref="SinkConfiguration"/>
        /// </summary>
        /// <param name="sc">Configuration to create a sink</param>
        /// <returns>Newly created sink</returns>
        public SinkSystemBase AddSink(SinkConfiguration sc)
        {
            var defaultLock = default(LogControllerScopedLock);
            return AddSink(sc, ref defaultLock);
        }

        /// <summary>
        /// Creates new sink using <see cref="SinkConfiguration"/>
        /// </summary>
        /// <param name="sc">Configuration to create a sink</param>
        /// <param name="logLock">Lock that holds this logger's LogController</param>
        /// <returns>Newly created sink</returns>
        public SinkSystemBase AddSink(SinkConfiguration sc, ref LogControllerScopedLock logLock)
        {
            Handle.MustBeValid();
            ThreadGuard.EnsureRunningOnMainThread();
            ThrowIfThisIsWrongLock(ref logLock);

            var sink = sc.CreateSinkInstance(this);
            m_Sinks.Add(sink);

            if (logLock.IsValid)
            {
                ref var lc = ref logLock.GetLogController();
                lc.AddSinkStruct(sink);
            }
            else
            {
                LogControllerScopedLock @lock = default;
                try
                {
                    @lock = LogControllerScopedLock.Create(Handle);

                    ref var lc = ref @lock.GetLogController();
                    lc.AddSinkStruct(sink);
                }
                finally
                {
                    @lock.Dispose();
                }
            }

            return sink;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private void ThrowIfThisIsWrongLock(ref LogControllerScopedLock logLock)
        {
            if (logLock.Handle.IsValid && logLock.Handle.Value != Handle.Value)
                throw new Exception("LogControllerScopedLock is valid, but from the wrong Logger");
        }

        /// <summary>
        /// Returns the sink of type T. Will return first one if there are several ones of the type T
        /// </summary>
        /// <typeparam name="T">SinkSystemBase type</typeparam>
        /// <returns>Existing sink of type T or default</returns>
        public T GetSink<T>() where T : SinkSystemBase
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
        /// <typeparam name="T">SinkSystemBase</typeparam>
        /// <returns>Existing or created sink</returns>
        public T GetOrCreateSink<T>(SinkConfiguration sc) where T : SinkSystemBase
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
        public SinkSystemBase GetSink(int i) => m_Sinks[i];

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
            Logging.Internal.LoggerManager.FlushAll();

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
        /// Does Sync update if SyncMode is set to FullSync.
        /// Also creates a lock struct that contains LogController that can be used afterwards in async update logic
        /// </summary>
        /// <param name="lock">LogController lock that will be created. Doesn't own the lock</param>
        /// <param name="loggerHandle">Logger handle to update</param>
        /// <returns>Returns true if need to update async. False if everything was done already</returns>
        [BurstCompile]
        private static bool UpdateFullSync(out LogControllerScopedLock @lock, LoggerHandle loggerHandle)
        {
            @lock = LogControllerScopedLock.CreateAlreadyUnderLock(loggerHandle);
            ref var lc = ref @lock.GetLogController();

            if (lc.SyncMode == SyncMode.FullSync)
            {
                lc.MemoryManager.Update();

                return false;
            }

            return true;
        }

        internal JobHandle ScheduleUpdateWithoutLock(JobHandle dependency)
        {
            ThreadGuard.AssertRunningOnMainThread();
            Handle.MustBeValid();
            LogControllerWrapper.MustBeReadLocked(Handle);

            dependency = JobHandle.CombineDependencies(m_CurrentUpdateJobHandle, dependency);

            var needToUpdateAsync = UpdateFullSync(out var @lock, Handle);

            if (needToUpdateAsync)
            {
                // external dependency -> sort -> all sinks in parallel -> cleanup -> logManagerUpdate
                //                             -> all sinks in parallel ->

                // Note: In case of FatalIsSync  FlushSync can be called in between sinks.
                // Example:
                // This update is scheduled, Sort is done, Sink A ran
                //      -- FlushSync happens from Log.Fatal --
                //         Sink A should be skipped for the 'read' queue in the DispatchQueue
                //           - This is done via LastTimestamp in SinkStruct
                //         Sink A should handle 'write' queue in the DispatchQueue
                //         Sink B should run on both, since it didn't run during update
                //         Cleanup, clear of all dispatch queue
                //      -- FlushSync is done
                // Sink B, Cleanup are running on the empty queue, doing nothing
                // log manager update

                var sortJobHandle = new SortTimestampsJob { Lock = @lock }.Schedule(dependency);

                var chain = sortJobHandle;
                // run sinks in parallel after 'dependency' is done.
                foreach (var sink in m_Sinks)
                    chain = JobHandle.CombineDependencies(chain, sink.ScheduleUpdate(@lock, sortJobHandle));

                m_CurrentUpdateJobHandle = new CleanupUpdateJob { Lock = @lock }.Schedule(chain);
                return m_CurrentUpdateJobHandle;
            }

            // sync update, so dependency should be completed here
            dependency.Complete();
            return default;
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
        internal struct CleanupUpdateJob : IJob
        {
            public LogControllerScopedLock Lock;

            public void Execute()
            {
                ref var logController = ref Lock.GetLogController();
                try
                {
                    try
                    {
                        var reader = logController.DispatchQueue.BeginReadExclusive();

                        var n = reader.Length;

                        for (int position = 0; position < n; ++position)
                        {
                            unsafe
                            {
                                var elem = UnsafeUtility.ReadArrayElement<LogMessage>(reader.Ptr, position);

                                ReleaseLogMessage(ref logController.MemoryManager, ref elem);
                            }
                        }
                    }
                    finally
                    {
                        logController.DispatchQueue.EndReadExclusiveClearAndFlip();
                    }
                }
                finally
                {
                    logController.MemoryManager.Update();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static void ReleaseLogMessage(ref LogMemoryManager memoryManager, ref LogMessage elem)
            {
                //
                // TODO: Add check to see if we need to force release of a Payload buffer to safeguard against
                // poorly behaving Systems, i.e. a Sink fails to release a lock, don't want to "leak" buffers.
                //
                var shouldForce = false;

                // Attempt to release the buffer, and if it's not Locked, then delete the Entity referencing it
                // NOTE: Buffer may have been manually released by some other system and need to still remove the data
                var success = memoryManager.ReleasePayloadBuffer(elem.Payload, out _, shouldForce);
                Assert.IsTrue(success);

                ManagedStackTraceWrapper.Free(elem.StackTraceId);
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
