using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Logging.Internal;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Logging
{
    /// <summary>
    /// Double buffered thread-safe queue<br/>
    /// Limited by a maximum size that is passed in the constructor. This is an important for performance, to avoid memory reallocation during the work. <para/>
    /// In the usual case the queue works in the <b>asynchronous</b> mode:<br/>
    /// - One buffer is used for write: several threads can add new <see cref="LogMessage"/>'s to it, no one can read from that buffer. That is done via <see cref="Enqueue"/> method that
    /// does that under the <b>read lock</b>[1].<br/>
    /// - Second buffer is used as a read-only buffer: several sinks can read from it, without modifying anything. This is done via <see cref="BeginRead"/> and <see cref="EndRead"/> that
    /// enters/exits the <b>read lock</b>[1].<br/>
    /// - Second buffer also can be in an exclusive mode: Cleanup system calls <see cref="BeginReadExclusive"/> to enter the <b>exclusive lock</b>[2] to make sure only it can access the read buffer
    /// at the time to release the memory used by the messages. <see cref="EndReadExclusiveClearAndFlip"/> at the end of its work, cleaning the read buffer and flipping write/read ones, so
    /// now the write buffer is empty. Exclusive lock is unlocked after this.<br/>
    /// - <see cref="Sort"/> of the second (read) buffer is done under an <b>exclusive lock</b>[2], since it modifies the list, so it is not safe to read it.
    /// <para/>
    /// Another use case that is much slower - <b>synchronous</b> access. <see cref="LockAndSortForSyncAccess"/> will put the queue under the <b>exclusive lock</b>[2] and return both buffers for read-write access.<br/>
    /// When the access is finished <see cref="EndLockAfterSyncAccess"/> call will clear both buffers and unlock the data structure.<br/>
    /// This is used in the case when full synchronous flush is needed.
    /// <para/>
    /// Lock is used to control the thread-safe access for this data structure:<br/>
    /// [1] Read-lock allows several threads to take it, but only if no exclusive lock is taken. Guarantees that no exclusive lock [2] will be taken during it. Read-lock is used in situations when
    /// it is ok for multiple threads to do some kind of access, like: add new elements (see <see cref="Enqueue"/>), read simultaneously without modifications (see <see cref="BeginRead"/>, <see cref="EndRead"/>).<br/>
    /// [2] Exclusive lock is used when only one thread can enter and no read-locks are taken. This lock is used to cleanup the memory of the messages (see <see cref="BeginReadExclusive"/>) and then
    /// clear the read and swap the read and write buffers (see <see cref="EndReadExclusiveClearAndFlip"/>).
    /// <para/>
    /// Note: While read buffer is in read-only <see cref="BeginRead"/> -- <see cref="EndRead"/> mode the write buffer still can be used to <see cref="Enqueue"/> from multiple threads
    /// </summary>
    [BurstCompile]
    public struct DispatchQueue : IDisposable
    {
        private UnsafeList<LogMessage> m_ListA;
        private UnsafeList<LogMessage> m_ListB;

        private byte m_UseABlittable;
        private bool UseAforRead => m_UseABlittable != 0;

        // This lock makes sure that double buffer swap is protected.
        private SpinLockReadWrite m_SwapDoubleBufferLock;


        /// <summary>
        /// Constructor for <see cref="DispatchQueue"/>. 
        /// </summary>
        /// <remarks>
        /// Creates two lists with the maximum amount of <see cref="LogMessage"/>'s that it can process before it should be flipped.
        /// </remarks>
        /// <param name="size">Maximum length of the queue</param>
        public DispatchQueue(int size)
        {
            m_SwapDoubleBufferLock = new SpinLockReadWrite(Allocator.Persistent);

            m_ListA = new UnsafeList<LogMessage>(size, Allocator.Persistent);
            m_ListB = new UnsafeList<LogMessage>(size, Allocator.Persistent);
            m_UseABlittable = 0;
        }

        /// <summary>
        /// Dispose call that will call Dispose for the lists (under lock) if IsCreated is true. <see cref="IDisposable"/>
        /// </summary>
        public void Dispose()
        {
            MustBeCreated();

            Assert.AreEqual(0, TotalLength);
            using (var exclusiveLock = new SpinLockReadWrite.ScopedExclusiveLock(m_SwapDoubleBufferLock))
            {
                m_ListA.Dispose();
                m_ListB.Dispose();
            }

            m_SwapDoubleBufferLock.Dispose();
        }

        /// <summary>
        /// Puts <see cref="DispatchQueue"/> into reading lock mode till <see cref="EndRead"/> is called to prevent buffer flipping. Several threads can use this at the same time for reading only.
        /// </summary>
        /// <returns>ParallelReader is returned that can be used to get all the LogMessage's from the read buffer</returns>
        public UnsafeList<LogMessage>.ParallelReader BeginRead()
        {
            m_SwapDoubleBufferLock.LockRead();

            var res = UseAforRead ? m_ListA.AsParallelReader() : m_ListB.AsParallelReader();

            return res;
        }

        /// <summary>
        /// Unlocks <see cref="DispatchQueue"/> reading lock that was initiated by <see cref="BeginRead"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndRead()
        {
            m_SwapDoubleBufferLock.UnlockRead();
        }

        /// <summary>
        /// Puts <see cref="DispatchQueue"/> into exclusive lock mode till <see cref="EndReadExclusiveClearAndFlip"/> is called to prevent buffer flipping.
        /// Only one thread can access the read buffer, usually to modify it (cleanup system can free the memory that is used by messages).
        /// </summary>
        /// <returns>ParallelReader is returned that can be used to get all the LogMessage's from read buffer, usually to dispose them</returns>
        public UnsafeList<LogMessage>.ParallelReader BeginReadExclusive()
        {
            m_SwapDoubleBufferLock.Lock();

            var res = UseAforRead ? m_ListA.AsParallelReader() : m_ListB.AsParallelReader();

            return res;
        }

        /// <summary>
        /// Unlocks <see cref="DispatchQueue"/> exclusive lock that was initiated by <see cref="BeginReadExclusive"/>, clears the read buffer and swaps the buffers, so now the write buffer is empty.
        /// </summary>
        public void EndReadExclusiveClearAndFlip()
        {
            try
            {
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
            }
            finally
            {
                m_SwapDoubleBufferLock.Unlock();
            }
        }

        /// <summary>
        /// Sorts the read buffer's <see cref="LogMessage"/>s by timestamp
        /// </summary>
        internal void Sort()
        {
            MustBeCreated();

            using (var exclusiveLock = new SpinLockReadWrite.ScopedExclusiveLock(m_SwapDoubleBufferLock))
            {
                if (UseAforRead)
                    m_ListA.Sort();
                else
                    m_ListB.Sort();
            }
        }

        /// <summary>
        /// Is true if this struct was initialized.
        /// </summary>
        public bool IsCreated => m_ListA.IsCreated;

        /// <summary>
        /// Total length of read-only and write buffers. Usually used for testing of internal state of the queue.
        /// </summary>
        public int TotalLength => m_ListA.Length + m_ListB.Length;

        /// <summary>
        /// Adds new message to the write buffer under read lock. Gets the timestamp just before adding the message to the queue.
        /// </summary>
        /// <param name="payload">PayloadHandle of the log message</param>
        /// <param name="stacktraceId">Stacktrace id of the log message or 0 if none</param>
        /// <param name="logLevel">LogLevel of the log message</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(PayloadHandle payload, long stacktraceId, LogLevel logLevel)
        {
            using (var readLock = new SpinLockReadWrite.ScopedReadLock(m_SwapDoubleBufferLock))
            {
                var listToWrite = UseAforRead ? m_ListB.AsParallelWriter() : m_ListA.AsParallelWriter();
                var timestamp = TimeStampWrapper.GetTimeStamp();
                listToWrite.AddNoResize(new LogMessage(payload, timestamp, stacktraceId, logLevel));
            }
        }

        /// <summary>
        /// Adds new message to the write buffer under read lock. Timestamp is supplied.
        /// Note that if timestamp is less than last timestamp logged, it will be ignored.
        /// This method is intended for logging messages injected at startup.
        /// </summary>
        /// <param name="payload">PayloadHandle of the log message</param>
        /// <param name="timestamp">Timestamp of log in nanoseconds</param>
        /// <param name="stacktraceId">Stacktrace id of the log message or 0 if none</param>
        /// <param name="logLevel">LogLevel of the log message</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Enqueue(PayloadHandle payload, long timestamp, long stacktraceId, LogLevel logLevel)
        {
            using (var readLock = new SpinLockReadWrite.ScopedReadLock(m_SwapDoubleBufferLock))
            {
                var listToWrite = UseAforRead ? m_ListB.AsParallelWriter() : m_ListA.AsParallelWriter();
                listToWrite.AddNoResize(new LogMessage(payload, timestamp, stacktraceId, logLevel));
            }
        }

        /// <summary>
        /// Enters the exclusive lock for <see cref="DispatchQueue"/> to get the access for both buffers till <see cref="EndLockAfterSyncAccess"/> is called.
        /// </summary>
        /// <param name="olderMessages">Returns the buffer with older messages (read buffer)</param>
        /// <param name="newerMessages">Returns the buffer with newer messages (write buffer)</param>
        public void LockAndSortForSyncAccess(out UnsafeList<LogMessage> olderMessages, out UnsafeList<LogMessage> newerMessages)
        {
            MustBeCreated();

            m_SwapDoubleBufferLock.Lock();

            m_ListA.Sort();
            m_ListB.Sort();

            if (UseAforRead)
            {
                olderMessages = m_ListA;
                newerMessages = m_ListB;
            }
            else
            {
                olderMessages = m_ListB;
                newerMessages = m_ListA;
            }
        }

        /// <summary>
        /// Unlocks <see cref="DispatchQueue"/> exclusive lock that was initiated by <see cref="LockAndSortForSyncAccess"/> and clears all the buffers.
        /// </summary>
        public void EndLockAfterSyncAccess()
        {
            try
            {
                m_ListA.Clear();
                m_ListB.Clear();
            }
            finally
            {
                m_SwapDoubleBufferLock.Unlock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private void MustBeCreated()
        {
            if (IsCreated == false)
            {
                throw new Exception("DispatchQueue is not created!");
            }
        }
    }
}
