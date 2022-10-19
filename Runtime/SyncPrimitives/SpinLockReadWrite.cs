using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.IL2CPP.CompilerServices;

namespace Unity.Logging
{
    /// <summary>
    /// Burst-friendly synchronization primitive that supports read lock and exclusive (write) lock
    /// </summary>
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public struct SpinLockReadWrite : IDisposable
    {
        private BurstSpinLockReadWrite m_lock;

        /// <summary>
        /// IDisposable scoped structure that holds <see cref="BurstSpinLockReadWrite"/> in exclusive mode. Should be using with <c>using</c>
        /// </summary>
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.DivideByZeroChecks, false)]
        public struct ScopedExclusiveLock : IDisposable
        {
            private SpinLockReadWrite m_parentLock;

            /// <summary>
            /// Creates ScopedReadLock and locks SpinLockReadWrite in exclusive mode
            /// </summary>
            /// <param name="sl">SpinLock to lock</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ScopedExclusiveLock(SpinLockReadWrite sl)
            {
                m_parentLock = sl;
                m_parentLock.Lock();
            }

            /// <summary>
            /// Unlocks the lock
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                m_parentLock.Unlock();
            }
        }

        /// <summary>
        /// IDisposable scoped structure that holds <see cref="BurstSpinLockReadWrite"/> in read mode. Should be using with <c>using</c>
        /// </summary>
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.DivideByZeroChecks, false)]
        public struct ScopedReadLock : IDisposable
        {
            private SpinLockReadWrite m_parentLock;

            /// <summary>
            /// Creates ScopedReadLock and locks SpinLockReadWrite in read mode
            /// </summary>
            /// <param name="sl">SpinLock to lock</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ScopedReadLock(SpinLockReadWrite sl)
            {
                m_parentLock = sl;
                m_parentLock.LockRead();
            }

            /// <summary>
            /// Unlocks the lock
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                m_parentLock.UnlockRead();
            }
        }

        /// <summary>
        /// Allocates the spinlock
        /// </summary>
        /// <param name="allocator">Allocator to use</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpinLockReadWrite(Allocator allocator)
        {
            m_lock = new BurstSpinLockReadWrite(allocator);
        }

        /// <summary>
        /// True if was created
        /// </summary>
        public bool IsCreated => m_lock.IsCreated;

        /// <summary>
        /// Enters the exclusive mode (so no other is holding the lock) and destroys it - so nobody can enter it
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            m_lock.Dispose();
        }

        /// <summary>
        /// Enters the exclusive lock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            m_lock.EnterExclusive();
        }

        /// <summary>
        /// Exits the exclusive lock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unlock()
        {
            m_lock.ExitExclusive();
        }

        /// <summary>
        /// Enters the read lock (multiple read locks allowed in parallel, but no exclusive)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockRead()
        {
            m_lock.EnterRead();
        }

        /// <summary>
        /// Exits the read lock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnlockRead()
        {
            m_lock.ExitRead();
        }

        /// <summary>
        /// True if exclusively locked
        /// </summary>
        public bool Locked => m_lock.Locked;

        /// <summary>
        /// True if read locked
        /// </summary>
        public bool LockedForRead => m_lock.LockedForRead;

        /// <summary>
        /// Throws if not in the exclusive lock
        /// </summary>
        /// <exception cref="Exception">If SpinLock is not exclusively locked</exception>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        public void MustBeExclusivelyLocked()
        {
            if (m_lock.Locked == false)
                throw new Exception("SpinLock is not exclusively locked!");
        }

        /// <summary>
        /// Throws if not in the read lock
        /// </summary>
        /// <exception cref="Exception">If SpinLock is not read locked</exception>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        public void MustBeReadLocked()
        {
            if (m_lock.LockedForRead == false)
                throw new Exception("SpinLock is not read locked!");
        }
    }
}
