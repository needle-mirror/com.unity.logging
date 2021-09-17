using System;
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

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.DivideByZeroChecks, false)]
        public struct ScopedExclusiveLock : IDisposable
        {
            private SpinLockReadWrite m_lock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ScopedExclusiveLock(SpinLockReadWrite sl)
            {
                m_lock = sl;
                m_lock.Lock();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                m_lock.Unlock();
            }
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.DivideByZeroChecks, false)]
        public struct ScopedReadLock : IDisposable
        {
            private SpinLockReadWrite m_lock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ScopedReadLock(SpinLockReadWrite sl)
            {
                m_lock = sl;
                m_lock.LockRead();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                m_lock.UnlockRead();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpinLockReadWrite(Allocator allocator)
        {
            m_lock = new BurstSpinLockReadWrite(allocator);
        }

        public bool IsCreated => m_lock.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            m_lock.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            m_lock.EnterExclusive();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unlock()
        {
            m_lock.ExitExclusive();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockRead()
        {
            m_lock.EnterRead();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnlockRead()
        {
            m_lock.ExitRead();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpgradeReadToWriteLock()
        {
            m_lock.UpgradeReadToWriteLock();
        }
    }
}
