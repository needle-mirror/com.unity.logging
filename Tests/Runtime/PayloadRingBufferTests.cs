using System;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Logging;

namespace Unity.Logging.Tests
{
    [TestFixture]
    public class PayloadRingBufferTests
    {
        const int testData1 = -54321;
        const ulong testData2 = 0x123456789ABCDEF1;
        const float testData3 = 3.1415926f;
        const double testData4 = 3.14159265358979323d;

        const string testData5 = "The quick brown fox jumps over the lazy dog.";
        const string testData6 = "Quizdeltagerne spiste jordbær med fløde, mens cirkusklovnen Wolther spillede på xylofon.";
        const string testData7 = "Portez ce vieux whisky au juge blond qui fume sur son île intérieure, à\n" +
            "côté de l'alcôve ovoïde, où les bûches se consument dans l'âtre, ce\n" +
            "qui lui permet de penser à la cænogenèse de l'être dont il est question\n" +
            "dans la cause ambiguë entendue à Moÿ, dans un capharnaüm qui,\n" +
            "pense-t - il, diminue çà et là la qualité de son œuvre.";

        [Test]
        public void PayloadAllocationsAndDataAreCorrect()
        {
            var ascii = Encoding.ASCII;
            var unicode = Encoding.Unicode;

            var srcBuffer1 = BitConverter.GetBytes(testData1);
            var srcBuffer2 = BitConverter.GetBytes(testData2);
            var srcBuffer3 = BitConverter.GetBytes(testData3);
            var srcBuffer4 = BitConverter.GetBytes(testData4);
            var srcBuffer5 = ascii.GetBytes(testData5);
            var srcBuffer6 = unicode.GetBytes(testData6);
            var srcBuffer7 = unicode.GetBytes(testData7);

            CreateRingBufferAndValidateInstance(1024 * 4, 2, Allocator.Persistent, out var ringBuffer);
            try
            {
                // IMPORTANT: The order of these allocations matters! Must match up with Release/Reclaim check below
                var handle1 = AllocateBufferAndCopyTestData(ref ringBuffer, srcBuffer1);
                var handle6 = AllocateBufferAndCopyTestData(ref ringBuffer, srcBuffer6);
                var handle4 = AllocateBufferAndCopyTestData(ref ringBuffer, srcBuffer4);
                var handle5 = AllocateBufferAndCopyTestData(ref ringBuffer, srcBuffer5);
                var handle2 = AllocateBufferAndCopyTestData(ref ringBuffer, srcBuffer2);
                var handle7 = AllocateBufferAndCopyTestData(ref ringBuffer, srcBuffer7);
                var handle3 = AllocateBufferAndCopyTestData(ref ringBuffer, srcBuffer3);

                ValidateRingBufferAllocationSize(ref ringBuffer, srcBuffer1, srcBuffer2, srcBuffer3, srcBuffer4, srcBuffer5, srcBuffer6, srcBuffer7);

                RetrieveTestDataAndValidate(ref ringBuffer, handle1, testData1);
                RetrieveTestDataAndValidate(ref ringBuffer, handle2, testData2);
                RetrieveTestDataAndValidate(ref ringBuffer, handle3, testData3);
                RetrieveTestDataAndValidate(ref ringBuffer, handle4, testData4);
                RetrieveTestDataAndValidate(ref ringBuffer, handle5, testData5, ascii);
                RetrieveTestDataAndValidate(ref ringBuffer, handle6, testData6, unicode);
                RetrieveTestDataAndValidate(ref ringBuffer, handle7, testData7, unicode);

                // IMPORTANT: The order of Release/Reclaim checks matter!
                // Memory can only be reclaimed from a contiguous set of released blocks at the end (Tail) of the ring
                // Current allocation: [1][6][4][5][2][7][3]
                ringBuffer.ReleasePayload(handle6);
                ringBuffer.ReleasePayload(handle2);
                ringBuffer.ReleasePayload(handle4);
                ringBuffer.ReclaimReleasedPayloadBlocks();

                // We didn't release the "Tail" block and so no memory should have been reclaimed
                // Current allocation: [1][-6][-4][5][-2][7][3]
                ValidateRingBufferAllocationSize(ref ringBuffer, srcBuffer1, srcBuffer6, srcBuffer4, srcBuffer3, srcBuffer2, srcBuffer7, srcBuffer5);

                ringBuffer.ReleasePayload(handle1);
                ringBuffer.ReleasePayload(handle3);
                ringBuffer.ReclaimReleasedPayloadBlocks();

                // Reclaimed some of the blocks:
                // Current allocation: [5][-2][7][-3]
                ValidateRingBufferAllocationSize(ref ringBuffer, srcBuffer5, srcBuffer2, srcBuffer7, srcBuffer3);

                ringBuffer.ReleasePayload(handle5);
                ringBuffer.ReclaimReleasedPayloadBlocks();

                // Reclaimed more blocks:
                // Current allocation: [7][-3]
                ValidateRingBufferAllocationSize(ref ringBuffer, srcBuffer7, srcBuffer3);

                ringBuffer.ReleasePayload(handle7);
                ringBuffer.ReclaimReleasedPayloadBlocks();

                // Reclaimed final blocks:
                // Current allocation: <empty>
                ValidateRingBufferAllocationSize(ref ringBuffer);

                // All memory should have been reclaimed
                RingControl.ValidateRingControlValues(ref ringBuffer, RingControl.Create(0, 0, (int)ringBuffer.Capacity - 1, 0));
                Assert.IsTrue(ringBuffer.IsRingBufferEmpty());

                // Allocate filler blocks to test wrap-around allocations
                Assert.IsTrue(ringBuffer.AllocatePayload(1024 * 1, out var filler1, out var fillerArray));
                Assert.IsTrue(ringBuffer.AllocatePayload(1024 * 1, out var filler2, out fillerArray));
                Assert.IsTrue(ringBuffer.AllocatePayload(1024 * 1, out var filler3, out fillerArray));

                // Allocate one more blocks so that we only have 50 bytes of space left
                uint remainingSpace = ringBuffer.Capacity - (1024 * 3);
                Assert.That(remainingSpace > 256);
                Assert.IsTrue(ringBuffer.AllocatePayload(remainingSpace - 256, out var filler4, out fillerArray));

                // Release filler1 block so there's room at the tail of the buffer
                Assert.IsTrue(ringBuffer.ReleasePayload(filler1));
                ringBuffer.ReclaimReleasedPayloadBlocks();

                // Since this test string is longer than 256 bytes, we expect it to have been allocated at
                // the beginning (tail) of the array; the handle should have an offset of 0
                var wrapAroundHandle = AllocateBufferAndCopyTestData(ref ringBuffer, unicode.GetBytes(testData7));
                Assert.That((wrapAroundHandle.m_Value & PayloadHandle.BufferOffsetMask) == 0, "Payload block wasn't allocated at beginning of array");
                RetrieveTestDataAndValidate(ref ringBuffer, wrapAroundHandle, testData7, unicode);

                // Attempt to allocate another 1024 block, which should fail because there insufficient space
                Assert.IsFalse(ringBuffer.AllocatePayload(1024, out var failedAlloc, out fillerArray));

                DisposeRingBufferAndValidate(ref ringBuffer);
            }
            finally
            {
                if (ringBuffer.IsCreated)
                    ringBuffer.Dispose();
            }
        }

