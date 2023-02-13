using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging
{
    /// <summary>
    /// Logging system's primary container for allocating Payload buffers.
    /// </summary>
    /// <remarks>
    /// This container provides the backing memory for message buffers referenced by <see cref="LogMessage"/>.
    /// It allows fast, thread-safe allocations from a pre-allocated array of native memory, which can be safely
    /// accessed by a <see cref="PayloadHandle"/> value.
    ///
    /// This container is utilized internally by <see cref="LogMemoryManager"/>, and in general it's unnecessary to directly
    /// call into it. However, it may be used directly in advanced memory scenarios.
    /// </remarks>
    [BurstCompile]
    public unsafe struct UnsafePayloadRingBuffer : IDisposable
    {
        [NativeDisableUnsafePtrRestriction]
        private byte*                m_Buffer;

        private volatile int         m_Head;
        private volatile int         m_Tail;
        private volatile int         m_Fence;

        private uint                 m_Capacity;
        private Allocator            m_Allocator;

        private long                 m_VersionCounter;

        private volatile uint        m_BytesAllocated;

        private volatile uint        m_BytesAllocatedMax;

        /// <summary>
        /// Provides thread safety for the low-level allocation and release
        /// </summary>
        internal SpinLockReadWrite   m_AllocationLock;


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // These safety handles control read/write access for returned NativeArray "views"
        internal AtomicSafetyHandle  m_BlockReadOnlyHandle;
        internal AtomicSafetyHandle  m_BlockReadWriteHandle;
#endif

        /// <summary>
        /// Minimum capacity of the container.
        /// </summary>
        /// <remarks>
        /// In order to function properly the RingBuffer requires minimum capacity of 1K.
        /// </remarks>
        public const uint MinimumCapacity = 1024 * 1;

        /// <summary>
        /// Maximum capacity of the container.
        /// </summary>
        /// <remarks>
        /// Limit capacity to max 2^27 bytes == 134 mb because RingBuffer's arithmetic (Distance) uses signed 32-bit int and we don't want to crash in 32 bit platforms, also too much will crash with OOM
        /// </remarks>
        public const uint MaximumCapacity = 134217728; // 2^27 bytes == 134 mb

        /// <summary>
        /// Minimum size for a single Payload block (excludes header) that can be allocated.
        /// </summary>
        public const uint MinimumPayloadSize = 4;

        /// <summary>
        /// Maximum size for a single Payload block (excludes header) that can be allocated.
        /// </summary>
        public const uint MaximumPayloadSize = 1024 * 32;

        /// <summary>
        /// Capacity of the RingBuffer, which cannot change after RingBuffer has been initialized
        /// </summary>
        public uint Capacity => m_Capacity;

        /// <summary>
        /// Total number of bytes currently allocated from the RingBuffer, including payload headers.
        /// </summary>
        public uint BytesAllocated => m_BytesAllocated;

        /// <summary>
        /// Max value of <see cref="BytesAllocated"/> that was registered.
        /// </summary>
        public uint BytesAllocatedMax => m_BytesAllocatedMax;

        /// <summary>
        /// Returns true if RingBuffer is initialized and not Disposed.
        /// </summary>
        public bool IsCreated => m_Buffer != null;

        /// <summary>
        /// Returns the maximum number of Payloads that can be referenced within a single DisjointedBuffer.
        /// </summary>
        /// <remarks>
        /// The <see cref="MaximumPayloadSize"/> determines how may <see cref="PayloadHandle"/> values can fit into the "head" buffer and therefore
        /// the maximum number of payloads that can be referenced.
        /// </remarks>
        public static uint MaximumDisjointedPayloadCount => (uint)(MaximumPayloadSize / (float)UnsafeUtility.SizeOf<PayloadHandle>());

        /// <summary>
        /// Unique ID value assigned by the user which identifies PayloadHandle as referencing allocations from this instance.
        /// </summary>
        public readonly byte BufferId;

        /// <summary>
        /// Initializes a new instance of the container.
        /// </summary>
        /// <param name="capacity">Size of the container in bytes, must be in the range of <see cref="MinimumCapacity"/> and <see cref="MaximumCapacity"/>.</param>
        /// <param name="bufferId">User specified value that identifies this instance.</param>
        /// <param name="allocator">Memory pool to allocate the containers from.</param>
        public UnsafePayloadRingBuffer(uint capacity, byte bufferId, Allocator allocator = Allocator.Persistent)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            // Native allocation is only valid for Temp, Job and Persistent.
            if (allocator <= Allocator.None || allocator > Allocator.Persistent)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));

            if (capacity < MinimumCapacity || capacity > MaximumCapacity)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be within minimum and maximum sizes.");
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // AtomicSafetyHandles are read/write by default
            m_BlockReadWriteHandle = AtomicSafetyHandle.Create();

            // To enforce read-only, switch to the "secondary" version
            // This is automatically performed in Jobs, but our read/write access isn't based on Jobs
            m_BlockReadOnlyHandle = AtomicSafetyHandle.Create();
            AtomicSafetyHandle.SetAllowSecondaryVersionWriting(m_BlockReadOnlyHandle, false);
            AtomicSafetyHandle.UseSecondaryVersion(ref m_BlockReadOnlyHandle);
