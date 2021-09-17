//#define USE_BASELIB

#if ENABLE_UNITY_COLLECTIONS_CHECKS
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

namespace Unity.Logging
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    internal static class BurstSpinLockCheckFunctions
    {
        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        public static void CheckForRecursiveLock(in long threadId, ref long lockVar)
        {
#if USE_BASELIB
            var currentOwnerThreadId = Interlocked.Read(ref lockVar);

            if (threadId == currentOwnerThreadId)
                throw new Exception("Recursive lock!");
#endif
        }

        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        public static void CheckForRecursiveLock(ref long lockVar)
        {
#if USE_BASELIB
            var threadId = Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId().ToInt64();
            CheckForRecursiveLock(threadId, ref lockVar);
#endif
        }

        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        public static void CheckWeCanExit(ref long lockVar)
        {
#if USE_BASELIB
            var currentOwnerThreadId = Interlocked.Read(ref lockVar);
            var threadId = Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId().ToInt64();

            if (threadId != currentOwnerThreadId)
                throw new Exception("Exit is called from the other thread!");
#endif
        }
    }

    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    internal static class BurstSpinLockExclusiveFunctions
    {
        /// <summary>
        /// Lock Exclusive. Will block if cannot lock immediately
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnterExclusive(ref long lockVar)
        {
#if USE_BASELIB
            var threadId = Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId().ToInt64();
            BurstSpinLockCheckFunctions.CheckForRecursiveLock(threadId, ref lockVar);
#else
            var threadId = 1;
#endif

            while (System.Threading.Interlocked.CompareExchange(ref lockVar, threadId, 0) != 0)
            {
#if USE_BASELIB
                Baselib.LowLevel.Binding.Baselib_Thread_YieldExecution();
#endif
            }
        }

        /// <summary>
        /// Try to lock Exclusive. Won't block
        /// </summary>
        /// <returns>True if locked</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryEnterExclusive(ref long lockVar)
        {
#if USE_BASELIB
            var threadId = Baselib.LowLevel.Binding.Baselib_Thread_GetCurrentThreadId().ToInt64();
            BurstSpinLockCheckFunctions.CheckForRecursiveLock(threadId, ref lockVar);
#else
            var threadId = 1;
#endif

            return System.Threading.Interlocked.CompareExchange(ref lockVar, threadId, 0) == 0;
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
    }

    /// <summary>
    /// Implement a very basic, Burst-compatible SpinLock that mirrors the basic .NET SpinLock API.
    /// </summary>
    [BurstCompile]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    internal struct BurstSpinLock : IDisposable
    {
        [Conditional("DEBUG_ADDITIONAL_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckIfLockCreated()
        {
            if (m_Locked.IsCreated == false)
                throw new Exception("Lock wasn't created, but you're accessing it");
        }

        /// <summary>
        /// Lock. Will block if cannot lock immediately
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enter()
        {
            CheckIfLockCreated();

            BurstSpinLockExclusiveFunctions.EnterExclusive(ref m_Locked.ElementAt(0));
        }

        /// <summary>
        /// Try to lock. Won't block
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnter()
        {
            CheckIfLockCreated();

            return BurstSpinLockExclusiveFunctions.TryEnterExclusive(ref m_Locked.ElementAt(0));
        }

        /// <summary>
        /// Unlock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exit()
        {
            CheckIfLockCreated();

            BurstSpinLockExclusiveFunctions.ExitExclusive(ref m_Locked.ElementAt(0));
        }

        private UnsafeList<long> m_Locked;

        /// <summary>
        /// Constructor for the spin lock
        /// </summary>
        /// <param name="allocator">allocator to use for internal memory allocation. Usually should be Allocator.Persistent</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BurstSpinLock(Allocator allocator)
        {
            m_Locked = new UnsafeList<long>(1, allocator);
            m_Locked.AddNoResize(0);
        }

        /// <summary>
        /// Dispose this spin lock. <see cref="IDisposable"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (m_Locked.IsCreated)
                m_Locked.Dispose();
        }

        public bool Locked => Interlocked.Read(ref m_Locked.ElementAt(0)) != 0;
    }
}