        [TestCase(0, 1024 * 2u, 500000, 10u, 1000u, Description = "Basic buffer allocation range")]
        [TestCase(0, 1024 * 64u, 7000, 10u, 20u, Description = "Large buffer small allocations")]
        [TestCase(0, 1024 * 1u, 1000, 300u, 1000u, Description = "Small buffer large allocations")]
        [TestCase(0, 1024 * 1u, 100, 64u, 64u, Description = "Allocations exactly fit buffer")]
        [TestCase(0, 1024 * 1u, 5, 1024u, 1024u, Description = "Allocations same size as buffer")]
        [TestCase(0, 1024 * 64u, 200000, 10u, 1024 * 4u, Description = "Large buffer with large allocation range")]
        public void LowLevelAllocationsAreCorrect(int randSeed, uint bufferCapacity, int numOperations, uint minPayloadAlloc = 10, uint maxPayloadAlloc = 1000)
        {
            Allocator allocator = Allocator.Persistent;

            if (randSeed == 0)
                randSeed = Environment.TickCount;

            NUnit.Framework.TestContext.Out.WriteLine("Random seed: 0x{0:X}", randSeed);

            var allocationQueue = new NativeQueue<AllocationReference>(Allocator.Persistent);
            Assert.IsTrue(allocationQueue.IsCreated);

            var ringBuffer = new UnsafePayloadRingBuffer(bufferCapacity, 2, allocator);
            Assert.IsTrue(ringBuffer.IsCreated);

            try
            {
                var control = RingControl.CreateFromRingBuffer(ref ringBuffer);
                var rand = new Random(randSeed);

                int numActions;
                int actionFailureCount = 0;
                while (numOperations > 0)
                {
                    // Failsafe in case something goes horribly wrong
                    // If we're continuously unable to perform any actions then fail
                    if (actionFailureCount > 50)
                    {
                        Assert.Fail("Unable to perform an Allocation or Release actions");
                    }

                    // Perform a set of "actions" for either allocating or releasing blocks
                    numActions = (rand.Next() % 8) + 1;

                    if (numActions > numOperations)
                        numActions = numOperations;

                    // Randomly choose the action type: Alloc or Free (unless queue is empty)
                    if ((rand.Next() % 2) == 0 && allocationQueue.Count > 0)
                    {
                        while (numActions > 0)
                        {
                            if (!allocationQueue.TryDequeue(out var allocation))
                            {
                                actionFailureCount++;
                                break;
                            }

                            ReleaseLowLevelAndValidate(ref ringBuffer, ref control, allocation);

                            numActions--;
                            numOperations--;
                            actionFailureCount = 0;
                        }
                    }
                    else
                    {
                        while (numActions > 0)
                        {
                            uint size = (uint)rand.Next((int)minPayloadAlloc, (int)maxPayloadAlloc);

                            // If this fails should mean ringBuffer is full (validation checks will verify this)
                            if (!AllocateLowLevelAndValidate(ref ringBuffer, ref control, size, out var allocation))
                            {
                                actionFailureCount++;
                                break;
                            }

                            allocationQueue.Enqueue(allocation);
                            numActions--;
                            numOperations--;
                            actionFailureCount = 0;
                        }
                    }
                } // while (numOperations > 0)
            }
            finally
            {
                ringBuffer.Dispose();
                allocationQueue.Dispose();
            }
        }