#endif
            m_Buffer = (byte*)UnsafeUtility.Malloc(capacity, UnsafeUtility.AlignOf<byte>(), allocator);
            m_Allocator = allocator;
            m_Capacity = capacity;
            m_VersionCounter = 0;
            m_Head = 0;
            m_Tail = 0;
            m_Fence = (int)capacity - 1;
            BufferId = bufferId;
            m_BytesAllocated = 0;
            m_BytesAllocatedMax = 0;

            m_AllocationLock = new SpinLockReadWrite(Allocator.Persistent);
        }

        /// <summary>
        /// Frees the container's memory and returns this instance to an uninitialized state.
        /// </summary>
        /// <remarks>
        /// This must be called from the main thread.
        /// </remarks>
        public void Dispose()
        {
            if (!IsCreated)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (!UnsafeUtility.IsValidAllocator(m_Allocator))
                throw new InvalidOperationException("The RingBuffer can not be Disposed because it was not allocated with a valid allocator.");
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_BlockReadWriteHandle);
            AtomicSafetyHandle.Release(m_BlockReadOnlyHandle);
#endif
            // Even though RingBuffer is "Unsafe" we'll at least ensure we don't free the memory
            // while in the process of allocating/releasing a payload block

            m_AllocationLock.Dispose();
            UnsafeUtility.Free(m_Buffer, m_Allocator);
            m_Buffer = null;
            m_Capacity = 0;
            m_Head = 0;
            m_Tail = 0;
            m_Fence = 0;
            m_BytesAllocated = 0;
            m_BytesAllocatedMax = 0;
        }

        /// <summary>
        /// Power of 2 align value used in <see cref="RoundToNextAlign"/>
        /// </summary>
        public const uint AlignTo = 8;

        /// <summary>
        /// Changes the input size to the size that is aligned to <see cref="AlignTo"/> bytes
        /// </summary>
        /// <param name="requestedSize">Input size to align</param>
        /// <returns>Aligned size</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RoundToNextAlign(uint requestedSize)
        {
            const uint bytes = AlignTo - 1; // 8 bit align
            const uint mask = ~bytes;
            return (requestedSize + bytes) & mask; // increase till requestedSize % bytes == 0
        }

        /// <summary>
        /// Allocate a block of memory from the container.
        /// </summary>
        /// <remarks>
        /// Actual size of the allocation is slightly larger than requestedSize, as additional bytes are needed for the
        /// memory block's header.
        /// </remarks>
        /// <param name="requestedSize">Size of memory block in bytes to allocate.</param>
        /// <param name="payloadHandle">Handle to the memory block, if allocation was successful.</param>
        /// <param name="payloadArray">NativeArray allowing read/write access into the memory block.</param>
        /// <param name="disjointedBuffer">True to specify allocation is the "head" of a Disjointed Payload.</param>
        /// <returns>True if allocation was successful and false if not</returns>
        public bool AllocatePayload(uint requestedSize, out PayloadHandle payloadHandle, out NativeArray<byte> payloadArray, bool disjointedBuffer = false)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (requestedSize < MinimumPayloadSize || requestedSize > MaximumPayloadSize)
            {
                Internal.Debug.SelfLog.OnFailedToAllocatePayloadBecauseOfItsSize(requestedSize);
                payloadHandle = default;
                payloadArray = default;
                return false;
            }
