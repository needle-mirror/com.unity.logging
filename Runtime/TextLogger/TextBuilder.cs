using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IL2CPP.CompilerServices;
using Unity.Logging.Internal;
using UnityEngine;
using UnityEngine.Scripting;

namespace Unity.Logging
{
    /// <summary>
    /// Helper struct that helps pack data into <see cref="LogMemoryManager"/>
    /// Will be extended by Source Generator (that's why it is partial)
    /// </summary>
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public struct Builder
    {
        /// <summary>
        /// FixedString32 that contains Environment.NewLine so Burst can read it
        /// </summary>
        public static readonly SharedStatic<FixedString32Bytes> EnvNewLine = SharedStatic<FixedString32Bytes>.GetOrCreate<FixedString32Bytes, Builder>(16);

        [BurstDiscard]
        internal static void Initialize()
        {
            EnvNewLine.Data = new FixedString32Bytes(Environment.NewLine);
        }

        /// <summary>
        /// Creates <see cref="UnsafeText"/> that has UTF8 representation of source string
        /// When CreateText is called in the burst - CreateText__Unmanaged is called instead
        /// </summary>
        /// <param name="utf8Ptr">UTF8 string pointer</param>
        /// /// <param name="utf8Length">UTF8 string's length in bytes</param>
        /// <param name="allocator">Allocator that should be used to allocate <see cref="UnsafeText"/></param>
        /// <returns><see cref="UnsafeText"/> that has UTF8 representation of source string</returns>
        [RequiredMember]
        public static unsafe UnsafeText CreateText__Unmanaged(byte* utf8Ptr, int utf8Length, Allocator allocator)
        {
            var res = new UnsafeText(utf8Length, allocator) { Length = utf8Length };
            UnsafeUtility.MemCpy(res.GetUnsafePtr(), utf8Ptr, utf8Length);
            return res;
        }

        /// <summary>
        /// Creates <see cref="UnsafeText"/> that has UTF8 representation of source string
        /// </summary>
        /// <param name="source">UTF16 string</param>
        /// <param name="allocator">Allocator that should be used to allocate <see cref="UnsafeText"/></param>
        /// <returns><see cref="UnsafeText"/> that has UTF8 representation of source string</returns>
        public static unsafe UnsafeText CreateText(string source, Allocator allocator)
        {
            fixed(char* sourcePtr = source)
            {
                var lengthBytes = source.Length * 2;

                var res = new UnsafeText(lengthBytes, allocator) { Length = lengthBytes };

                var error = UTF8ArrayUnsafeUtility.Copy(res.GetUnsafePtr(), out var actualBytes, res.Capacity, sourcePtr, source.Length);
                if (error != CopyError.None)
                {
                    res.Dispose();
                    return default;
                }

                res.Length = actualBytes;

                return res;
            }
        }

        /// <summary>
        /// Allocates memory in the <see cref="LogMemoryManager"/> and copies data into it with headers. Create copy of string message in UTF8 in PayloadBuffer, prepended with optional typeId and length
        /// </summary>
        /// <param name="message">UTF8 string to copy</param>
        /// <param name="memAllocator">MemoryManager where to allocate the <see cref="PayloadHandle"/></param>
        /// <param name="prependTypeId">If true - typeId will be added to payload</param>
        /// <param name="prependLength">If true - length in bytes will be added to payload</param>
        /// <param name="deferredRelease">If true - <see cref="PayloadHandle"/> will be registered for deferred release (after 2 updates)</param>
        /// <typeparam name="T">UTF8 collections string</typeparam>
        /// <returns><see cref="PayloadHandle"/> that holds the data</returns>
        public static unsafe PayloadHandle CopyCollectionStringToPayloadBuffer<T>(in T message, ref LogMemoryManager memAllocator, bool prependTypeId = false, bool prependLength = false, bool deferredRelease = false) where T : IUTF8Bytes, INativeList<byte>
        {
            var allocSize = message.Length;
            if (prependTypeId)
                allocSize += UnsafeUtility.SizeOf<ulong>();
            if (prependLength)
                allocSize += UnsafeUtility.SizeOf<int>();

            if (allocSize < UnsafePayloadRingBuffer.MinimumPayloadSize)
                allocSize = (int)UnsafePayloadRingBuffer.MinimumPayloadSize;

            var handle = memAllocator.AllocatePayloadBuffer((uint)allocSize, out var buffer);

            if (handle.IsValid)
            {
                var dataPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                var dataLen = allocSize;

                if (prependTypeId) {
                    ulong typeId = 200;
                    UnsafeUtility.MemCpy(&dataPtr[0], &typeId, UnsafeUtility.SizeOf<ulong>());

                    dataPtr += UnsafeUtility.SizeOf<ulong>();
                    dataLen -= UnsafeUtility.SizeOf<ulong>();
                }

                var actualBytes = message.Length;

                if (prependLength) {
                    var lenPtr = (int*)dataPtr;
                    *lenPtr = actualBytes;

                    dataPtr += UnsafeUtility.SizeOf<int>();
                    dataLen -= UnsafeUtility.SizeOf<int>();
                }

                var sourcePtr = message.GetUnsafePtr();
                UnsafeUtility.MemCpy(dataPtr, sourcePtr, actualBytes);

                if (actualBytes < dataLen)
                    UnsafeUtility.MemSet(&dataPtr[actualBytes], 0, dataLen - actualBytes);
                if (deferredRelease)
                    memAllocator.ReleasePayloadBufferDeferred(handle);
            }
            return handle;
        }

        /// <summary>
        /// Appends data from <see cref="PayloadHandle"/> as UTF8 string into <see cref="UnsafeText"/>
        /// </summary>
        /// <param name="output"><see cref="UnsafeText"/> append to</param>
        /// <param name="payloadHandle"><see cref="PayloadHandle"/> that has the UTF8 string</param>
        /// <param name="memAllocator"><see cref="LogMemoryManager"/> that owns the <see cref="PayloadHandle"/></param>
        /// <returns>True if append was successful</returns>
        public static bool AppendStringAsPayloadHandle(ref UnsafeText output, PayloadHandle payloadHandle, ref LogMemoryManager memAllocator)
        {
            if (memAllocator.RetrievePayloadBuffer(payloadHandle, out var buffer))
            {
                unsafe
                {
                    var bufferPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                    return output.Append(bufferPtr + UnsafeUtility.SizeOf<int>(), *(int*)bufferPtr) == FormatError.None;
                }
            }
            return false;
        }

        /// <summary>
        /// Allocates memory in the <see cref="LogMemoryManager"/> and copies data into it with headers.
        /// </summary>
        /// <remarks>
        /// Create a copy of string message in UTF8 in PayloadBuffer, prepended with optional typeId and length.
        /// When CopyStringToPayloadBuffer is called in the Burst code, CopyStringToPayloadBuffer__Unmanaged is called instead
        /// </remarks>
        /// <param name="source">String that stores the data in UTF16</param>
        /// <param name="memAllocator">MemoryManager where to allocate the <see cref="PayloadHandle"/></param>
        /// <param name="prependTypeId">If true - typeId will be added to payload</param>
        /// <param name="prependLength">If true - length in bytes will be added to payload</param>
        /// <param name="deferredRelease">If true - <see cref="PayloadHandle"/> will be registered for deferred release (after 2 updates)</param>
        /// <returns><see cref="PayloadHandle"/> that holds the data</returns>
        public static PayloadHandle CopyStringToPayloadBuffer(string source, ref LogMemoryManager memAllocator, bool prependTypeId = false, bool prependLength = false, bool deferredRelease = false)
        {
            var allocSize = source.Length * 2;
            if (prependTypeId)
                allocSize += UnsafeUtility.SizeOf<ulong>();
            if (prependLength)
                allocSize += UnsafeUtility.SizeOf<int>();

            if (allocSize < UnsafePayloadRingBuffer.MinimumPayloadSize)
                allocSize = (int)UnsafePayloadRingBuffer.MinimumPayloadSize;

            var handle = memAllocator.AllocatePayloadBuffer((uint)allocSize, out var buffer);

            if (handle.IsValid)
            {
                unsafe
                {
                    var dataPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                    var dataLen = allocSize;

                    var lenPtr = dataPtr;

                    if (prependTypeId) {
                        ulong typeId = 200;
                        UnsafeUtility.MemCpy(&dataPtr[0], &typeId, UnsafeUtility.SizeOf<ulong>());

                        dataPtr += UnsafeUtility.SizeOf<ulong>();
                        dataLen -= UnsafeUtility.SizeOf<ulong>();
                    }

                    if (prependLength) {
                        lenPtr = dataPtr;

                        dataPtr += UnsafeUtility.SizeOf<int>();
                        dataLen -= UnsafeUtility.SizeOf<int>();
                    }

                    fixed(char* sourcePtr = source)
                    {
                        var error = UTF8ArrayUnsafeUtility.Copy(dataPtr, out var actualBytes, allocSize, sourcePtr, source.Length);
                        if (error != CopyError.None)
                        {
                            memAllocator.ReleasePayloadBuffer(handle, out var _, true);
                            return default;
                        }

                        if (prependLength)
                            UnsafeUtility.MemCpy(&lenPtr[0], &actualBytes, UnsafeUtility.SizeOf<int>());

                        if (actualBytes < dataLen)
                            UnsafeUtility.MemSet(&dataPtr[actualBytes], 0, dataLen - actualBytes);
                    }
                }
                if (deferredRelease)
                    memAllocator.ReleasePayloadBufferDeferred(handle);
            }
            return handle;
        }

        /// <summary>
        /// Allocates memory in the <see cref="LogMemoryManager"/> and copies data into it with headers.
        /// </summary>
        /// <remarks>
        /// Create a copy of the string message in PayloadBuffer, prepended with optional typeId and length.
        /// When CopyStringToPayloadBuffer is called in Burst code, CopyStringToPayloadBuffer__Unmanaged is called instead.
        /// </remarks>
        /// <param name="sourcePtr">Pointer to data</param>
        /// <param name="sourceLength">Length of data in bytes</param>
        /// <param name="memAllocator">MemoryManager where to allocate the <see cref="PayloadHandle"/></param>
        /// <param name="prependTypeId">If true - typeId will be added to payload</param>
        /// <param name="prependLength">If true - length in bytes will be added to payload</param>
        /// <param name="deferredRelease">If true - <see cref="PayloadHandle"/> will be registered for deferred release (after 2 updates)</param>
        /// <returns><see cref="PayloadHandle"/> that holds the data</returns>
        [RequiredMember]
        public static unsafe PayloadHandle CopyStringToPayloadBuffer__Unmanaged(byte* sourcePtr, int sourceLength, ref LogMemoryManager memAllocator, bool prependTypeId = false, bool prependLength = false, bool deferredRelease = false)
        {
            var allocSize = sourceLength;
            if (prependTypeId)
                allocSize += UnsafeUtility.SizeOf<ulong>();
            if (prependLength)
                allocSize += UnsafeUtility.SizeOf<int>();

            if (allocSize < UnsafePayloadRingBuffer.MinimumPayloadSize)
                allocSize = (int)UnsafePayloadRingBuffer.MinimumPayloadSize;

            var handle = memAllocator.AllocatePayloadBuffer((uint)allocSize, out var buffer);

            if (handle.IsValid)
            {
                var dataPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                var dataLen = allocSize;

                var lenPtr = dataPtr;

                if (prependTypeId) {
                    ulong typeId = 200;
                    UnsafeUtility.MemCpy(&dataPtr[0], &typeId, UnsafeUtility.SizeOf<ulong>());

                    dataPtr += UnsafeUtility.SizeOf<ulong>();
                    dataLen -= UnsafeUtility.SizeOf<ulong>();
                }

                if (prependLength) {
                    lenPtr = dataPtr;

                    dataPtr += UnsafeUtility.SizeOf<int>();
                    dataLen -= UnsafeUtility.SizeOf<int>();
                }

                {
                    UnsafeUtility.MemCpy(dataPtr, sourcePtr, sourceLength);
                    var actualBytes = sourceLength;

                    if (prependLength)
                        UnsafeUtility.MemCpy(&lenPtr[0], &actualBytes, UnsafeUtility.SizeOf<int>());

                    if (actualBytes < dataLen)
                        UnsafeUtility.MemSet(&dataPtr[actualBytes], 0, dataLen - actualBytes);
                }
                if (deferredRelease)
                    memAllocator.ReleasePayloadBufferDeferred(handle);
            }
            return handle;
        }

        /// <summary>
        /// Helper function to pack <see cref="string"/> into <see cref="LogMemoryManager"/>
        /// </summary>
        /// <param name="message">Input message</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static PayloadHandle BuildMessage(string message, ref LogMemoryManager memAllocator)
        {
            PayloadHandle handle;
            var nativeMsg = new UnsafeText();
            try
            {
                nativeMsg = CreateText(message, (Jobs.LowLevel.Unsafe.JobsUtility.IsExecutingJob ? Allocator.Temp : Allocator.TempJob));
                handle = BuildMessage(nativeMsg, ref memAllocator);
            }
            finally
            {
                if (nativeMsg.IsCreated)
                    nativeMsg.Dispose();
            }
            return handle;
        }

        /// <summary> Helper function to pack generic T into <see cref="LogMemoryManager"/></summary>
        /// <remarks>
        /// When BuildMessage is called in Burst code, BuildMessage__Unmanaged is called instead
        /// </remarks>
        /// <param name="sourcePtr">Input message's ptr</param>
        /// /// <param name="sourceLength">Input message's length in bytes</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        [RequiredMember]
        public static unsafe PayloadHandle BuildMessage__Unmanaged(byte* sourcePtr, int sourceLength, ref LogMemoryManager memAllocator)
        {
            return BuildMessage(sourcePtr, sourceLength, ref memAllocator);
        }

        /// <summary> Helper function to pack generic T into <see cref="LogMemoryManager"/>
        /// </summary>
        /// <param name="message">Input message</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <typeparam name="T"><see cref="IUTF8Bytes"/>, INativeList of byte</typeparam>
        /// <returns>Handle for the created data</returns>
        public static PayloadHandle BuildMessage<T>(in T message, ref LogMemoryManager memAllocator) where T : IUTF8Bytes, INativeList<byte>
        {
            unsafe
            {
                return BuildMessage(message.GetUnsafePtr(), message.Length, ref memAllocator);
            }
        }

        /// <summary>
        /// Writes UTF8 string into <see cref="LogMemoryManager"/>
        /// </summary>
        /// <param name="msg"><see cref="NativeTextBurstWrapper"/> nativeText wrapper that has UTF8 string</param>
        /// <param name="memAllocator">Memory allocator that should allocate the <see cref="PayloadHandle"/> and store the UTF8 string data</param>
        /// <returns><see cref="PayloadHandle"/> that contains the copied UTF8 string's data</returns>
        public static PayloadHandle BuildMessage(NativeTextBurstWrapper msg, ref LogMemoryManager memAllocator)
        {
            return BuildMessage(msg.ptr, msg.len, ref memAllocator);
        }

        /// <summary>
        /// Writes UTF8 string into <see cref="LogMemoryManager"/>
        /// </summary>
        /// <param name="utf8Ptr">IntPtr pointer to UTF8 string data</param>
        /// <param name="utf8Length">Length of UTF8 string data</param>
        /// <param name="memAllocator">Memory allocator that should allocate the <see cref="PayloadHandle"/> and store the UTF8 string data</param>
        /// <returns><see cref="PayloadHandle"/> that contains the copied UTF8 string's data</returns>
        public static PayloadHandle BuildMessage(IntPtr utf8Ptr, int utf8Length, ref LogMemoryManager memAllocator)
        {
            unsafe
            {
                return BuildMessage((byte*)utf8Ptr.ToPointer(), utf8Length, ref memAllocator);
            }
        }

        /// <summary>
        /// Writes UTF8 string into <see cref="LogMemoryManager"/>
        /// </summary>
        /// <param name="utf8Ptr">Pointer to UTF8 string data</param>
        /// <param name="utf8Length">Length of UTF8 string data</param>
        /// <param name="memAllocator">Memory allocator that should allocate the <see cref="PayloadHandle"/> and store the UTF8 string data</param>
        /// <returns><see cref="PayloadHandle"/> that contains the copied UTF8 string's data</returns>
        public unsafe static PayloadHandle BuildMessage(byte* utf8Ptr, int utf8Length, ref LogMemoryManager memAllocator)
        {
            var allocSize = utf8Length;

            // Message isn't long enough to reach min payload size
            if (allocSize < UnsafePayloadRingBuffer.MinimumPayloadSize)
                allocSize = (int)UnsafePayloadRingBuffer.MinimumPayloadSize;

            var handle = memAllocator.AllocatePayloadBuffer((uint)allocSize, out var buffer);
            if (handle.IsValid)
            {
                // Copy to payload
                var bufferptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                UnsafeUtility.MemCpy(bufferptr, utf8Ptr, utf8Length);

                // Write 0 for any padding bytes
                if (allocSize > utf8Length)
                    UnsafeUtility.MemSet(&bufferptr[utf8Length], 0, allocSize - utf8Length);
            }

            return handle;
        }

        /// <summary>
        /// Adds decorators to <see cref="LogContextWithDecorator"/> payloads list
        /// </summary>
        /// <seealso cref="BuildDecorators(ref Unity.Logging.LogController,ref Unity.Logging.Internal.LogContextWithDecorator)"/>
        /// <param name="logController">LogController that owns the decorators</param>
        /// <param name="lock">Lock that controls logController's life time</param>
        /// <param name="handles">List of handles where to add decorators</param>
        public static unsafe void BuildDecorators(ref LogController logController, LogControllerScopedLock @lock, ref FixedList512Bytes<PayloadHandle> handles)
        {
            fixed(FixedList512Bytes<PayloadHandle>* a = &handles)
            {
                var payload = new LogContextWithDecorator(a, @lock);
                BuildDecorators(ref logController, ref payload);
            }
        }

        /// <summary>
        /// Adds decorators to <see cref="LogContextWithDecorator"/> payloads list
        /// </summary>
        /// <seealso cref="BuildDecorators(ref Unity.Logging.LogController,ref Unity.Logging.Internal.LogContextWithDecorator)"/>
        /// <param name="logController">LogController that owns the decorators</param>
        /// <param name="lock">Lock that controls logController's life time</param>
        /// <param name="handles">List of handles where to add decorators</param>
        public static unsafe void BuildDecorators(ref LogController logController, LogControllerScopedLock @lock, ref FixedList4096Bytes<PayloadHandle> handles)
        {
            fixed(FixedList4096Bytes<PayloadHandle>* a = &handles)
            {
                var payload = new LogContextWithDecorator(a, @lock);
                BuildDecorators(ref logController, ref payload);
            }
        }

        /// <summary>
        /// Adds decorators to <see cref="LogContextWithDecorator"/> payloads list
        /// </summary>
        /// <param name="logController">LogController that owns the decorators</param>
        /// <param name="payload">LogContextWithDecorator to add decorators to</param>
        public static unsafe void BuildDecorators(ref LogController logController, ref LogContextWithDecorator payload)
        {
            payload.Lock.MustBeValid();

            // header first
            var allocSize = sizeof(ushort) * 3;
            Assert.IsTrue(allocSize >= UnsafePayloadRingBuffer.MinimumPayloadSize);

            var nBefore = payload.Length;

            var headerHandle = logController.MemoryManager.AllocatePayloadBuffer((uint)allocSize, out var buffer);
            if (headerHandle.IsValid)
            {
                payload.Add(headerHandle);

                var localConstHandlesCount = logController.AddConstDecorateHandlers(payload);
                var globalConstHandlesCount = LoggerManager.AddConstDecorateHandlers(payload);

                // Execute handlers, so they can create payloads for `logController` and store them to `logController.MemoryManager`
                logController.ExecuteDecorateHandlers(ref payload);
                // Execute global handlers, so they can create payloads for `logController` and store them to `logController.MemoryManager`
                LoggerManager.ExecuteDecorateHandlers(ref payload);

                var total = payload.Length - nBefore - 1;

                Assert.IsTrue(localConstHandlesCount + globalConstHandlesCount <= total, "Wrong count in decors");
                Assert.IsTrue(total % 2 == 0, "Total is not even (message-value pairs)");

                var dataPtr = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                *dataPtr++ = localConstHandlesCount;
                *dataPtr++ = globalConstHandlesCount;
                *dataPtr = (ushort)total;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            for (int i = nBefore; i < payload.Length; i++)
            {
                Assert.IsTrue(logController.MemoryManager.IsPayloadHandleValid(payload.ElementAt(i)));
            }
#endif
        }

        /// <summary>
        /// Helper function to pack a special type of int
        /// </summary>
        /// <param name="special_int">Integer to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(int special_int, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(1, &special_int, UnsafeUtility.SizeOf<int>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of uint
        /// </summary>
        /// <param name="special_uint">UInt to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(uint special_uint, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(2, &special_uint, UnsafeUtility.SizeOf<uint>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of ulong
        /// </summary>
        /// <param name="special_ulong">ULong to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(ulong special_ulong, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(3, &special_ulong, UnsafeUtility.SizeOf<ulong>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of long
        /// </summary>
        /// <param name="special_long">Long to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(long special_long, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(4, &special_long, UnsafeUtility.SizeOf<long>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of char
        /// </summary>
        /// <param name="special_char">Char to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(char special_char, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(5, &special_char, UnsafeUtility.SizeOf<char>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of float
        /// </summary>
        /// <param name="special_float">Float to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(float special_float, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(6, &special_float, UnsafeUtility.SizeOf<float>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of double
        /// </summary>
        /// <param name="special_double">Double to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(double special_double, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(7, &special_double, UnsafeUtility.SizeOf<double>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of bool
        /// </summary>
        /// <param name="special_bool">bool to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(bool special_bool, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(8, &special_bool, UnsafeUtility.SizeOf<bool>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of decimal
        /// </summary>
        /// <param name="special_decimal">Decimal to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(decimal special_decimal, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(9, &special_decimal, UnsafeUtility.SizeOf<decimal>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of short
        /// </summary>
        /// <param name="special_short">short to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(short special_short, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(10, &special_short, UnsafeUtility.SizeOf<short>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of ushort
        /// </summary>
        /// <param name="special_ushort">Ushort to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(ushort special_ushort, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(11, &special_ushort, UnsafeUtility.SizeOf<ushort>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of sbyte
        /// </summary>
        /// <param name="special_sbyte">sbyte to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(sbyte special_sbyte, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(12, &special_sbyte, UnsafeUtility.SizeOf<sbyte>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of byte
        /// </summary>
        /// <param name="special_byte">Byte to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(byte special_byte, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(13, &special_byte, UnsafeUtility.SizeOf<byte>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of IntPtr
        /// </summary>
        /// <param name="special_intptr">Byte to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(IntPtr special_intptr, ref LogMemoryManager memAllocator)
        {
            var l = special_intptr.ToInt64();
            return BuildPrimitive(14, &l, UnsafeUtility.SizeOf<long>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a special type of UIntPtr
        /// </summary>
        /// <param name="special_uintptr">Byte to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(UIntPtr special_uintptr, ref LogMemoryManager memAllocator)
        {
            var l = special_uintptr.ToUInt64();
            return BuildPrimitive(15, &l, UnsafeUtility.SizeOf<ulong>(), ref memAllocator);
        }

        private static unsafe PayloadHandle BuildPrimitive(ulong typeId, void* argPtr, int argSize, ref LogMemoryManager memAllocator)
        {
            var typeIdSize = UnsafeUtility.SizeOf<ulong>();

            var structSize = argSize + typeIdSize;
            var allocSize = structSize;

            // Context has data but not enough to reach min Payload size requirement
            if (allocSize < UnsafePayloadRingBuffer.MinimumPayloadSize)
                allocSize = (int)UnsafePayloadRingBuffer.MinimumPayloadSize;

            var handle = memAllocator.AllocatePayloadBuffer((uint)allocSize, out var buffer);
            if (handle.IsValid)
            {
                // Copy to payload
                var bufferptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                UnsafeUtility.MemCpy(&bufferptr[0], &typeId, typeIdSize);
                UnsafeUtility.MemCpy(&bufferptr[typeIdSize], argPtr, argSize);

                // Write 0 for any padding bytes
                if (allocSize > structSize)
                    UnsafeUtility.MemSet(&bufferptr[structSize], 0, allocSize - structSize);
            }

            // Return Payload Log builder
            return handle;
        }

        /// <summary>
        /// Helper function to pack a struct
        /// </summary>
        /// <param name="structure">Struct to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <typeparam name="T">Structure type</typeparam>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContext<T>(in T structure, ref LogMemoryManager memAllocator) where T : unmanaged
        {
            var structSize = UnsafeUtility.SizeOf<T>();
            var allocSize = structSize;

            // Context has data but not enough to reach min Payload size requirement
            if (allocSize < UnsafePayloadRingBuffer.MinimumPayloadSize)
                allocSize = (int)UnsafePayloadRingBuffer.MinimumPayloadSize;


            var handle = memAllocator.AllocatePayloadBuffer((uint)allocSize, out var buffer);
            if (handle.IsValid)
            {
                // Copy to payload
                var bufferptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                UnsafeUtility.CopyStructureToPtr(ref UnsafeUtilityExtensions.AsRef(structure), bufferptr);

                // Write 0 for any padding bytes
                if (allocSize > structSize)
                    UnsafeUtility.MemSet(&bufferptr[structSize], 0, allocSize - structSize);
            }

            // Return Payload Log builder
            return handle;
        }

        /// <summary>
        /// Force release memory for payloads
        /// </summary>
        /// <param name="payloadList">List of payloads</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <typeparam name="T">INativeList of PayloadHandle</typeparam>
        public static void ForceReleasePayloads<T>(in T payloadList, ref LogMemoryManager memAllocator) where T : INativeList<PayloadHandle>
        {
            for (var i = 0; i < payloadList.Length; i++)
            {
                memAllocator.ReleasePayloadBuffer(payloadList[i], out _, true);
            }
        }

        /// <summary>
        /// Helper function to pack a FixedString
        /// </summary>
        /// <param name="fixedString">FixedString to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(FixedString32Bytes fixedString, ref LogMemoryManager memAllocator)
        {
            return BuildSizeAndData(fixedString.GetUnsafePtr(), fixedString.Length, ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a FixedString
        /// </summary>
        /// <param name="fixedString">FixedString to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(FixedString64Bytes fixedString, ref LogMemoryManager memAllocator)
        {
            return BuildSizeAndData(fixedString.GetUnsafePtr(), fixedString.Length, ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a FixedString
        /// </summary>
        /// <param name="fixedString">FixedString to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(FixedString128Bytes fixedString, ref LogMemoryManager memAllocator)
        {
            return BuildSizeAndData(fixedString.GetUnsafePtr(), fixedString.Length, ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a FixedString
        /// </summary>
        /// <param name="fixedString">FixedString to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(FixedString512Bytes fixedString, ref LogMemoryManager memAllocator)
        {
            return BuildSizeAndData(fixedString.GetUnsafePtr(), fixedString.Length, ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a FixedString
        /// </summary>
        /// <param name="fixedString">FixedString to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(FixedString4096Bytes fixedString, ref LogMemoryManager memAllocator)
        {
            return BuildSizeAndData(fixedString.GetUnsafePtr(), fixedString.Length, ref memAllocator);
        }

        static unsafe PayloadHandle BuildSizeAndData(byte* data, int length, ref LogMemoryManager memAllocator)
        {
            ulong typeId = 110;

            var typeIdSize = UnsafeUtility.SizeOf<ulong>();
            var lenSize = UnsafeUtility.SizeOf<int>();

            var structSize = length + typeIdSize + lenSize;
            var allocSize = structSize;

            // Context has data but not enough to reach min Payload size requirement
            if (allocSize < UnsafePayloadRingBuffer.MinimumPayloadSize)
                allocSize = (int)UnsafePayloadRingBuffer.MinimumPayloadSize;

            var handle = memAllocator.AllocatePayloadBuffer((uint)allocSize, out var buffer);
            if (handle.IsValid)
            {
                // Copy to payload
                var bufferptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                // type id
                UnsafeUtility.MemCpy(&bufferptr[0], &typeId, typeIdSize);
                // length
                UnsafeUtility.MemCpy(&bufferptr[typeIdSize], &length, lenSize);
                // utf8 data
                UnsafeUtility.MemCpy(&bufferptr[typeIdSize+lenSize], data, length);

                // Write 0 for any padding bytes
                if (allocSize > structSize)
                    UnsafeUtility.MemSet(&bufferptr[structSize], 0, allocSize - structSize);
            }

            // Return Payload Log builder
            return handle;
        }

        /// <summary>
        /// Helper function to pack an UnsafeText
        /// </summary>
        /// <param name="unsafeText">UnsafeText to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(UnsafeText unsafeText, ref LogMemoryManager memAllocator)
        {
            var dataLen = unsafeText.Length;
            var data = unsafeText.GetUnsafePtr();

            return BuildSizeAndData(data, dataLen, ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a NativeText
        /// </summary>
        /// <param name="nativeText">NativeText to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(NativeText nativeText, ref LogMemoryManager memAllocator)
        {
            var dataLen = nativeText.Length;
            var data = nativeText.GetUnsafePtr();

            return BuildSizeAndData(data, dataLen, ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a NativeTextWrapper
        /// </summary>
        /// <param name="nativeTextWrapper">NativeTextBurstWrapper to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(NativeTextBurstWrapper nativeTextWrapper, ref LogMemoryManager memAllocator)
        {
            var dataLen = nativeTextWrapper.len;
            var data = (byte*)nativeTextWrapper.ptr.ToPointer();

            return BuildSizeAndData(data, dataLen, ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a string saved in a PayloadBuffer
        /// </summary>
        /// <param name="payloadHandle">PayloadHandle to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(PayloadHandle payloadHandle, ref LogMemoryManager memAllocator)
        {
            if (memAllocator.RetrievePayloadBuffer(payloadHandle, out var buffer))
            {
                var bufferPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                *(ulong*)bufferPtr = 200;
            }
            return payloadHandle;
        }
    }
}
