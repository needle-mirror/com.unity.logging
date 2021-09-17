using System;
using NUnit.Framework;
using Unity.Logging;

namespace Unity.Logging.Tests
{
    /// <summary>
    /// Helper struct to track RingBuffer control data (Head, Tail, Capacity, etc.) and help validate RingBufferAllocations
    /// </summary>
    public struct RingControl
    {
        public int Head;
        public int Tail;
        public int Fence;
        public Int64 Allocated;
        public Int64 Capacity;

        public bool SimulateAlloc(uint size, out int offset)
        {
            if (Head >= Tail)
            {
                if (Head + size <= Capacity)
                {
                    // Allocate from the "front" of the buffer
                    offset = Head;
                    Head += (int)size;
                }
                else if (size < Tail)
                {
                    // Wrap-around and allocate from the "back" of the buffer
                    Fence = Head;
                    Head = 0;
                    offset = Head;
                    Head += (int)size;
                }
                else
                {
                    // No more space!
                    offset = -1;
                    return false;
                }
            }
            else if (Head + size < Tail)
            {
                // Allocate from the "back" of the buffer
                offset = Head;
                Head += (int)size;
            }
            else
            {
                // No more space!
                offset = -1;
                return false;
            }

            Allocated += size;
            return true;
        }

        public void SimulateFree(uint size)
        {
            Tail += (int)size;
            if (Tail != Head && Tail >= Fence)
            {
                Tail = 0;
                Fence = (int)Capacity - 1;
            }

            if (Tail == Head || Head == 0)
            {
                // RingBuffer is empty so reset control values
                Head = 0;
                Tail = 0;
                Fence = (int)Capacity - 1;
            }

            Allocated -= size;
        }

        public static RingControl Create(int head, int tail, int fence, Int64 allocated = 0, Int64 capacity = UnsafePayloadRingBuffer.MinimumCapacity)
        {
            return new RingControl
            {
                Head = head,
                Tail = tail,
                Fence = fence,
                Allocated = allocated,
                Capacity = capacity,
            };
        }

        public static RingControl CreateFromRingBuffer(ref UnsafePayloadRingBuffer ringBuffer)
        {
            int head, tail, fence;
            ringBuffer.GetRingControlData(out head, out tail, out fence);

            return new RingControl
            {
                Head = head,
                Tail = tail,
                Fence = fence,
                Allocated = ringBuffer.BytesAllocated,
                Capacity = ringBuffer.Capacity
            };
        }

        public static void ValidateRingControlValues(ref UnsafePayloadRingBuffer ringBuffer, RingControl expected)
        {
            int head, tail, fence;
            ringBuffer.GetRingControlData(out head, out tail, out fence);

            Assert.AreEqual(expected.Head, head);
            Assert.AreEqual(expected.Tail, tail);
            Assert.AreEqual(expected.Fence, fence);
            Assert.AreEqual(expected.Allocated, ringBuffer.BytesAllocated);
        }
    }

    public unsafe struct AllocationReference
    {
        public byte* MemoryBlock;
        public uint Size;
        public int Offset;
    }
}