#endif
            // Add extra bytes to accommodate the header
            uint totalSize = RoundToNextAlign(requestedSize) + PayloadBlockHeader.HeaderSize;

            // Attempt to allocate the memory block
            var payloadBlock = AllocatePayloadBlock(totalSize, out var offset, out var versionNumber);
            if (payloadBlock == null)
            {
                payloadHandle = new PayloadHandle();
                payloadArray = new NativeArray<byte>();
                return false;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            {
                var ptr = new IntPtr(payloadBlock).ToInt64();
                if (ptr % AlignTo != 0)
                    throw new InvalidOperationException($"AllocatePayloadBlock {ptr} is not {AlignTo} bytes aligned. ptr % AlignTo = {ptr % AlignTo}, expected to be 0");
            }
#endif

            // Allocation was successful; initialize the header data and create a Handle to this allocation
            PayloadBlockFlags flags =
                disjointedBuffer ? PayloadBlockFlags.DisjointedBufferHeadFlag : 0;

            var payloadBlockHeader = (PayloadBlockHeader*)payloadBlock;
            payloadBlockHeader->PayloadSize = (ushort)requestedSize;
            payloadBlockHeader->TotalSize = totalSize;

            var version = (ushort)(versionNumber % PayloadBlockHeader.MaxVersionValue + 1);

            payloadBlockHeader->InitializeControl(version, flags);
            var payload = payloadBlock + PayloadBlockHeader.HeaderSize;

            // Set appropriate handle bit-fields
            // NOTE: PayloadHandle flags are defined as UInt64 type but only occupy the first 8-bits of the handle value
            var bitfields = (flags & PayloadBlockFlags.DisjointedBufferHeadFlag) != 0 ? PayloadHandle.DisjointedBufferFlag : 0;

            var payloadHandleData = new PayloadHandleData
            {
                Offset = (uint)offset,
                Version = version,
                BufferId = BufferId,
                BitFields = (byte)bitfields
            };

            PayloadHandle.CreateHandleFromFields(ref payloadHandleData, out payloadHandle);

            // Create a NativeArray as a view into this new block, which is passed out to the user
            // This array is given read/write access automatically, but by default arrays retrieved later (using the handle) will be read only
            payloadArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(payload, (int)requestedSize, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref payloadArray, m_BlockReadWriteHandle);
#endif
            return true;
        }

        /// <summary>
        /// Releases the memory block referenced by the specified handle.
        /// </summary>
        /// <remarks>
        /// This method doesn't actually release the memory but instead marks the block as being "freed". The memory
        /// is actually released by <see cref="ReclaimReleasedPayloadBlocks"/>, which must be called from a system Update.
        /// </remarks>
        /// <param name="payloadHandle">Handle value that references the memory block to be released.</param>
        /// <returns>True if successful and false if not.</returns>
        public bool ReleasePayload(PayloadHandle payloadHandle)
        {
            if (!GetPayloadBlockFromHandleAndValidate(payloadHandle, out var header, out _))
                return false;

            // Tag this block as "released" allowing RingBuffer to reclaim the memory
            // This will invalidate the handle and this block can no longer be accessed
            header->Control = PayloadBlockHeader.ControlReleasedTag;
            return true;
        }

        /// <summary>
        /// Reclaims any and all freed memory blocks from the "tail" of the RingBuffer.
        /// </summary>
        /// <remarks>
        /// Released memory doesn't actually become available for new allocations until this is called.
        /// It must be called from the main thread, typically once per frame.
        /// </remarks>
        public void ReclaimReleasedPayloadBlocks()
        {
            // IMPORTANT: This function is not (in of itself) thread-safe.
            //
            // While the low-level block allocations are thread-safe, this is a higher-level
            // operation that can only be called from one thread at a time and isn't safe with
            // other high-level operation, e.g. Dispose().

            // Check memory block pointed to by Tail to see if it can be released
            // We can only reclaim memory once the Tail block is released
            var currHeader = (PayloadBlockHeader*)&m_Buffer[m_Tail];
            if (currHeader->Control != PayloadBlockHeader.ControlReleasedTag)
                return;

            do
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                // Validate the size is correct, again if not something is very wrong
                if (currHeader->PayloadSize > MaximumPayloadSize)
                    throw new InvalidOperationException("Logging RingBuffer has hit an invalid state with Payload allocations.");
#endif
                FreePayloadBlock(currHeader->TotalSize, out var isEmpty);

                // RingBuffer is empty so we're done
                if (isEmpty)
                    break;

                // Point currHeader to next allocation and check if it's been released or not
                // If not then we're done, otherwise continue to free this block
                currHeader = (PayloadBlockHeader*)&m_Buffer[m_Tail];
                if (currHeader->Control != PayloadBlockHeader.ControlReleasedTag)
                    break;
            }
            while (true);
        }

        /// <summary>
        /// Retrieves a NativeArray for the allocated memory block referenced by the specified handle.
        /// </summary>
        /// <remarks>
        /// NativeArray is read-only.
        /// See <see cref="RetrievePayloadFromHandle(PayloadHandle, bool, out NativeArray{byte})"/>.
        /// </remarks>
        /// <param name="blockHandle">Valid handle to the memory block to retrieve.</param>
        /// <param name="blockArray">If successful, NativeArray as a view into the memory block.</param>
        /// <returns>True if successfully retrieved memory block and false if not.</returns>
        public bool RetrievePayloadFromHandle(PayloadHandle blockHandle, out NativeArray<byte> blockArray)
        {
            return RetrievePayloadFromHandle(blockHandle, false, out blockArray);
        }

        /// <summary>
        /// Retrieves a NativeArray for the allocated memory block referenced by the specified handle.
        /// </summary>
        /// <remarks>
        /// The returned NativeArray is a view into the allocated memory block and not a copy, allows for safe
        /// access to them memory block. Access to the NativeArray can be specified as either read-only or read-write.
        ///
        /// NOTE: Since the NativeArray is a view into a segment of native memory, a buffer overrun will corrupt the
        /// container causing undefined behavior.
        /// </remarks>
        /// <param name="blockHandle">Valid handle to the memory block to retrieve.</param>
        /// <param name="readWriteAccess">True to give read-write access to the NativeArray and false for read-only access.</param>
        /// <param name="blockArray">If successful, NativeArray as a view into the memory block.</param>
        /// <returns>True if successfully retrieved memory block and false if not.</returns>
        public bool RetrievePayloadFromHandle(PayloadHandle blockHandle, bool readWriteAccess, out NativeArray<byte> blockArray)
        {
            if (!GetPayloadBlockFromHandleAndValidate(blockHandle, out var header, out var payload))
            {
                blockArray = new NativeArray<byte>();
                return false;
            }

            // Payload block handle is good; create a NativeArray to reference into the Payload and set read/write access according to passed in parameter
            blockArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(payload, header->PayloadSize, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref blockArray, readWriteAccess ? m_BlockReadWriteHandle : m_BlockReadOnlyHandle);
#endif
            return true;
        }

        /// <summary>
        /// Checks if the specified handle currently references a valid memory block.
        /// </summary>
        /// <remarks>
        /// Use this method to quickly check the validity of a handle value without the overhead of creating a NativeArray.
        /// A valid handle means it references an allocated memory block and that block hasn't yet been released.
        /// </remarks>
        /// <param name="blockHandle">A <see cref="PayloadHandle"/> value to check.</param>
        /// <returns>True if the handle references a valid memory block and false if not.</returns>
        public bool IsPayloadHandleValid(PayloadHandle blockHandle)
        {
            // Validates the passed in handle references an active memory block without the overhead
            // of creating a NativeArray

            return GetPayloadBlockFromHandleAndValidate(blockHandle, out _, out _);
        }

        internal byte* AllocatePayloadBlock(uint size, out int offset, out ulong version)
        {
            // NOTE: Payload blocks must be in contiguous memory and cannot wrap-around.
            // So allocations may still fail even if there's technically enough space.

            version = 0;

            // ReSharper disable once NonAtomicCompoundOperator
            // SpinLockExclusive makes this atomic
            using (var _ = new SpinLockReadWrite.ScopedExclusiveLock(m_AllocationLock))
            {
                // If Head is "behind" tail then check if enough space in the back of the Buffer
                if (m_Head < m_Tail && (m_Head + size) >= (m_Tail))
                {
                    offset = -1;
                    return null;
                }

                if (m_Head >= m_Tail && (m_Head + size) > m_Capacity)
                {
                    // Not enough space at the back, but check if enough space in front
                    // If not then we cannot allocate (RingBuffer state doesn't change).
                    if (size > m_Tail - 1)
                    {
                        offset = -1;
                        return null;
                    }

                    // Mark current Head as the "fence"; there are no other allocations beyond this
                    // point in the Buffer and any unused space is (unfortunately) wasted
                    m_Fence = m_Head;
                    m_Head = 0;
                }

                // Allocate from Head's current position
                var payload = &m_Buffer[m_Head];
                offset = m_Head;
                m_Head += (int)size;

                // ReSharper disable once NonAtomicCompoundOperator
                // SpinLockExclusive makes this atomic
                m_BytesAllocated += size;

                if (m_BytesAllocatedMax < m_BytesAllocated)
                    m_BytesAllocatedMax = m_BytesAllocated;

                var newValue = Interlocked.Increment(ref m_VersionCounter);
                version = unchecked((ulong) newValue);

                return payload;
            }
        }

        internal void FreePayloadBlock(uint size, out bool isEmpty)
        {
            // ReSharper disable once NonAtomicCompoundOperator
            // SpinLockExclusive makes this atomic
            using (var _ = new SpinLockReadWrite.ScopedExclusiveLock(m_AllocationLock))
            {
                // Advance Tail index by Size taken in the block's header
                // NOTE: The caller is responsible for passing in the correct size of the block
                m_Tail += (int)size;

                // Check if Tail has reached the fence, indicating it must wrap-around
                // UNLESS Head is also at the Fence; Tail must not pass Head
                if (m_Tail != m_Head && m_Tail >= m_Fence)
                {
                    m_Fence = (int)m_Capacity - 1;
                    m_Tail = 0;
                }

                // If this was the last allocated block then reset indexes and return RingBuffer is empty
                if (m_Tail == m_Head || m_Head == 0)
                {
                    m_Head = 0;
                    m_Tail = 0;
                    m_Fence = (int)m_Capacity - 1;
                    isEmpty = true;
                }
                else
                    isEmpty = false;

                // ReSharper disable once NonAtomicCompoundOperator
                // SpinLockExclusive makes this atomic
                m_BytesAllocated -= size;
            }
        }

        internal bool IsRingBufferEmpty()
        {
            if (!IsCreated)
                return true;

            // Safely check if RingBuffer is empty at this specific time

            using (var _ = new SpinLockReadWrite.ScopedReadLock(m_AllocationLock))
            {
                return m_Tail == m_Head;
            }
        }

        /// <summary>
        /// Debug function that returns details about a particular <see cref="PayloadHandle"/>.
        /// </summary>
        /// <param name="blockHandle"><see cref="PayloadHandle"/> to analyze</param>
        /// <returns><see cref="FixedString4096Bytes"/> with the details</returns>
        public FixedString4096Bytes DebugDetailsOfPayloadHandle(PayloadHandle blockHandle)
        {
            // NOTE: This function doesn't need to take the AllocationLock because it neither reads nor modifies
            // the ring control fields (Head, Tail, etc.).

            PayloadHandle.ExtractFieldsFromHandle(ref blockHandle, out var payloadHandleData);

            var result = (FixedString4096Bytes)"[PayloadHandle] value=<";
            result.Append(blockHandle.m_Value);
            result.Append((FixedString64Bytes)"> ");

            result.Append((FixedString64Bytes)"Offset = <");
            result.Append(payloadHandleData.Offset);
            result.Append((FixedString64Bytes)"> Version = <");
            result.Append(payloadHandleData.Version);
            result.Append((FixedString64Bytes)"> BufferId = <");
            result.Append(payloadHandleData.BufferId);
            result.Append((FixedString64Bytes)"> BitFields = <");
            result.Append(payloadHandleData.BitFields);
            result.Append((FixedString64Bytes)"> ");

            if (payloadHandleData.Offset >= m_Capacity)
            {
                result.Append((FixedString64Bytes)"INVALID! Offset <");
                result.Append(payloadHandleData.Offset);
                result.Append((FixedString64Bytes)"> >= Capacity <");
                result.Append(m_Capacity);
                result.Append((FixedString64Bytes)">");

                return result;
            }

            byte* blockStart = &m_Buffer[payloadHandleData.Offset];

            var header = (PayloadBlockHeader*)blockStart;

            if (header->IsDisjointedBufferHead)
            {
                result.Append((FixedString64Bytes)"[DisjointedBufferHead]");
            }

            // - Handle's ID matches this RingBuffer
            if (payloadHandleData.BufferId != BufferId)
            {
                result.Append((FixedString64Bytes)"[INVALID! payloadHandleData.BufferId <");
                result.Append(payloadHandleData.BufferId);
                result.Append((FixedString64Bytes)"> != BufferId <");
                result.Append(BufferId);
                result.Append((FixedString64Bytes)">]");
            }

            // - Handle's Version value matches referenced block (hasn't been deallocated)
            if (header->Version != payloadHandleData.Version)
            {
                result.Append((FixedString64Bytes)"[INVALID! header->Version <");
                result.Append(header->Version);
                result.Append((FixedString64Bytes)"> != payloadHandleData.Version <");
                result.Append(payloadHandleData.Version);
                result.Append((FixedString64Bytes)">]");
            }

            // - Sanity check that payload size is valid
            if (header->PayloadSize > MaximumPayloadSize)
            {
                result.Append((FixedString64Bytes)"[INVALID! header->PayloadSize <");
                result.Append(header->PayloadSize);
                result.Append((FixedString64Bytes)"> > MaximumPayloadSize <");
                result.Append(MaximumPayloadSize);
                result.Append((FixedString64Bytes)">]");
            }
            else
            {
                result.Append((FixedString64Bytes)"[header->PayloadSize <");
                result.Append(header->PayloadSize);
                result.Append((FixedString64Bytes)">]");
            }

            if (header->IsReleased)
            {
                result.Append((FixedString64Bytes)"[RELEASED!]");
            }

            return result;
        }

        internal bool GetPayloadBlockFromHandleAndValidate(PayloadHandle blockHandle, out PayloadBlockHeader* header, out byte* payload)
        {
            // NOTE: This function doesn't need to take the AllocationLock because it neither reads nor modifies
            // the ring control fields (Head, Tail, etc.).

            PayloadHandle.ExtractFieldsFromHandle(ref blockHandle, out var payloadHandleData);

            byte* blockStart = &m_Buffer[payloadHandleData.Offset];

            header = (PayloadBlockHeader*)blockStart;
            payload = blockStart + PayloadBlockHeader.HeaderSize;

            // Check that this handle references a valid Payload block
            // - Offset within buffer's range; memory access exception can be thrown if attempt to read memory outside of the buffer
            // - Handle's ID matches this RingBuffer
            // - Handle's Version value matches referenced block (hasn't been deallocated)
            // - Sanity check that payload size is valid
            if (payloadHandleData.Offset >= m_Capacity || payloadHandleData.BufferId != BufferId || header->Version != payloadHandleData.Version || header->PayloadSize > MaximumPayloadSize)
            {
                header = null;
                payload = null;
                return false;
            }

            return true;
        }

        internal void GetRingControlData(out int head, out int tail, out int fence)
        {
            using (var _ = new SpinLockReadWrite.ScopedReadLock(m_AllocationLock))
            {
                head = m_Head;
                tail = m_Tail;
                fence = m_Fence;
            }
        }

        internal byte* GetUnsafePointerToBuffer()
        {
            return m_Buffer;
        }
    }

    [BurstCompile]
    [StructLayout(LayoutKind.Sequential, Size = (short)HeaderSize)]
    internal struct PayloadBlockHeader
    {
        public const uint HeaderSize = UnsafePayloadRingBuffer.AlignTo;

        /// <summary>
        /// Size of the memory block requested by the user, i.e. Payload
        /// </summary>
        public volatile ushort PayloadSize;

        /// <summary>
        /// Holds bit-fields and control values used to validate handles and track allocation state.
        /// </summary>
        /// <remarks>
        /// bits 0 - 11 : holds allocation Version used to validate PayloadHandle
        /// bit 12 : DisjointedBuffer Head flag; allocation block is the "head" of a combo buffer
        /// bit 13 : Reserved for future flag
        /// bit 14 : Reserved for future flag
        /// bit 15 : Reserved for future flag
        ///
        /// NOTE: The value 0xFFFF is special and indicates buffer has been release but not yet reclaimed; it cannot
        /// be accessed ever again.
        /// </remarks>
        public volatile ushort Control;

        /// <summary>
        /// Real allocated amount of memory (alignment padding, header)
        /// </summary>
        public volatile uint TotalSize;


        public ushort Version => (ushort)(Control & 0xFFF);

        /// <summary>
        /// Maximum Version number allowed for allocation blocks
        /// </summary>
        public const ushort MaxVersionValue = 0xFFD;

        /// <summary>
        /// Value indicates allocated block has been released but not yet reclaimed
        /// </summary>
        public const ushort ControlReleasedTag = 0xFFFF;

        public bool IsDisjointedBufferHead => (Control & (ushort)PayloadBlockFlags.DisjointedBufferHeadFlag) != 0 && (!IsReleased);

        public bool IsReleased => Control == ControlReleasedTag;

        public void InitializeControl(ushort version, PayloadBlockFlags flags)
        {
            Control = (ushort)(((ushort)flags & 0xF000) | (version & 0xFFF));
        }
    }

    /// <summary>
    /// Flags for configuring Payload allocation blocks
    /// </summary>
    /// <remarks>
    /// Only flags defined for the upper 4 bits (of UInt16) can be saved in the allocation block Header, due to
    /// the limited space. Other flags may be defined and used for configuring allocations, but these won't be written
    /// to the header.
    /// </remarks>
    [Flags]
    internal enum PayloadBlockFlags : ushort
    {
        /// <summary>
        /// Specifies the allocation will be the "head" of a Disjointed buffer.
        /// </summary>
        /// <remarks>
        /// The DisjointedBuffer head holds the handle values for all the other allocations
        /// that make up the entire buffer.
        /// </remarks>
        DisjointedBufferHeadFlag = 0x1000,
    }
}
