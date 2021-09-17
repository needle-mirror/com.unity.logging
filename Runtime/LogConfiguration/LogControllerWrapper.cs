//#define DEBUG_DEADLOCKS
//#define USE_BASELIB

#if ENABLE_UNITY_COLLECTIONS_CHECKS
#define DEBUG_ADDITIONAL_CHECKS
#endif

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Lock that controls any <see cref="LogControllerWrapper"/> changes. This ensures that <see cref="LogController"/> and its <see cref="LogMemoryManager"/> are valid during the lock
    /// </summary>
    public readonly struct LogControllerScopedLock : IDisposable
    {
        /// <summary>
        /// <see cref="LoggerHandle"/> of the lock's <see cref="LogController"/>
        /// </summary>
        public readonly LoggerHandle Handle;

        private readonly int m_Index;
        private readonly byte m_OwnsLock;

        /// <summary>
        /// True if lock has a valid <see cref="LoggerHandle"/>, means it is connected to some <see cref="Logger"/>
        /// </summary>
        public bool IsValid => Handle.IsValid;

        /// <summary>
        /// True if this lock owns the lock operation and is responsible for the unlocking in Dispose()
        /// <seealso cref="CreateAlreadyUnderLock"/>
        /// </summary>
        public bool OwnsLock => m_OwnsLock != 0;

        /// <summary>
        /// Creates <see cref="LogControllerScopedLock"/> for the current <see cref="LoggerManager.CurrentLoggerHandle"/> logger. Owns the lock operation
        /// </summary>
        /// <returns>Created <see cref="LogControllerScopedLock"/></returns>
        public static LogControllerScopedLock Create()
        {
            LogControllerWrapper.LockRead();
            return new LogControllerScopedLock(LoggerManager.CurrentLoggerHandle);
        }

        /// <summary>
        /// Creates <see cref="LogControllerScopedLock"/> for the logger with <see cref="loggerHandle"/>. Owns the lock operation
        /// </summary>
        /// <param name="loggerHandle"><see cref="LoggerHandle"/> of the logger to create a lock on</param>
        /// <returns>Created <see cref="LogControllerScopedLock"/></returns>
        public static LogControllerScopedLock Create(LoggerHandle loggerHandle)
        {
            LogControllerWrapper.LockRead();
            return new LogControllerScopedLock(loggerHandle);
        }

        /// <summary>
        /// Creates <see cref="LogControllerScopedLock"/> for the logger with <see cref="loggerHandle"/>. But doesn't own the lock operation. Used in situations when lock is already taken
        /// <seealso cref="OwnsLock"/>
        /// </summary>
        /// <param name="loggerHandle"><see cref="LoggerHandle"/> of the logger</param>
        /// <returns>Created <see cref="LogControllerScopedLock"/> with <see cref="OwnsLock"/> == false</returns>
        public static LogControllerScopedLock CreateAlreadyUnderLock(LoggerHandle loggerHandle)
        {
            return new LogControllerScopedLock(loggerHandle, ownsLock: 0);
        }

        private LogControllerScopedLock(LoggerHandle handle, byte ownsLock = 1)
        {
            m_OwnsLock = ownsLock;
            Handle = handle;
            // this is called under lock
            m_Index = LogControllerWrapper.GetLogControllerIndexUnderLock(handle);

            if (m_OwnsLock != 0)
                GetLogController().MemoryManager.LockRead();
        }

        /// <summary>
        /// If owns the lock - unlock
        /// <seealso cref="OwnsLock"/>
        /// </summary>
        public void Dispose()
        {
            if (m_OwnsLock != 0)
            {
                GetLogController().MemoryManager.UnlockRead();

                LogControllerWrapper.UnlockRead();
            }
        }

        /// <summary>
        /// Returns <see cref="LogController"/> that is safe to use
        /// </summary>
        /// <returns>Ref to <see cref="LogController"/> of this lock</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref LogController GetLogController()
        {
            LogControllerWrapper.MustBeReadLocked(Handle);
            return ref LogControllerWrapper.GetLogControllerByIndexUnderLock(m_Index);
        }

        /// <summary>
        /// Throws if this lock is not valid.
        /// <seealso cref="IsValid"/>
        /// </summary>
        /// <exception cref="Exception">throws if this lock is not valid</exception>
        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        public void MustBeValid()
        {
            if (Handle.IsValid == false)
                throw new Exception($"LoggerHandle is not valid in LogControllerScopedLock");
        }
    }

    internal static class ThreadGuard
    {
#if !UNITY_DOTSRUNTIME
    #if !UNITY_EDITOR
        #if USE_BASELIB
                private static IntPtr s_MainThreadId;
        #else
                private static Thread s_MainThread;
        #endif
                private static bool s_Initialized = false;

                [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
                static void InitRun()
                {
                    if (s_Initialized) return;
        #if USE_BASELIB
                    s_MainThreadId = Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId();
        #else
                    s_MainThread = Thread.CurrentThread;
        #endif
                    s_Initialized = true;
                }
    #endif
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsMainThread()
        {
#if UNITY_DOTSRUNTIME
            return true; // not supported
#elif UNITY_EDITOR
            return UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread();
#else
            if (s_Initialized == false)
                throw new Exception("ThreadGuard was not initialized, cannot check what thread is this");
    #if USE_BASELIB
                var current = Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId();
                return s_MainThreadId == current;
    #else
                var current = Thread.CurrentThread;
                return s_MainThread == current;
    #endif
#endif
        }

        /// <summary>
        /// Throws if current thread is not the main one
        /// </summary>
        /// <exception cref="Exception">Throws if current thread is not the main one</exception>
        public static void EnsureRunningOnMainThread()
        {
            if (IsMainThread() == false)
                throw new Exception("You cannot call this from not-main thread");
        }

        /// <summary>
        /// Debug call. Throws if current thread is not the main one.
        /// </summary>
        /// <exception cref="Exception">Throws if current thread is not the main one</exception>
        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        public static void AssertRunningOnMainThread()
        {
            EnsureRunningOnMainThread();
        }
    }

    static class LogControllerWrapper
    {
        private struct SharedLogControllersReaders {}
        private static readonly SharedStatic<long> s_LogControllersReaders = SharedStatic<long>.GetOrCreate<long, SharedLogControllersReaders>(16);

        private struct SharedLogControllers {}
        private static readonly SharedStatic<UnsafeList<LogController>> s_LogControllers = SharedStatic<UnsafeList<LogController>>.GetOrCreate<UnsafeList<LogController>, SharedLogControllers>(16);

        private struct SharedLogControllersUniqIdCounter {}
        private static readonly SharedStatic<long> s_LogControllersUniqIdCounter = SharedStatic<long>.GetOrCreate<long, SharedLogControllersUniqIdCounter>(16);

        private struct SharedLogControllersLock {}
        private static readonly SharedStatic<long> s_LogControllersLock = SharedStatic<long>.GetOrCreate<long, SharedLogControllersLock>(16);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Lock()
        {
            BurstSpinLockReadWriteFunctions.EnterExclusive(ref s_LogControllersLock.Data, ref s_LogControllersReaders.Data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Unlock()
        {
            BurstSpinLockReadWriteFunctions.ExitExclusive(ref s_LogControllersLock.Data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LockRead()
        {
            BurstSpinLockReadWriteFunctions.EnterRead(ref s_LogControllersLock.Data, ref s_LogControllersReaders.Data);
#if DEBUG_DEADLOCKS
            UnityEngine.Debug.Log(string.Format("after Lock read {0}", Interlocked.Read(ref s_LogControllersReaders.Data)));
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UnlockRead()
        {
            BurstSpinLockReadWriteFunctions.ExitRead(ref s_LogControllersReaders.Data);
#if DEBUG_DEADLOCKS
            UnityEngine.Debug.Log(string.Format("after Un Lock read {0}", Interlocked.Read(ref s_LogControllersReaders.Data)));
#endif
        }

        internal static void AssertNoLocks()
        {
            var exclusive = Interlocked.Read(ref s_LogControllersLock.Data);
            var readers = Interlocked.Read(ref s_LogControllersReaders.Data);
            Assert.AreEqual(0, exclusive, "Exclusive lock is taken");
            Assert.AreEqual(0, readers, "Read lock is taken");
        }

        internal static void ShutdownAll()
        {
            ThreadGuard.EnsureRunningOnMainThread();

            Lock();

            try
            {
                if (s_LogControllers.Data.IsCreated && s_LogControllers.Data.IsEmpty == false)
                {
                    var n = s_LogControllers.Data.Length;

                    for (var i = 0; i < n; i++)
                    {
                        if (s_LogControllers.Data[i].IsCreated)
                        {
                            s_LogControllers.Data[i].Shutdown();
                        }
                    }

                    s_LogControllers.Data.Clear();
                }
            }
            finally
            {
                Unlock();
            }
        }

        public static void Remove(in LoggerHandle loggerHandle)
        {
            ThreadGuard.EnsureRunningOnMainThread();

            CheckLogControllersCreated();
            CheckLoggerHandleIsValid(loggerHandle);

            Lock();

            try
            {
                var n = s_LogControllers.Data.Length;
                for (var i = 0; i < n; i++)
                {
                    ref var lc = ref s_LogControllers.Data.ElementAt(i);
                    if (lc.Handle.Value == loggerHandle.Value)
                    {
                        lc.Handle = default;
                        lc.Shutdown();
                        break;
                    }
                }
            }
            finally
            {
                Unlock();
            }
        }

        public static LoggerHandle Create(ref LogMemoryManagerParameters memoryManagerParameters)
        {
            ThreadGuard.EnsureRunningOnMainThread();

            Lock();

            try
            {
                if (s_LogControllers.Data.IsCreated == false)
                {
                    s_LogControllers.Data = new UnsafeList<LogController>(64, Allocator.Persistent);
                }

                var uniqId = Interlocked.Increment(ref s_LogControllersUniqIdCounter.Data);

                var handle = new LoggerHandle(uniqId);
                s_LogControllers.Data.Add(new LogController(handle, memoryManagerParameters));

                return handle;
            }
            finally
            {
                Unlock();
            }
        }

        public static int GetTotalDispatchedMessages()
        {
            CheckLogControllersCreated();

            LockRead();

            try
            {
                var res = 0;
                var n = s_LogControllers.Data.Length;
                for (var i = 0; i < n; i++)
                {
                    res += s_LogControllers.Data[i].LogDispatched();
                }
                return res;
            }
            finally
            {
                UnlockRead();
            }
        }

        public static int GetLogControllerIndexUnderLock(LoggerHandle loggerHandle)
        {
            CheckLoggerHandleIsValid(loggerHandle);
            MustBeReadLocked(loggerHandle);
            CheckLogControllersCreated();

            var n = s_LogControllers.Data.Length;
            for (var i = 0; i < n; i++)
            {
                if (s_LogControllers.Data[i].Handle.Value == loggerHandle.Value)
                {
                    return i;
                }
            }

            UnityEngine.Debug.LogError(string.Format("Cannot find logger by handle {0} in {1} logControllers:", loggerHandle.Value, n));
            for (var i = 0; i < n; i++)
            {
                ref var lc = ref s_LogControllers.Data.ElementAt(i);
                UnityEngine.Debug.LogError(string.Format("- {0}", lc.Handle.Value));
            }

            throw new Exception("Cannot find logger by handle");
        }

        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        private static void CheckLoggerHandleIsValid(in LoggerHandle loggerHandle)
        {
            if (loggerHandle.IsValid == false)
                throw new Exception($"LoggerHandle is not valid");
        }

        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        private static void CheckLogControllersCreated()
        {
            if (s_LogControllers.Data.IsCreated == false)
                throw new Exception($"LogControllers.Data is not created");
        }

        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        public static void DebugPrintQueueInfos(LoggerHandle loggerHandle)
        {
            if (s_LogControllers.Data.IsCreated == false)
            {
                UnityEngine.Debug.Log("[DebugPrintQueueInfos] LogControllers are not created!");
            }
            else
            {
                LockRead();

                try
                {
                    var foundLoggerToHighlight = false;
                    var res = 0;
                    var n = s_LogControllers.Data.Length;
                    for (var i = 0; i < n; i++)
                    {
                        var thisRes = s_LogControllers.Data[i].LogDispatched();
                        var handle = s_LogControllers.Data[i].Handle;

                        var highlightedLogger = handle.Value == loggerHandle.Value;
                        if (highlightedLogger)
                            foundLoggerToHighlight = true;

                        res += thisRes;
                        UnityEngine.Debug.Log($"[DebugPrintQueueInfos] #{i} id={handle.Value} dispatched = {thisRes} {(highlightedLogger ? "<<<<<<" : "")}");
                    }
                    if (foundLoggerToHighlight == false)
                        UnityEngine.Debug.LogWarning($"<b>[DebugPrintQueueInfos] id={loggerHandle.Value} was not found! Is it an error?</b>");
                    UnityEngine.Debug.Log($"[DebugPrintQueueInfos] {n} controllers. Total dispatched = {res}\n");
                }
                finally
                {
                    UnlockRead();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref LogController GetLogControllerByIndexUnderLock(int index)
        {
            return ref s_LogControllers.Data.ElementAt(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        public static void MustBeReadLocked(LoggerHandle handle)
        {
            if (BurstSpinLockReadWriteFunctions.HasReadLock(ref s_LogControllersReaders.Data) == false)
            {
                // burst cannot string.Format in exceptions
                UnityEngine.Debug.LogError(string.Format("No read lock for {0}", handle.Value));

                throw new Exception("No read lock!");
            }
        }
    }
}
