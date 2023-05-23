#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Manager of the loggers. Usually shouldn't be accessed directly, but via codegenerated Log. calls.
    /// </summary>
    public static class LoggerManager
    {
        /// <summary>
        /// Defines a delegate to handle Decorate calls
        /// </summary>
        /// <param name="ctx">LogContextWithDecorator that can be used to add new parameters to the log message</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OutputWriterDecorateHandler(ref LogContextWithDecorator ctx);

        private struct LoggerHandleKey {}
        private struct JobHandleKey {}
        private struct TextLoggerDecoratorKey {}
        private struct GlobalMemoryManagerKey {}
        private struct GlobalHandlesKey {}

        private static readonly SharedStatic<LoggerHandle> s_CurrentLoggerHandle = SharedStatic<LoggerHandle>.GetOrCreate<LoggerHandle, LoggerHandleKey>(16);
        private static readonly SharedStatic<JobHandle> s_LastLogUpdate = SharedStatic<JobHandle>.GetOrCreate<JobHandle, JobHandleKey>(16);
        private static readonly SharedStatic<ThreadSafeFuncList<OutputWriterDecorateHandler>> s_OutputWriterDecorateHandlers =
            SharedStatic<ThreadSafeFuncList<OutputWriterDecorateHandler>>.GetOrCreate<ThreadSafeFuncList<OutputWriterDecorateHandler>, TextLoggerDecoratorKey>(16);
        private static readonly SharedStatic<LogMemoryManager> s_GlobalMemoryManager = SharedStatic<LogMemoryManager>.GetOrCreate<LogMemoryManager, GlobalMemoryManagerKey>(16);

        private static readonly SharedStatic<ThreadSafeList4096<PayloadHandle>> s_GlobalDecoratePayloadHandles = SharedStatic<ThreadSafeList4096<PayloadHandle>>.GetOrCreate<ThreadSafeList4096<PayloadHandle>, GlobalHandlesKey>(16);

        private static Logger s_CurrentLogger;

        private static List<Logger> s_OtherLoggers;

        private static List<Logger> OtherLoggers
        {
            get
            {
                if (s_OtherLoggers == null)
                    s_OtherLoggers = new List<Logger>(8);
                return s_OtherLoggers;
            }
        }

        /// <summary>
        /// This function can be used for putting a breakpoint on any OutputWriterHandler that are not easy to access, since they come from sourcegen
        /// NOTE: If breakpoint doesn't work - try to disable burst
        /// </summary>
        //[Conditional("UNITY_EDITOR")]
        public static void DebugBreakpointPlaceForOutputWriterHandlers()
        {
            //Baselib.LowLevel.Binding.Baselib_Thread_YieldExecution();
        }

        private static byte s_Initialized = 0;
        /// <summary>
        /// This function usually executed from the static constructors to make sure all resources are allocated and ready for the logging system. You shouldn't call it directly.
        /// </summary>
        [BurstDiscard]
        public static void Initialize()
        {
            if (s_Initialized != 0) return;
                s_Initialized = 1;

            ThreadGuard.InitializeFromMainThread();

            BurstHelper.CheckThatBurstIsEnabled(false);
            PerThreadData.Initialize();
            ManagedStackTraceWrapper.Initialize();
            TimeStampWrapper.Initialize();
            Builder.Initialize();
#if !UNITY_DOTSRUNTIME
            UnityLogRedirectorManager.Initialize();
#endif
        }

        /// <summary>
        /// Currently active logger's handle. Log.Info(...) and other calls will go to this one.
        /// Used by codegen
        /// </summary>
        public static LoggerHandle CurrentLoggerHandle => s_CurrentLoggerHandle.Data;

        private static event Action<Logger> m_onNewLoggerCreatedEvent;

        /// <summary>
        /// Gets/Sets the current active logger.
        /// Log.Info(...) and other calls will use the current one.
        /// Used by codegen. User sees it as Log.Logger = ...
        /// </summary>
        public static Logger Logger
        {
            get => s_CurrentLogger;

            set
            {
                ThreadGuard.EnsureRunningOnMainThread();

                if (s_CurrentLogger == value)
                    return;

                if (s_CurrentLogger != null)
                {
                    OtherLoggers.Add(s_CurrentLogger);
                }

                // if new logger is not null
                //  -- find it in OtherLoggers and remove from there
                if (value != null)
                {
                    value.Handle.MustBeValid();

                    s_CurrentLoggerHandle.Data = value.Handle;

                    var indx = OtherLoggers.IndexOf(value);
                    if (indx != -1)
                        OtherLoggers.RemoveAtSwapBack(indx);
                }
                else
                {
                    s_CurrentLoggerHandle.Data = default;
                }

                s_CurrentLogger = value;
            }
        }

        /// <summary>
        /// Schedules Update jobs for all loggers on a worker thread. If another <see cref="ScheduleUpdateLoggers"/> is in progress - will add its JobHandle as a dependency.
        /// </summary>
        /// <param name="dependency">Dependencies are used to ensure that a job executes on worker threads after the dependency has completed execution. Please, make sure that two jobs that read or write to the same data don't run in parallel.</param>
        /// <returns>The handle identifying the scheduled job. Can be used as a dependency for a later job or ensure completion on the main thread.</returns>
        public static JobHandle ScheduleUpdateLoggers(JobHandle dependency = default)
        {
            if (s_CurrentLogger == null && OtherLoggers.Count == 0)
                return dependency;

            var allPreviousUpdatesComplete = JobHandle.CombineDependencies(s_LastLogUpdate.Data, dependency);

            var allLoggerComplete = allPreviousUpdatesComplete;

            LogControllerWrapper.LockRead();

            // start all loggers in parallel
            if (s_CurrentLogger != null)
            {
                allLoggerComplete = s_CurrentLogger.ScheduleUpdateWithoutLock(allPreviousUpdatesComplete);
            }

            foreach (var logger in OtherLoggers)
            {
                allLoggerComplete = JobHandle.CombineDependencies(allLoggerComplete, logger.ScheduleUpdateWithoutLock(allPreviousUpdatesComplete));
            }

            // at the end of loggers - update mem manager, run console flush and UnlockRead
            s_LastLogUpdate.Data = new AfterUpdateLoggersJob().Schedule(allLoggerComplete);

            return s_LastLogUpdate.Data;
        }

        /// <summary>
        /// Completes previously scheduled <see cref="ScheduleUpdateLoggers"/>
        /// </summary>
        public static void CompleteUpdateLoggers()
        {
            s_LastLogUpdate.Data.Complete();
        }

        internal static void OnDispose(in Logger logger)
        {
            CompleteUpdateLoggers();

            if (Logger == logger)
                Logger = null;

            var indx = OtherLoggers.IndexOf(logger);
            if (indx != -1)
                OtherLoggers.RemoveAtSwapBack(indx);

            LogControllerWrapper.Remove(logger.Handle);
#if !UNITY_DOTSRUNTIME
            if (logger.Config.GetRedirectUnityLogs())
                UnityLogRedirectorManager.EndRedirection(logger);
#endif
        }

        /// <summary>
        /// Call <see cref="ScheduleUpdateLoggers"/> two times (because of double-buffering) and <see cref="CompleteUpdateLoggers"/> to make sure all logs were processed.
        /// </summary>
        public static void FlushAll()
        {
            if (BurstHelper.IsManaged && ThreadGuard.IsMainThread())
            {
                // burst cannot do next calls because they involve classes / managed types
                // only main thread can do the next calls because if will spawn jobs
                ScheduleUpdateLoggers();
                ScheduleUpdateLoggers(); // make sure all double buffers are processed
                CompleteUpdateLoggers();
            }
            else
            {
                try
                {
                    LogControllerWrapper.LockRead();

                    var n = LogControllerWrapper.GetLogControllersCount();
                    for (var i = 0; i < n; i++)
                    {
                        ref var lc = ref LogControllerWrapper.GetLogControllerByIndexUnderLock(i);
                        lc.FlushSync();
                    }
                }
                finally
                {
                    LogControllerWrapper.UnlockRead();
                }
            }
        }

        /// <summary>
        /// Calls <see cref="FlushAll"/> and completes all in-flight <see cref="ScheduleUpdateLoggers"/>. Shutdowns all loggers and makes sure their state were valid.
        /// </summary>
        public static void DeleteAllLoggers()
        {
            ThreadGuard.EnsureRunningOnMainThread();
            CompleteUpdateLoggers();

            Logger = null;

            FlushAll();

            ShutdownAllGlobalDecorators();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            var errorLoggers = 0;
            var errorMessage = new NativeText(4096, Allocator.Temp);
            ref var gmem = ref GetGlobalDecoratorMemoryManager();
            if (gmem.GetCurrentDefaultBufferUsage() != 0)
            {
                ++errorLoggers;
                errorMessage.Append(gmem.DebugStateString("Global"));
                errorMessage.Append('\n');
            }
#endif

            for (var i = OtherLoggers.Count - 1; i >= 0; i--)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                {
                    using var scopedLock = LogControllerScopedLock.Create(OtherLoggers[i].Handle);
                    ref var lc = ref scopedLock.GetLogController();

                    var usage = lc.MemoryManager.GetCurrentDefaultBufferUsage();
                    if (usage != 0)
                    {
                        ++errorLoggers;
                        var name = (FixedString128Bytes)"OtherLoggers["; name.Append(i); name.Append(']');
                        errorMessage.Append(lc.MemoryManager.DebugStateString(name));
                        errorMessage.Append('\n');
                    }
                }
#endif
                OtherLoggers[i].Dispose();
            }

            OtherLoggers.Clear();

            LogControllerWrapper.ShutdownAll();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            var str = errorMessage.ToString();
            errorMessage.Dispose();

            Assert.AreEqual(0, errorLoggers, str);

            // Usage checks will have re-created the memory manager, so shut it down again.
            ShutdownAllGlobalDecorators();
#endif
        }

        private static void ShutdownAllGlobalDecorators()
        {
            if (s_GlobalMemoryManager.Data.IsInitialized)
                s_GlobalMemoryManager.Data.Shutdown();

            s_GlobalDecoratePayloadHandles.Data.Clear();
            s_OutputWriterDecorateHandlers.Data.Clear();
        }

        /// <summary>
        /// Adds an action to call on any new logger creation
        /// <seealso cref="CallForEveryLogger"/>
        /// </summary>
        /// <param name="callback">Action to execute on any new logger</param>
        public static void OnNewLoggerCreated(Action<Logger> callback)
        {
            if (callback == null) return;

            ThreadGuard.EnsureRunningOnMainThread();

            m_onNewLoggerCreatedEvent += callback;
        }

        /// <summary>
        /// Calls an action for every existing logger.
        /// <seealso cref="OnNewLoggerCreated"/>
        /// </summary>
        /// <param name="callback">Action to execute on all existing loggers</param>
        public static void CallForEveryLogger(Action<Logger> callback)
        {
            if (callback == null) return;

            ThreadGuard.EnsureRunningOnMainThread();

            if (s_CurrentLogger != null)
                callback(s_CurrentLogger);

            foreach (var logger in OtherLoggers)
                if (logger != null)
                    callback(logger);
        }

        /// <summary>
        /// Debug function to assert that there is no locks currently held on <see cref="LogControllerWrapper"/>
        /// </summary>
        public static void AssertNoLocks()
        {
            LogControllerWrapper.AssertNoLocks();
        }

        /// <summary>
        /// Internal call that is called on new Logger creation. Completes all in-flight loggers' update jobs.
        /// </summary>
        /// <param name="logger">Newly created <see cref="Logger"/></param>
        /// <param name="memoryManagerParameters">Reference with the memory manager settings</param>
        /// <returns>The handle of newly created <see cref="Logger"/></returns>
        internal static LoggerHandle RegisterLogger(in Logger logger, ref LogMemoryManagerParameters memoryManagerParameters)
        {
            CompleteUpdateLoggers();

            var handle = LogControllerWrapper.Create(ref memoryManagerParameters);

            OtherLoggers.Add(logger);

            return handle;
        }

        /// <summary>
        /// Gets logger by its <see cref="LoggerHandle"/>
        /// </summary>
        /// <param name="loggerHandle">Handle</param>
        /// <returns>Logger that has the handle</returns>
        public static Logger GetLogger(in LoggerHandle loggerHandle)
        {
            if (loggerHandle.IsValid == false)
                return null;

            ThreadGuard.EnsureRunningOnMainThread();

            if (s_CurrentLogger != null && s_CurrentLogger.Handle.Value == loggerHandle.Value)
                return s_CurrentLogger;

            foreach (var logger in OtherLoggers)
            {
                if (logger.Handle.Value == loggerHandle.Value)
                    return logger;
            }

            return null;
        }

        /// <summary>
        /// Debug function to get all currently dispatched messages across all loggers
        /// </summary>
        /// <returns>Number of all currently dispatched messages across all loggers</returns>
        public static int GetTotalDispatchedMessages()
        {
            return LogControllerWrapper.GetTotalDispatchedMessages();
        }

        /// <summary>
        /// Get global decorator's <see cref="LogMemoryManager"/>
        /// Used by the codegen
        /// </summary>
        /// <returns>Global decorator's <see cref="LogMemoryManager"/></returns>
        public static ref LogMemoryManager GetGlobalDecoratorMemoryManager()
        {
            if (s_GlobalMemoryManager.Data.IsInitialized == false)
                s_GlobalMemoryManager.Data.Initialize();

            return ref s_GlobalMemoryManager.Data;
        }

        /// <summary>
        /// Provides a list of global constant decorations. <see cref="EndEditDecoratePayloadHandles"/> must be called afterwards.
        /// Used by the codegen
        /// </summary>
        /// <param name="nBefore">Current count of list of global constant decorations</param>
        /// <returns>The list of global constant decorations</returns>
        public static LogContextWithDecorator BeginEditDecoratePayloadHandles(out int nBefore)
        {
            nBefore = s_GlobalDecoratePayloadHandles.Data.BeginWrite();

            ref var list = ref ThreadSafeList4096<PayloadHandle>.GetReferenceNotThreadSafe(ref s_GlobalDecoratePayloadHandles.Data);

            unsafe
            {
                fixed (FixedList4096Bytes<PayloadHandle>* ptr = &list)
                {
                    return new LogContextWithDecorator(ptr);
                }
            }
        }

        /// <summary>
        /// Call after <see cref="BeginEditDecoratePayloadHandles"/> is done
        /// Used by the codegen
        /// </summary>
        /// <param name="nBefore">nBefore from <see cref="BeginEditDecoratePayloadHandles"/></param>
        /// <returns>The list of added handles during Begin-End scope</returns>
        public static FixedList64Bytes<PayloadHandle> EndEditDecoratePayloadHandles(int nBefore)
        {
            var nAfter = s_GlobalDecoratePayloadHandles.Data.Length;

            var payloadHandles = new FixedList64Bytes<PayloadHandle>();
            for (var i = nBefore; i < nAfter; ++i)
                payloadHandles.Add(s_GlobalDecoratePayloadHandles.Data.ElementAt(i));

            s_GlobalDecoratePayloadHandles.Data.EndWrite();

            return payloadHandles;
        }

        /// <summary>
        /// Adds global Function-based Decorator.
        /// Used by the codegen
        /// <seealso cref="RemoveDecorateHandler"/>
        /// </summary>
        /// <param name="func">Function-based Decorator to add</param>
        public static void AddDecorateHandler(FunctionPointer<OutputWriterDecorateHandler> func)
        {
            s_OutputWriterDecorateHandlers.Data.Add(func);
        }

        /// <summary>
        /// Removes global Function-based Decorator.
        /// Used by the codegen
        /// <seealso cref="AddDecorateHandler"/>
        /// </summary>
        /// <param name="func">Function-based Decorator to remove</param>
        public static void RemoveDecorateHandler(FunctionPointer<OutputWriterDecorateHandler> func)
        {
            s_OutputWriterDecorateHandlers.Data.Remove(func.Value);
        }

        /// <summary>
        /// Returns count of global constant Decorators (that you can add with Log.Decorate("name", value))
        /// They will be copied to new log message payload each time log message is created for any logger
        /// </summary>
        /// <returns>Count of global constant Decorators</returns>
        public static int GlobalDecoratePayloadsCount()
        {
            return s_GlobalDecoratePayloadHandles.Data.Length;
        }

        /// <summary>
        /// Returns count of global Function-based Decorators (that you can add with Log.Decorate(function) or <see cref="AddDecorateHandler"/> and remove with <see cref="RemoveDecorateHandler"/>)
        /// They will be executed each time log message is created for any Logger
        /// </summary>
        /// <returns>Count of global Function-based Decorators</returns>
        public static int GlobalDecorateHandlerCount()
        {
            return s_OutputWriterDecorateHandlers.Data.Length;
        }

        /// <summary>
        /// Executes all global function based decorator handlers that were added with with Log.Decorate(function) or <see cref="AddDecorateHandler"/> and remove with <see cref="RemoveDecorateHandler"/>
        /// </summary>
        /// <param name="handles">Where to add decorations</param>
        /// <returns>Count of added decorations</returns>
        internal static ushort ExecuteDecorateHandlers(ref LogContextWithDecorator handles)
        {
            var lengthBefore = handles.Length;

            ref var decors = ref s_OutputWriterDecorateHandlers.Data;

            try
            {
                unsafe
                {
                    var n = decors.BeginRead();
                    for (var i = 0; i < n; i++)
                    {
                        ref var func = ref decors.ElementAt(i);
#if LOGGING_USE_UNMANAGED_DELEGATES
                        ((delegate * unmanaged[Cdecl] <ref LogContextWithDecorator, void>)func.Value)(ref handles);
#else
                        func.Invoke(ref handles);
#endif
                    }
                }
            }
            finally
            {
                decors.EndRead();
            }

            return (ushort)(handles.Length - lengthBefore);
        }

        /// <summary>
        /// Adds all global const decorator handlers that were added with with Log.Decorate("name", value)
        /// </summary>
        /// <param name="handles">Where to add decorations</param>
        /// <returns>Count of added decorations-payloads</returns>
        internal static ushort AddConstDecorateHandlers(LogContextWithDecorator handles)
        {
            ref var lc = ref handles.Lock.GetLogController();

            try
            {
                var n = s_GlobalDecoratePayloadHandles.Data.BeginRead();
                for (var i = 0; i < n; i++)
                {
                    var handleCopy = s_GlobalMemoryManager.Data.Copy(ref lc.MemoryManager, s_GlobalDecoratePayloadHandles.Data.ElementAt(i));

                    handles.Add(handleCopy);
                }
                return (ushort)n;
            }
            finally
            {
                s_GlobalDecoratePayloadHandles.Data.EndRead();
            }
        }

        /// <summary>
        /// Debug function to write debug info about log controllers and their message queues
        /// </summary>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
        public static void DebugPrintQueueInfos()
        {
            LogControllerWrapper.DebugPrintQueueInfos(s_CurrentLoggerHandle.Data);
        }

        /// <summary>
        /// When the decorator is not active anymore - resources should be freed. This method calls ReleasePayloadBufferDeferred and removes the payloads from decor list
        /// </summary>
        /// <param name="handle">LoggerHandle to get the LogController. Global if not valid</param>
        /// <param name="payloadHandles">Payload to release in 2 frames</param>
        public static void ReleaseDecoratePayloadBufferDeferred(LoggerHandle handle, FixedList64Bytes<PayloadHandle> payloadHandles)
        {
            if (handle.IsValid)
            {
                using var scopedLock = LogControllerScopedLock.Create(handle);
                ref var lc = ref scopedLock.GetLogController();
                lc.RemoveDecoratePayloadHandles(payloadHandles);

                ref var mem = ref lc.MemoryManager;
                foreach (var h in payloadHandles)
                    mem.ReleasePayloadBufferDeferred(h);
            }
            else
            {
                s_GlobalDecoratePayloadHandles.Data.Remove(payloadHandles);

                ref var mem = ref GetGlobalDecoratorMemoryManager();
                foreach (var h in payloadHandles)
                    mem.ReleasePayloadBufferDeferred(h);
            }
        }

        /// <summary>
        /// When new logger is created - the event is invoked.
        /// <seealso cref="OnNewLoggerCreated"/>
        /// </summary>
        /// <param name="logger">Just created <see cref="Logger"/></param>
        internal static void LoggerCreated(Logger logger)
        {
            m_onNewLoggerCreatedEvent?.Invoke(logger);
        }

        /// <summary>
        /// Clears <see cref="OnNewLoggerCreated"/> event subscribers list.
        /// <seealso cref="OnNewLoggerCreated"/>
        /// </summary>
        public static void ClearOnNewLoggerCreatedEvent()
        {
            m_onNewLoggerCreatedEvent = null;
        }

        /// <summary>
        /// If there is no current logger - create a default one
        /// </summary>
        public static void CreateDefaultLoggerIfNone()
        {
            if (CurrentLoggerHandle.IsValid == false)
                DefaultSettings.CreateDefaultLogger();
        }
    }

    /// <summary>
    /// Job that the update logger's logic executes last.
    /// </summary>
    [BurstCompile]
    internal struct AfterUpdateLoggersJob : IJob
    {
        public void Execute()
        {
#pragma warning disable 0169
            // ReSharper disable once NotAccessedVariable
            var burstBugWorkaround = 42;
            try
            {
                LoggerManager.GetGlobalDecoratorMemoryManager().Update();
                Console.Flush();
            }
            finally
            {
                LogControllerWrapper.UnlockRead();
            }

            ++burstBugWorkaround;
#pragma warning restore 0169
        }
    }
}
