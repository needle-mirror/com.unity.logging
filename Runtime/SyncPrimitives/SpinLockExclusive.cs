using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.IL2CPP.CompilerServices;

namespace Unity.Logging
{
    /// <summary>
    /// Burst-friendly synchronization primitive
    /// </summary>
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public struct SpinLockExclusive : IDisposable
    {
        private BurstSpinLock m_SpinLock;

        /// <summary>
        /// IDisposable scoped structure that holds <see cref="SpinLockExclusive"/>. Should be using with <c>using</c>
        /// </summary>
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.DivideByZeroChecks, false)]
        public struct ScopedLock : IDisposable
        {
            private SpinLockExclusive m_lock;

            /// <summary>
            /// Creates ScopedLock and locks SpinLockExclusive
            /// </summary>
            /// <param name="sl">SpinLock to lock</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ScopedLock(SpinLockExclusive sl)
            {
                m_lock = sl;
                m_lock.Lock();
            }

            /// <summary>
            /// Unlocks the lock
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                m_lock.Unlock();
            }
        }

        /// <summary>
        /// Constructor for the spin lock
        /// </summary>
        /// <param name="allocator">allocator to use for internal memory allocation. Usually should be Allocator.Persistent</param>
        public SpinLockExclusive(Allocator allocator)
        {
            m_SpinLock = new BurstSpinLock(allocator);
        }

        /// <summary>
        /// Dispose this spin lock. <see cref="IDisposable"/>
        /// </summary>
        public void Dispose()
        {
            m_SpinLock.Dispose();
        }

        /// <summary>
        /// True if locked
        /// </summary>
        public bool Locked => m_SpinLock.Locked;

        /// <summary>
        /// Lock. Will block if cannot lock immediately
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            m_SpinLock.Enter();
        }

        /// <summary>
        /// Try to lock. Won't block
        /// </summary>
        /// <param name="lockTaken">Will be true if lock was taken</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock(ref bool lockTaken)
        {
            lockTaken = m_SpinLock.TryEnter();
        }

        /// <summary>
        /// Unlock the spin lock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unlock()
        {
            m_SpinLock.Exit();
        }
    }
}
