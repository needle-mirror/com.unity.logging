using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.IL2CPP.CompilerServices;
#if NET_DOTS
using System.Runtime.InteropServices;
#endif

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
#if !NET_DOTS
        private BurstSpinLock m_SpinLock;
#else
        private Int64 m_Handle;
#endif

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
#if !NET_DOTS
            m_SpinLock = new BurstSpinLock(allocator);
#else
            m_Handle = CreateSpinLockNative();
#endif
        }

        /// <summary>
        /// Dispose this spin lock. <see cref="IDisposable"/>
        /// </summary>
        public void Dispose()
        {
#if !NET_DOTS
            m_SpinLock.Dispose();
#else
            DestroySpinLockNative(m_Handle);
            m_Handle = 0;
#endif
        }

        /// <summary>
        /// True if locked
        /// </summary>
#if !NET_DOTS
        public bool Locked => m_SpinLock.Locked;
#else
        public bool Locked => IsLockedNative(m_Handle);
#endif

        /// <summary>
        /// Lock. Will block if cannot lock immediately
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
#if !NET_DOTS
            m_SpinLock.Enter();
#else
            LockNative(m_Handle);
#endif
        }

        /// <summary>
        /// Try to lock. Won't block
        /// </summary>
        /// <param name="lockTaken">Will be true if lock was taken</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock(ref bool lockTaken)
        {
#if !NET_DOTS
            lockTaken = m_SpinLock.TryEnter();
#else
            lockTaken = TryLockNative(m_Handle);
#endif
        }

        /// <summary>
        /// Unlock the spin lock
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unlock()
        {
#if !NET_DOTS
            m_SpinLock.Exit();
#else
            UnlockNative(m_Handle);
#endif
        }

#if NET_DOTS
        [DllImport("lib_unity_logging", EntryPoint = "CreateSpinLock")]
        internal static extern unsafe Int64 CreateSpinLockNative();

        [DllImport("lib_unity_logging", EntryPoint = "Lock")]
        internal static extern unsafe bool LockNative(Int64 handle);

        [DllImport("lib_unity_logging", EntryPoint = "IsLocked")]
        internal static extern unsafe bool IsLockedNative(Int64 handle);

        [DllImport("lib_unity_logging", EntryPoint = "TryLock")]
        internal static extern unsafe bool TryLockNative(Int64 handle);

        [DllImport("lib_unity_logging", EntryPoint = "Unlock")]
        internal static extern unsafe void UnlockNative(Int64 handle);

        [DllImport("lib_unity_logging", EntryPoint = "DestroySpinLock")]
        internal static extern unsafe void DestroySpinLockNative(Int64 handle);
#endif
    }
}
