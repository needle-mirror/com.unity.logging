//#define DEBUG_DEADLOCKS

//#define MARK_THREAD_OWNERS

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
#define DEBUG_ADDITIONAL_CHECKS
#endif

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IL2CPP.CompilerServices;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Logging
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    internal static class BurstSpinLockReadWriteFunctions
    {
        /// <summary>
        /// Lock Exclusive. Will block if cannot lock immediately
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnterExclusive(ref long lockVar, ref long readersVar)
        {
#if MARK_THREAD_OWNERS
            var threadId = Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId().ToInt64();
            BurstSpinLockCheckFunctions.CheckForRecursiveLock(threadId, ref lockVar);
#else
            var threadId = 1;
#endif

            while (System.Threading.Interlocked.CompareExchange(ref lockVar, threadId, 0) != 0)
            {
                Baselib.LowLevel.Binding.Baselib_Thread_YieldExecution();
            }

#if DEBUG_DEADLOCKS
            var deadlockGuard = 0;
#endif

            // while we have readers
            while (Interlocked.Read(ref readersVar) != 0)
            {
                Baselib.LowLevel.Binding.Baselib_Thread_YieldExecution();

#if DEBUG_DEADLOCKS
                if (++deadlockGuard == 512)
                {
                    UnityEngine.Debug.LogError("Cannot get spin lock, because of readers");
                    Interlocked.Exchange(ref lockVar, 0);
                    throw new Exception();
                }
#endif
            }
        }

        /// <summary>
        /// Try to lock Exclusive. Won't block
        /// </summary>
        /// <returns>True if locked</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryEnterExclusive(ref long lockVar, ref long readersVar)
        {
            if (Interlocked.Read(ref readersVar) != 0)
                return false;

#if MARK_THREAD_OWNERS
            var threadId = Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId().ToInt64();
            BurstSpinLockCheckFunctions.CheckForRecursiveLock(threadId, ref lockVar);
#else
            var threadId = 1;
#endif

            if (System.Threading.Interlocked.CompareExchange(ref lockVar, threadId, 0) != 0)
                return false;

            if (Interlocked.Read(ref readersVar) != 0)
            {
                Interlocked.Exchange(ref lockVar, 0);
            }

            return true;
        }

        /// <summary>
        /// Unlock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExitExclusive(ref long lockVar)
        {
            BurstSpinLockCheckFunctions.CheckWeCanExit(ref lockVar);
            Interlocked.Exchange(ref lockVar, 0);
        }

        /// <summary>
        /// Lock for Read. Will block if exclusive is locked
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnterRead(ref long lockVar, ref long readersVar)
        {
            BurstSpinLockCheckFunctions.CheckForRecursiveLock(ref lockVar);

            for (;;)
            {
                Interlocked.Increment(ref readersVar);

                // if not locked
                if (Interlocked.Read(ref lockVar) == 0)
                {
                    return;
                }

                // fail, it is locked
                Interlocked.Decrement(ref readersVar);

                // while it is locked - spin
                while (Interlocked.Read(ref lockVar) != 0)
                {
                    Baselib.LowLevel.Binding.Baselib_Thread_YieldExecution();
                }
            }
        }

        /// <summary>
        /// Exit read lock. EnterRead must be called before this call by the same thread
        /// </summary>
        /// <param name="readersVar"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExitRead(ref long readersVar)
        {
            Interlocked.Decrement(ref readersVar);
            CheckForNegativeReaders(ref readersVar);
        }

        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CheckForNegativeReaders(ref long readers)
        {
            if (Interlocked.Read(ref readers) < 0)
                throw new Exception("Reader count cannot be negative!");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasReadLock(ref long readLock)
        {
            return Interlocked.Read(ref readLock) > 0;
        }
    }

    /// <summary>
    /// Implement a very basic, Burst-compatible read-write SpinLock
    /// </summary>
    [BurstCompile]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    internal struct BurstSpinLockReadWrite : IDisposable
    {
        private const int MemorySize = 16; // * sizeof(long) == 128 byte
        private const int LockLocation = 0;
        private const int ReadersLocation = 8; // * sizeof(long) == 64 byte offset (cache line)

        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckIfLockCreated()
        {
            if (m_Locked.IsCreated == false)
                throw new Exception("RWLock wasn't created, but you're accessing it");
        }

        /// <summary>
        /// Lock Exclusive. Will block if cannot lock immediately
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterExclusive()
        {
            CheckIfLockCreated();

            BurstSpinLockReadWriteFunctions.EnterExclusive(ref m_Locked.ElementAt(LockLocation), ref m_Locked.ElementAt(ReadersLocation));
        }

        /// <summary>
        /// Try to lock Exclusive. Won't block
        /// </summary>
        /// <returns>True if locked</returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnterExclusive()
        {
            CheckIfLockCreated();

            return BurstSpinLockReadWriteFunctions.TryEnterExclusive(ref m_Locked.ElementAt(LockLocation), ref m_Locked.ElementAt(ReadersLocation));
        }

        /// <summary>
        /// Unlock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitExclusive()
        {
            CheckIfLockCreated();

            BurstSpinLockReadWriteFunctions.ExitExclusive(ref m_Locked.ElementAt(LockLocation));
        }

        /// <summary>
        /// Lock for Read. Will block if exclusive is locked
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterRead()
        {
            CheckIfLockCreated();

            BurstSpinLockReadWriteFunctions.EnterRead(ref m_Locked.ElementAt(LockLocation), ref m_Locked.ElementAt(ReadersLocation));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitRead()
        {
            CheckIfLockCreated();

            BurstSpinLockReadWriteFunctions.ExitRead(ref m_Locked.ElementAt(ReadersLocation));
        }

        private UnsafeList<long> m_Locked;

        /// <summary>
        /// Constructor for the spin lock
        /// </summary>
        /// <param name="allocator">allocator to use for internal memory allocation. Usually should be Allocator.Persistent</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BurstSpinLockReadWrite(Allocator allocator)
        {
            m_Locked = new UnsafeList<long>(MemorySize, allocator);
            for (var i = 0; i < MemorySize; i++)
            {
                m_Locked.AddNoResize(0);
            }
        }

        /// <summary>
        /// Dispose this spin lock. <see cref="IDisposable"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (m_Locked.IsCreated)
            {
                EnterExclusive();
                m_Locked.Dispose();
            }
        }

        public bool Locked => Interlocked.Read(ref m_Locked.ElementAt(LockLocation)) != 0;
        public bool LockedForRead => Interlocked.Read(ref m_Locked.ElementAt(ReadersLocation)) != 0;

        public bool IsCreated => m_Locked.IsCreated;
        public long Id
        {
            get { unsafe { return (long)m_Locked.Ptr; } }
        }
    }
}
