using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging
{
    /// <summary>
    /// Ref struct to hide unsafe pointer logic
    /// </summary>
    public readonly ref struct BinaryParser
    {
        private readonly unsafe byte* Ptr;
        public readonly int LengthInBytes;

        public unsafe BinaryParser(void* ptrContextData, int payloadBufferLength)
        {
            OutOfBoundsArrayConstructor(ptrContextData, payloadBufferLength, 0);
            Ptr = (byte*)ptrContextData;
            LengthInBytes = payloadBufferLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek<T>() where T : unmanaged
        {
            unsafe
            {
                OutOfBoundsArrayConstructor(Ptr, LengthInBytes, UnsafeUtility.SizeOf<T>());
                return *(T*)Ptr;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteFormattedOutput<T>(ref UnsafeText hstring, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot) where T : unmanaged, IWriterFormattedOutput
        {
            unsafe
            {
                OutOfBoundsArrayConstructor(Ptr, LengthInBytes, UnsafeUtility.SizeOf<T>());
                ref var str = ref UnsafeUtility.AsRef<T>(Ptr);
                return str.WriteFormattedOutput(ref hstring, ref formatter, ref memAllocator, ref currArgSlot, 0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AppendUTF8StringToUnsafeText(ref UnsafeText hstring, ref FormatterStruct formatter, int stringBytes, ref ArgumentInfo currArgSlot)
        {
            if (LengthInBytes < 0) return false;
            if (LengthInBytes == 0) return true;

            unsafe
            {
                OutOfBoundsArrayConstructor(Ptr, LengthInBytes, stringBytes);
                return formatter.WriteUTF8String(ref hstring, Ptr, stringBytes, ref currArgSlot);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BinaryParser Skip(int bytes)
        {
            unsafe
            {
                OutOfBoundsArrayConstructor(Ptr, LengthInBytes, bytes);
                return new BinaryParser(Ptr + bytes, LengthInBytes - bytes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BinaryParser Skip<T>() where T : unmanaged
        {
            unsafe
            {
                var bytes = UnsafeUtility.SizeOf<T>();
                OutOfBoundsArrayConstructor(Ptr, LengthInBytes, bytes);
                return new BinaryParser(Ptr + bytes, LengthInBytes - bytes);
            }
        }

        public IntPtr Pointer { get { unsafe { return new IntPtr(Ptr); } } }

        public bool IsValid { get { unsafe { return Ptr != null && LengthInBytes > 0; } } }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe void OutOfBoundsArrayConstructor(void* ptr, int oldLengthBytes, int readBytes)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr), "BinaryParser.Ptr is null. Memory corruption detected");
            if (oldLengthBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(oldLengthBytes), $"oldLengthBytes is 0 or negative -> {oldLengthBytes}");
            if (readBytes != 0 && oldLengthBytes - readBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(readBytes), $"Reading out of range of BinaryParser: {oldLengthBytes}, read {readBytes} -> {oldLengthBytes - readBytes}");
#endif
        }
    }

    public interface IWriterFormattedOutput
    {
        bool WriteFormattedOutput(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth);
    }
}
