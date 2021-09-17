using System;
using Unity.Burst;
using Unity.Collections;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// FixedList4096Bytes{T} wrapped with read write spin lock
    /// </summary>
    /// <typeparam name="T">unmanaged, IEquatable{T}</typeparam>
    [BurstCompile]
    internal struct ThreadSafeList4096<T> where T : unmanaged, IEquatable<T>
    {
        private long m_Spinlock;
        private FixedList4096Bytes<T> m_Data;
        private long m_Readers;

        private void Lock()
        {
            BurstSpinLockReadWriteFunctions.EnterExclusive(ref m_Spinlock, ref m_Readers);
        }

        private void Unlock()
        {
            BurstSpinLockReadWriteFunctions.ExitExclusive(ref m_Spinlock);
        }

        private void LockRead()
        {
            BurstSpinLockReadWriteFunctions.EnterRead(ref m_Spinlock, ref m_Readers);
        }

        private void UnlockRead()
        {
            BurstSpinLockReadWriteFunctions.ExitRead(ref m_Readers);
        }

        public void Add(T obj)
        {
            Lock();
            try
            {
                m_Data.Add(obj);
            }
            finally
            {
                Unlock();
            }
        }

        public void Remove(T obj)
        {
            Lock();
            try
            {
                m_Data.RemoveSwapBack(obj);
            }
            finally
            {
                Unlock();
            }
        }

        public void Remove<U>(U list) where U : struct, INativeList<T>
        {
            Lock();
            try
            {
                var n = list.Length;
                for (var i = 0; i < n; i++)
                    m_Data.RemoveSwapBack(list.ElementAt(i));
            }
            finally
            {
                Unlock();
            }
        }

        /// <summary>
        /// This call is safe only if called in between BeginRead-EndRead or BeginWrite-EndWrite
        /// </summary>
        public int Length
        {
            get
            {
                var result = m_Data.Length;
                return result;
            }
        }

        public int BeginWrite()
        {
            Lock();

            return m_Data.Length;
        }

        public static ref FixedList4096Bytes<T> GetReferenceNotThreadSafe(ref ThreadSafeList4096<T> list)
        {
            return ref list.m_Data;
        }

        public void EndWrite()
        {
            Unlock();
        }

        public int BeginRead()
        {
            LockRead();

            return m_Data.Length;
        }

        public void EndRead()
        {
            UnlockRead();
        }

        /// <summary>
        /// This call is safe only if called in between BeginRead-EndRead or BeginWrite-EndWrite
        /// </summary>
        /// <param name="i">index</param>
        /// <returns>ref element at index i</returns>
        public ref T ElementAt(int i)
        {
            return ref m_Data.ElementAt(i);
        }
    }

    /// <summary>
    /// FixedList4096Bytes{FunctionPointer{T}} wrapped with read write spin lock
    /// </summary>
    /// <typeparam name="T">Any type that can be used in FunctionPointer</typeparam>
    [BurstCompile]
    internal struct ThreadSafeFuncList<T>
    {
        private long m_Spinlock;
        private FixedList4096Bytes<FunctionPointer<T>> m_Data;
        private long m_Readers;

        private void Lock()
        {
            BurstSpinLockReadWriteFunctions.EnterExclusive(ref m_Spinlock, ref m_Readers);
        }

        private void Unlock()
        {
            BurstSpinLockReadWriteFunctions.ExitExclusive(ref m_Spinlock);
        }

        private void LockRead()
        {
            BurstSpinLockReadWriteFunctions.EnterRead(ref m_Spinlock, ref m_Readers);
        }

        private void UnlockRead()
        {
            BurstSpinLockReadWriteFunctions.ExitRead(ref m_Readers);
        }

        public void Add(FunctionPointer<T> func)
        {
            Lock();
            try
            {
                foreach (var item in m_Data)
                {
                    if (item.Value == func.Value)
                        return;
                }
                m_Data.Add(func);
            }
            finally
            {
                Unlock();
            }
        }

        public void Remove(IntPtr token)
        {
            Lock();
            try
            {
                for (var i = 0; i < m_Data.Length; i++)
                {
                    if (m_Data[i].Value == token)
                    {
                        m_Data.RemoveAtSwapBack(i);
                        break;
                    }
                }
            }
            finally
            {
                Unlock();
            }
        }

        public int Length
        {
            get
            {
                // LockRead();
                var result = m_Data.Length;
                // UnlockRead();
                return result;
            }
        }

        public int BeginRead()
        {
            LockRead();

            return m_Data.Length;
        }

        public void EndRead()
        {
            UnlockRead();
        }

        public ref FunctionPointer<T> ElementAt(int i)
        {
            return ref m_Data.ElementAt(i);
        }
    }
}
