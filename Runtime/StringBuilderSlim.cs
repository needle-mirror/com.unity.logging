using System;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Logging
{
    internal class StringBuilderSlim : IDisposable
    {
        private unsafe byte* ptr;
        private int size;
        private int cap;

        public StringBuilderSlim(int n)
        {
            unsafe
            {
                cap = n;
                ptr = (byte*)UnsafeUtility.Malloc(n, 0, Allocator.Persistent);
                size = 0;
            }
        }

        public void Dispose()
        {
            unsafe
            {
                UnsafeUtility.Free(ptr, Allocator.Persistent);
                ptr = null;
            }
        }

        public unsafe int AppendUTF8(byte* data, int length)
        {
            var prevCap = cap;

            ReallocIfNeeded(size + length);

            if (size + length >= cap)
                UnityEngine.Debug.LogError($"{size} + {length} < {cap} . oldCap = {prevCap}");

            UnsafeUtility.MemCpy(ptr + size, data, length);
            size += length;

            return size;
        }

        public int AppendNewLine()
        {
            unsafe
            {
                ReallocIfNeeded(size + 2);
                ref var newLineChar = ref Builder.EnvNewLine.Data;

                var n = newLineChar.Length;
                for (var i = 0; i < n; i++)
                {
                    ptr[size++] = newLineChar.ElementAt(i);
                }
            }
            return size;
        }

        private void ReallocIfNeeded(int requiredSize)
        {
            unsafe
            {
                if (cap > requiredSize) return;

                while (cap <= requiredSize)
                    cap *= 2;

                var newPtr = (byte*)UnsafeUtility.Malloc(cap, 0, Allocator.Persistent);
                Assert.IsTrue(cap >= size);
                Assert.IsTrue(cap > requiredSize);
                UnsafeUtility.MemCpy(newPtr, ptr, size);
                UnsafeUtility.Free(ptr, Allocator.Persistent);
                ptr = newPtr;
            }
        }

        public void Clear()
        {
            size = 0;
        }

        public override string ToString()
        {
            unsafe
            {
                if (size == 0)
                    return "";
                return Encoding.UTF8.GetString(ptr, size);
            }
        }
    }
}
