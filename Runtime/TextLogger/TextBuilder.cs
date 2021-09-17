using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IL2CPP.CompilerServices;
using Unity.Logging.Internal;
using UnityEngine;

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
        private static unsafe UnsafeText CreateText(string source, Allocator allocator)
        {
            var res = new UnsafeText(source.Length * 2 + 1, allocator) {Length = source.Length * 2};

            fixed(char* sourcePtr = source)
            {
                var error = UTF8ArrayUnsafeUtility.Copy(res.GetUnsafePtr(), out var actualBytes, res.Capacity, sourcePtr, source.Length);
                if (error != CopyError.None)
                {
                    return default;
                }
                res.Length = actualBytes;
            }
            return res;
        }

        /// <summary>
        /// Helper function to pack <see cref="string"/> into <see cref="LogMemoryManager"/>
        /// </summary>
        /// <param name="message">Input message</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static PayloadHandle BuildMessage(string message, ref LogMemoryManager memAllocator)
        {
            // Wrapper to handle managed string case: copy message into a native UnsafeListString and then call into the normal BuildMessage flow.
            // Obviously this code path won't be Burst compatible.

            PayloadHandle handle;
            var nativeMsg = new UnsafeText();

            try
            {
                nativeMsg = CreateText(message, Allocator.Temp);
                handle = BuildMessage(nativeMsg, ref memAllocator);
            }
            finally
            {
                if (nativeMsg.IsCreated)
                    nativeMsg.Dispose();
            }
            return handle;
        }

        /// <summary>
        /// Helper function to pack generic T into <see cref="LogMemoryManager"/>
        /// </summary>
        /// <param name="message">Input message</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <typeparam name="T"><see cref="IUTF8Bytes"/>, INativeList of byte</typeparam>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildMessage<T>(in T message, ref LogMemoryManager memAllocator) where T : IUTF8Bytes, INativeList<byte>
        {
            var allocSize = message.Length;

            // Message isn't long enough to reach min payload size
            if (allocSize < UnsafePayloadRingBuffer.MinimumPayloadSize)
                allocSize = (int)UnsafePayloadRingBuffer.MinimumPayloadSize;

            var handle = memAllocator.AllocatePayloadBuffer((uint)allocSize, out var buffer);
            if (handle.IsValid)
            {
                // Copy to payload
                var bufferptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                UnsafeUtility.MemCpy(bufferptr, message.GetUnsafePtr(), message.Length);

                // Write 0 for any padding bytes
                if (allocSize > message.Length)
                    UnsafeUtility.MemSet(&bufferptr[message.Length], 0, allocSize - message.Length);
            }
            return handle;
        }

        public static unsafe void BuildDecorators(ref LogController logController, LogControllerScopedLock @lock, ref FixedList512Bytes<PayloadHandle> handles)
        {
            fixed(FixedList512Bytes<PayloadHandle>* a = &handles)
            {
                var payload = LogContextWithDecorator.From512(a);
                BuildDecorators(ref logController, @lock, ref payload);
            }
        }

        public static unsafe void BuildDecorators(ref LogController logController, LogControllerScopedLock @lock, ref FixedList4096Bytes<PayloadHandle> handles)
        {
            fixed(FixedList4096Bytes<PayloadHandle>* a = &handles)
            {
                var payload = LogContextWithDecorator.From4096(a);
                BuildDecorators(ref logController, @lock, ref payload);
            }
        }

        public static unsafe void BuildDecorators(ref LogController logController, LogControllerScopedLock @lock, ref LogContextWithDecorator payload)
        {
            payload.Lock = @lock;
            payload.Lock.MustBeValid();

            // header first
            var allocSize = sizeof(ushort) * 3;
            Assert.IsFalse(allocSize < UnsafePayloadRingBuffer.MinimumPayloadSize);

            var nBefore = payload.Length;

            var headerHandle = logController.MemoryManager.AllocatePayloadBuffer((uint)allocSize, out var buffer);
            if (headerHandle.IsValid)
            {
                payload.Add(headerHandle);

                var localConstHandlesCount = logController.AddConstDecorateHandlers(payload);
                var globalConstHandlesCount = LoggerManager.AddConstDecorateHandlers(payload);

                // Execute handlers, so they can create payloads for `logController` and store them to `logController.MemoryManager`
                logController.ExecuteDecorateHandlers(payload);
                // Execute global handlers, so they can create payloads for `logController` and store them to `logController.MemoryManager`
                LoggerManager.ExecuteDecorateHandlers(payload);

                var total = payload.Length - nBefore - 1;

                Assert.IsTrue(localConstHandlesCount + globalConstHandlesCount <= total, "Wrong count in decors");
                Assert.IsTrue(total % 2 == 0, "Total is not even (message-value pairs)");

                var dataPtr = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                *dataPtr++ = localConstHandlesCount;
                *dataPtr++ = globalConstHandlesCount;
                *dataPtr = (ushort)total;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
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
                memAllocator.ReleasePayloadBuffer(payloadList[i], out var result, true);
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
            return BuildPrimitive(100, &fixedString, UnsafeUtility.SizeOf<FixedString32Bytes>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a FixedString
        /// </summary>
        /// <param name="fixedString">FixedString to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(FixedString64Bytes fixedString, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(101, &fixedString, UnsafeUtility.SizeOf<FixedString64Bytes>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a FixedString
        /// </summary>
        /// <param name="fixedString">FixedString to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(FixedString128Bytes fixedString, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(102, &fixedString, UnsafeUtility.SizeOf<FixedString128Bytes>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a FixedString
        /// </summary>
        /// <param name="fixedString">FixedString to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(FixedString512Bytes fixedString, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(103, &fixedString, UnsafeUtility.SizeOf<FixedString512Bytes>(), ref memAllocator);
        }

        /// <summary>
        /// Helper function to pack a FixedString
        /// </summary>
        /// <param name="fixedString">FixedString to pack</param>
        /// <param name="memAllocator">Memory manager</param>
        /// <returns>Handle for the created data</returns>
        public static unsafe PayloadHandle BuildContextSpecialType(FixedString4096Bytes fixedString, ref LogMemoryManager memAllocator)
        {
            return BuildPrimitive(104, &fixedString, UnsafeUtility.SizeOf<FixedString4096Bytes>(), ref memAllocator);
        }
    }
}
