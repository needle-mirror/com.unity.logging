using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

namespace Unity.Logging
{
    /// <summary>
    /// Double buffered thread-safe queue that is used by com.unity.logging.
    /// One buffer is used for write: several threads can add new <see cref="LogMessage"/>'s to it
    /// Second one is used as read-only buffer: sinks and logging system's cleanup system read it.
    /// Cleanup system calls <see cref="EndReadClearAndFlip"/> at the end of its work, cleaning read-only buffer and flipping write/read-only ones.
    /// </summary>
    [BurstCompile]
    public struct DispatchQueue : IDisposable
    {
        private UnsafeList<LogMessage> m_ListA;
        private UnsafeList<LogMessage> m_ListB;

        private int m_UseABlittable;
        private bool UseAforRead => m_UseABlittable != 0;

        private SpinLockReadWrite m_Lock;

        /// <summary>
        /// Puts <see cref="DispatchQueue"/> into reading lock mode till `EndRead` or `EndReadClearAndFlip` is called to prevent buffer flipping.
        /// </summary>
        /// <returns>ParallelReader is returned that can be used to get all the LogMessage's from read-only buffer</returns>
        public UnsafeList<LogMessage>.ParallelReader BeginRead()
        {
            m_Lock.LockRead();

            var res = UseAforRead ? m_ListA.AsParallelReader() : m_ListB.AsParallelReader();

            return res;
        }

        /// <summary>
        /// Unlocks <see cref="DispatchQueue"/> reading lock that was initiated by <see cref="BeginRead"/>
        /// </summary>
        public void EndRead()
        {
            m_Lock.UnlockRead();
        }

        /// <summary>
        /// Clears read-only buffer and swaps it with write buffer.
        /// Then unlocks <see cref="DispatchQueue"/> reading lock that was initiated by <see cref="BeginRead"/>
        /// </summary>
        public void EndReadClearAndFlip()
        {
            m_Lock.UpgradeReadToWriteLock();

            if (m_UseABlittable != 0)
            {
                m_ListA.Clear();
                m_UseABlittable = 0;
            }
            else
            {
                m_ListB.Clear();
                m_UseABlittable = 1;
            }

            m_Lock.Unlock();
        }

        /// <summary>
        /// Constructor for <see cref="DispatchQueue"/>. Will create two lists with size = <see cref="size"/>, max amount of <see cref="LogMessage"/>'s that it can process before it should be flipped
        /// </summary>
        /// <param name="size">Max length of the queue</param>
        public DispatchQueue(int size)
        {
            m_Lock = new SpinLockReadWrite(Allocator.Persistent);
            m_ListA = new UnsafeList<LogMessage>(size, Allocator.Persistent);
            m_ListB = new UnsafeList<LogMessage>(size, Allocator.Persistent);
            m_UseABlittable = 0;
        }

        /// <summary>
        /// Sorts read-only <see cref="LogMessage"/>s by timestamp
        /// </summary>
        [BurstCompile]
        internal void Sort()
        {
            if (IsCreated == false) return;

            using (var exclusiveLock = new SpinLockReadWrite.ScopedExclusiveLock(m_Lock))
            {
                if (UseAforRead)
                    m_ListA.Sort();
                else
                    m_ListB.Sort();
            }
        }

        /// <summary>
        /// Dispose call that will call Dispose for the lists (under lock) if IsCreated is true. <see cref="IDisposable"/>
        /// </summary>
        public void Dispose()
        {
            if (!IsCreated)
                return;

            using (var exclusiveLock = new SpinLockReadWrite.ScopedExclusiveLock(m_Lock))
            {
                m_ListA.Dispose();
                m_ListB.Dispose();
            }

            m_Lock.Dispose();
        }

        /// <summary>
        /// Is true if this struct was initialized.
        /// </summary>
        public bool IsCreated => m_ListA.IsCreated;

        /// <summary>
        /// Total length of read-only and write buffers. Usually used for testing of internal state of the queue.
        /// </summary>
        public int TotalLength => m_ListA.Length + m_ListB.Length;

        public void Enqueue(ref LogMessage message)
        {
            using (var exclusiveLock = new SpinLockReadWrite.ScopedExclusiveLock(m_Lock))
            {
                var listToWrite = UseAforRead ? m_ListB.AsParallelWriter() : m_ListA.AsParallelWriter();
                listToWrite.AddNoResize(message);
            }
        }
    }
}