        [Test]
        public void WriteToReadOnlyMemoryBlockFails()
        {
            CreateRingBufferAndValidateInstance(1024 * 2, 2, Allocator.Persistent, out var ringBuffer);
            try
            {
                var srcBuffer = BitConverter.GetBytes(testData1);
                var handle = AllocateBufferAndCopyTestData(ref ringBuffer, srcBuffer);

                bool result = ringBuffer.RetrievePayloadFromHandle(handle, true, out var buffer);
                Assert.IsTrue(result);

                Assert.DoesNotThrow(() =>
                {
                    buffer[0] = 3;
                });

                result = ringBuffer.RetrievePayloadFromHandle(handle, false, out buffer);
                Assert.IsTrue(result);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.Throws<InvalidOperationException>(() =>
                {
                    buffer[0] = 7;
                }, "Read-only NativeArray should have thrown InvalidOperationException");
#endif
            }
            finally
            {
                if (ringBuffer.IsCreated)
                    ringBuffer.Dispose();
            }
        }

        [Test]
        public void RetrieveMemoryBlockWithInvalidHandleFails()
        {
            CreateRingBufferAndValidateInstance(1024 * 2, 2, Allocator.Persistent, out var ringBuffer);
            try
            {
                var srcBuffer = Encoding.ASCII.GetBytes(testData5);
                var handle = AllocateBufferAndCopyTestData(ref ringBuffer, srcBuffer);

                bool result = ringBuffer.RetrievePayloadFromHandle(handle, out var array);
                Assert.IsTrue(result);

                PayloadHandle.ExtractFieldsFromHandle(ref handle, out var goodData);

                // Bad offset within the RingBuffer's range
                {
                    var badData = goodData;
                    badData.Offset = 3;

                    PayloadHandle.CreateHandleFromFields(ref badData, out var badHandle);

                    result = ringBuffer.RetrievePayloadFromHandle(badHandle, out array);
                    Assert.IsFalse(result);
                    Assert.Zero(array.Length);
                }

                // Bad offset a little outside RingBuffer's range
                {
                    var badData = goodData;
                    badData.Offset = ringBuffer.Capacity + 10;

                    PayloadHandle.CreateHandleFromFields(ref badData, out var badHandle);

                    result = ringBuffer.RetrievePayloadFromHandle(badHandle, out array);
                    Assert.IsFalse(result);
                    Assert.Zero(array.Length);
                }

                // Bad offset way outside RingBuffer's range (invalid memory access)
                {
                    var badData = goodData;
                    badData.Offset = 0xFD08;

                    PayloadHandle.CreateHandleFromFields(ref badData, out var badHandle);

                    result = ringBuffer.RetrievePayloadFromHandle(badHandle, out array);
                    Assert.IsFalse(result);
                    Assert.Zero(array.Length);
                }

                // Bad version
                {
                    var badData = goodData;
                    badData.Version = 5;

                    PayloadHandle.CreateHandleFromFields(ref badData, out var badHandle);

                    result = ringBuffer.RetrievePayloadFromHandle(badHandle, out array);
                    Assert.IsFalse(result);
                    Assert.Zero(array.Length);
                }

                // Bad buffer ID
                {
                    var badData = goodData;
                    badData.Version = 27;

                    PayloadHandle.CreateHandleFromFields(ref badData, out var badHandle);

                    result = ringBuffer.RetrievePayloadFromHandle(badHandle, out array);
                    Assert.IsFalse(result);
                    Assert.Zero(array.Length);
                }

                result = ringBuffer.ReleasePayload(handle);
                Assert.IsTrue(result);
                result = ringBuffer.RetrievePayloadFromHandle(handle, out array);
                Assert.IsFalse(result);
                Assert.Zero(array.Length);
            }
            finally
            {
                if (ringBuffer.IsCreated)
                    ringBuffer.Dispose();
            }
        }

