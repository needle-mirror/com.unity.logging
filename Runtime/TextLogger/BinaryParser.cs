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
        /// <summary>
        /// Pointer
        /// </summary>
        private readonly unsafe byte* Ptr;

        /// <summary>
        /// Length of the data in bytes
        /// </summary>
        public readonly int LengthInBytes;

        /// <summary>
        /// Creates the ref struct
        /// </summary>
        /// <param name="ptrContextData">Pointer to the data</param>
        /// <param name="payloadBufferLength">Length of the data in bytes</param>
        public unsafe BinaryParser(void* ptrContextData, int payloadBufferLength)
        {
            OutOfBoundsArrayConstructor(ptrContextData, payloadBufferLength, 0);
            Ptr = (byte*)ptrContextData;
            LengthInBytes = payloadBufferLength;
        }

        /// <summary>
        /// Reads the pointer as T. Checks out of bound read if debug checks are present
        /// </summary>
        /// <typeparam name="T">Unmanaged type</typeparam>
        /// <returns>T representation of binary data under Ptr</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek<T>() where T : unmanaged
        {
            unsafe
            {
                OutOfBoundsArrayConstructor(Ptr, LengthInBytes, UnsafeUtility.SizeOf<T>());
                return *(T*)Ptr;
            }
        }

        /// <summary>
        /// Reads the pointer as T that implements ILoggableMirrorStruct, and appends it to the <see cref="UnsafeText"/>. Checks out of bound read if debug checks are present
        /// </summary>
        /// <param name="hstring">UnsafeText where to append</param>
        /// <param name="formatter">Current formatter</param>
        /// <param name="memAllocator">Memory manager that holds binary representation of the mirror struct</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <typeparam name="T">Unmanaged struct that implements ILoggableMirrorStruct</typeparam>
        /// <returns>True if append was successful</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AppendToUnsafeText<T>(ref UnsafeText hstring, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot) where T : unmanaged, ILoggableMirrorStruct
        {
            unsafe
            {
                OutOfBoundsArrayConstructor(Ptr, LengthInBytes, UnsafeUtility.SizeOf<T>());
                ref var str = ref UnsafeUtility.AsRef<T>(Ptr);
                return str.AppendToUnsafeText(ref hstring, ref formatter, ref memAllocator, ref currArgSlot, 0);
            }
        }

        /// <summary>
        /// Reads the pointer as a UTF8 string and appends it to the <see cref="UnsafeText"/>. Checks out of bound read if debug checks are present
        /// </summary>
        /// <param name="hstring">UnsafeText where to append</param>
        /// <param name="formatter">Current formatter</param>
        /// <param name="stringLengthInBytes">Length of the UTF8 string in bytes</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if append was successful</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AppendUTF8StringToUnsafeText(ref UnsafeText hstring, ref FormatterStruct formatter, int stringLengthInBytes, ref ArgumentInfo currArgSlot)
        {
            if (LengthInBytes < 0) return false;
            if (LengthInBytes == 0) return true;

            unsafe
            {
                OutOfBoundsArrayConstructor(Ptr, LengthInBytes, stringLengthInBytes);
                return formatter.WriteUTF8String(ref hstring, Ptr, stringLengthInBytes, ref currArgSlot);
            }
        }

        /// <summary>
        /// Creates new BinaryParser that is a slice of the current one, but 'bytes' are skipped
        /// </summary>
        /// <param name="bytes">Bytes to skip</param>
        /// <returns>Slice of the current BinaryParser</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BinaryParser Skip(int bytes)
        {
            unsafe
            {
                OutOfBoundsArrayConstructor(Ptr, LengthInBytes, bytes);
                return new BinaryParser(Ptr + bytes, LengthInBytes - bytes);
            }
        }

        /// <summary>
        /// Creates new BinaryParser that is a slice of the current one, but SizeOf{T} are skipped
        /// </summary>
        /// <typeparam name="T">Unmanaged type, its size will be used to skip</typeparam>
        /// <returns>Slice of the current BinaryParser</returns>
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

        /// <summary>
        /// Safe IntPtr wrapper for the internal pointer
        /// </summary>
        public IntPtr Pointer { get { unsafe { return new IntPtr(Ptr); } } }

        /// <summary>
        /// True if Pointer is not null and length is bigger than 0
        /// </summary>
        public bool IsValid { get { unsafe { return Ptr != null && LengthInBytes > 0; } } }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe void OutOfBoundsArrayConstructor(void* ptr, int oldLengthBytes, int readBytes)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr), "BinaryParser.Ptr is null. Memory corruption detected");
            if (oldLengthBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(oldLengthBytes), $"oldLengthBytes is negative -> {oldLengthBytes}");
            if (readBytes != 0 && oldLengthBytes - readBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(readBytes), $"Reading out of range of BinaryParser: {oldLengthBytes}, read {readBytes} -> {oldLengthBytes - readBytes}");
#endif
        }
    }

    /// <summary>
    /// Interface that is used by Unity.Logging to understand how to convert a mirror structure into UnsafeText<br/>
    /// </summary>
    /// <remarks>
    /// This not typed variant is used inside Unity.Logging, for user usage see <see cref="ILoggableMirrorStruct{T}"/>
    /// </remarks>
    public interface ILoggableMirrorStruct
    {
        /// <summary>
        /// Method that defines how the origin type should be converted into text form in Unity.Logging. Similar to a ToString.
        /// </summary>
        /// <param name="output">Where to append</param>
        /// <param name="formatter">Current formatter that is used by the sink. Could be json/text/etc.</param>
        /// <param name="memAllocator">Memory manager that holds binary representation of the mirror struct</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <param name="depth">Current depth, it is a good idea to not append anything if depth is high to avoid stack overflow</param>
        /// <returns>True if append was successful, for instance no FormatErrors happened</returns>
        bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth);
    }

    /// <summary>
    /// Interface that is used by Unity.Logging to understand how to convert a mirror structure into UnsafeText. Low-level way to describe 'ToString'-like behavior for any type for Unity.Logging to use.
    /// </summary>
    /// <remarks>
    /// <para>This interface must be on a partial structure - then it means this partial structure is a mirror structure of type T.</para>
    /// <para>There are several requirements:</para>
    /// <para>- Multiple implementations of different ILoggableMirrorStruct on the same struct are not allowed.</para>
    /// <para>- First field of the structure must be <c>MirrorStructHeader</c></para>
    /// <para>- Structure must have an implicit operator that converts from T.</para>
    /// </remarks>
    /// <typeparam name="T">The original type that this mirror structure is for</typeparam>
    public interface ILoggableMirrorStruct<T> : ILoggableMirrorStruct
    {
    }
}