        private void CreateRingBufferAndValidateInstance(UInt32 capacity, byte bufferId, Allocator allocator, out UnsafePayloadRingBuffer ringBuffer)
        {
            ringBuffer = new UnsafePayloadRingBuffer(capacity, bufferId, allocator);

            Assert.IsTrue(ringBuffer.IsCreated);
            Assert.AreEqual(capacity, ringBuffer.Capacity);
            Assert.AreEqual(bufferId, ringBuffer.BufferId);
            Assert.AreEqual(0, ringBuffer.BytesAllocated);

            RingControl.ValidateRingControlValues(ref ringBuffer, RingControl.Create(0, 0, (int)ringBuffer.Capacity - 1, ringBuffer.BytesAllocated));
        }

        private void DisposeRingBufferAndValidate(ref UnsafePayloadRingBuffer ringBuffer)
        {
            Assert.IsTrue(ringBuffer.IsCreated);

            ringBuffer.Dispose();

            Assert.IsFalse(ringBuffer.IsCreated);
        }

        private void ValidateRingBufferAllocationSize(ref UnsafePayloadRingBuffer ringBuffer, params byte[][] srcBuffers)
        {
            uint expectedBytesUsed = 0;
            foreach (byte[] array in srcBuffers)
            {
                expectedBytesUsed += UnsafePayloadRingBuffer.RoundToNextAlign((uint)array.Length) + (int)PayloadBlockHeader.HeaderSize;
            }
            Assert.AreEqual(expectedBytesUsed, ringBuffer.BytesAllocated);
        }

        private PayloadHandle AllocateBufferAndCopyTestData(ref UnsafePayloadRingBuffer ringBuffer, byte[] srcData)
        {
            ringBuffer.AllocatePayload((uint)srcData.Length, out var handle, out var array);
            array.CopyFrom(srcData);

            return handle;
        }

        private void RetrieveTestDataAndValidate<T>(ref UnsafePayloadRingBuffer ringBuffer, PayloadHandle handle, T expectedValue, Encoding encoding = null)
        {
            Assert.IsTrue(ringBuffer.RetrievePayloadFromHandle(handle, out var array));

            switch (expectedValue)
            {
                case Int32 iValue:
                    Assert.AreEqual(iValue, BitConverter.ToInt32(array.ToArray(), 0));
                    break;

                case UInt64 ulValue:
                    Assert.AreEqual(ulValue, BitConverter.ToUInt64(array.ToArray(), 0));
                    break;

                case Single fValue:
                    Assert.AreEqual(fValue, BitConverter.ToSingle(array.ToArray(), 0));
                    break;

                case Double dValue:
                    Assert.AreEqual(dValue, BitConverter.ToDouble(array.ToArray(), 0));
                    break;

                case String sValue:
                    Assert.IsNotNull(encoding);
                    Assert.AreEqual(sValue, encoding.GetString(array.ToArray()));
                    break;

                default:
                    Assert.Fail("Invalid Type for expectedValue: " + expectedValue.ToString());
                    break;
            }
        }

        private unsafe bool AllocateLowLevelAndValidate(ref UnsafePayloadRingBuffer ringBuffer, ref RingControl expectedData, uint size, out AllocationReference allocation)
        {
            var memoryBlock = ringBuffer.AllocatePayloadBlock(size, out var offset, out _);
            bool result = memoryBlock != null;

            bool expectedResult = expectedData.SimulateAlloc(size, out var expectedOffset);

            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(expectedOffset, offset);
            RingControl.ValidateRingControlValues(ref ringBuffer, expectedData);

            allocation = new AllocationReference
            {
                MemoryBlock = memoryBlock,
                Size = size,
                Offset = offset
            };
            return result;
        }

        private unsafe void ReleaseLowLevelAndValidate(ref UnsafePayloadRingBuffer ringBuffer, ref RingControl expectedData, AllocationReference allocation)
        {
            var currData = RingControl.CreateFromRingBuffer(ref ringBuffer);
            byte* buffer = ringBuffer.GetUnsafePointerToBuffer();
            bool isTail = &buffer[currData.Tail] == allocation.MemoryBlock;

            ringBuffer.FreePayloadBlock(allocation.Size, out var isEmpty);

            expectedData.SimulateFree(allocation.Size);

            // Verify we didn't underflow Allocated count
            Assert.IsTrue(expectedData.Allocated >= 0);

            Assert.AreEqual(isEmpty, expectedData.Allocated == 0);
            RingControl.ValidateRingControlValues(ref ringBuffer, expectedData);
        }
    }
}
