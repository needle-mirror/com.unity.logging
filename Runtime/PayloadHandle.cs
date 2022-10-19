using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;

namespace Unity.Logging
{
    /// <summary>
    /// Holds a 64-bit value that uniquely references an allocated memory block within <see cref="LogMemoryManager"/>.
    /// </summary>
    /// <remarks>
    /// This is the primary field in <see cref="LogMessage"/>.
    /// The value is opaque and shouldn't be accessed externally. It encodes the following data/fields:
    /// [Offset - 32bits][Version - 16bits][BufferID - 8bits][BitFlags - 8 bits]
    ///
    /// Offset - Byte offset (index) within the RingBuffer of the allocated Payload chunk (includes header)
    /// Version - Handle validation value; must match Version within chunk header or handle is rejected
    /// BufferID - Identifies specific RingBuffer by an ID value (MemoryManager can maintain multiple RingBuffers)
    /// BitFields - Other flags an/or misc. data
    ///
    /// The handle holds a unique, single-use value that references a specific memory buffer under the control
    /// of <see cref="LogMemoryManager"/>. Once the referenced memory buffer is released, the handle becomes
    /// invalid and any attempt to retrieve the buffer will fail.
    /// </remarks>
    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PayloadHandle : IEquatable<PayloadHandle>
    {
        internal readonly ulong m_Value;

        private PayloadHandle(ulong initValue = 0)
        {
            m_Value = initValue;
        }

        /// <summary>
        /// Checks if the PayloadHandle value is valid.
        /// </summary>
        /// <remarks>
        /// Note this doesn't check if the PayloadHandle actually references a valid Payload buffer or not.
        /// An invalid handle typically indicates a Payload allocation failed.
        ///
        /// To check if the handle references a valid Payload buffer, call <see cref="LogMemoryManager.IsPayloadHandleValid(PayloadHandle)"/>
        /// </remarks>
        /// <returns>True if valid and false if not</returns>
        public bool IsValid => m_Value != 0;

        /// <summary>
        /// Checks if the PayloadHandle references a Disjointed Buffer.
        /// </summary>
        /// <remarks>
        /// A Disjointed Buffer is an allocation that contains PayloadHandle values to other allocations.
        /// This allows multiple allocated buffers to be combined into a single reference, but as a
        /// consequence the memory isn't contiguous and requires multiple handle look-ups to retrieve
        /// the entirety of the data.
        /// </remarks>
        /// <returns></returns>
        public bool IsDisjointedBuffer => (((m_Value & BitFieldsMask) >> BitFieldsShift) & DisjointedBufferFlag) != 0;


        internal const byte   BufferOffsetShift     = 32;
        internal const ulong BufferOffsetMask      = 0xFFFFFFFF00000000ul;

        internal const byte   BlockVersionShift     = 16;
        internal const ulong BlockVersionMask      = 0x00000000FFFF0000ul;

        internal const byte   BufferIdShift         = 8;
        internal const ulong BufferIdMask          = 0x000000000000FF00ul;

        internal const byte   BitFieldsShift        = 0;
        internal const ulong BitFieldsMask         = 0x00000000000000FFul;

        // Define internal handle bit-flags
        internal const ulong DisjointedBufferFlag  = 0x0000000000000001ul;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CreateHandleFromFields(ref PayloadHandleData data, out PayloadHandle handle)
        {
            handle = new PayloadHandle(
                ((ulong)data.Offset << BufferOffsetShift) |
                ((ulong)data.Version << BlockVersionShift) |
                ((ulong)data.BufferId << BufferIdShift) |
                ((ulong)data.BitFields << BitFieldsShift));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ExtractFieldsFromHandle(ref PayloadHandle handle, out PayloadHandleData data)
        {
            data.Offset = (uint)((handle.m_Value & BufferOffsetMask) >> BufferOffsetShift);
            data.Version = (ushort)((handle.m_Value & BlockVersionMask) >> BlockVersionShift);
            data.BufferId = (byte)((handle.m_Value & BufferIdMask) >> BufferIdShift);
            data.BitFields = (byte)((handle.m_Value & BitFieldsMask) >> BitFieldsShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte ExtractBufferIdFromHandle(ref PayloadHandle handle)
        {
            return (byte)((handle.m_Value & BufferIdMask) >> BufferIdShift);
        }

        /// <summary>
        /// Equals method that compares this PayloadHandle with another one
        /// </summary>
        /// <param name="other">PayloadHandle</param>
        /// <returns>true if they're equal</returns>
        public bool Equals(PayloadHandle other)
        {
            return m_Value == other.m_Value;
        }

        /// <summary>
        /// Hash function
        /// </summary>
        /// <returns>Hash value of this PayloadHandle</returns>
        public override int GetHashCode()
        {
            return (int)m_Value;
        }
    }


    [BurstCompile]
    internal struct PayloadHandleData
    {
        public uint Offset;
        public ushort Version;
        public byte BufferId;
        public byte BitFields;
    }

    /// <summary>
    /// Holds a value to identify a specific "context" for a Payload buffer lock.
    /// </summary>
    /// <remarks>
    /// This value is returned by <see cref="LogMemoryManager.LockPayloadBuffer(PayloadHandle)"/>
    /// and later passed into <see cref="LogMemoryManager.UnlockPayloadBuffer"/> to release the lock.
    /// </remarks>
    [BurstCompile]
    public readonly struct PayloadLockContext : IEquatable<PayloadLockContext>
    {
        internal readonly ulong Value;

        internal PayloadLockContext(byte currentLockCount)
        {
            Value = 1u << currentLockCount;
        }

        /// <summary>
        /// Returns if context value is valid or not.
        /// </summary>
        /// <remarks>
        /// An invalid context means call to <see cref="LogMemoryManager.LockPayloadBuffer(PayloadHandle)"/> failed.
        /// </remarks>
        public bool IsValid => Value != 0;

        /// <summary>
        /// Compares <see cref="PayloadLockContext"/> with another one
        /// </summary>
        /// <param name="other">Another <see cref="PayloadLockContext"/> to compare with</param>
        /// <returns>True if they are equal</returns>
        public bool Equals(PayloadLockContext other)
        {
            return Value == other.Value;
        }
    }

    [BurstCompile]
    internal struct PayloadBufferLockData
    {
        internal static readonly uint MaxContexts = 64;

        internal int LockCount => m_CurrCount;

        public PayloadLockContext CreateNewContext()
        {
            if (m_TotalCount >= MaxContexts)
                return new PayloadLockContext();

            // Each locking context is tracked by a bit-flag but cannot "reuse"
            // a given context value; so max of 64 total locks on a given buffer.
            var newContext = new PayloadLockContext((byte)m_TotalCount);
            m_Contexts |= newContext.Value;

            m_CurrCount++;
            m_TotalCount++;
            return newContext;
        }

        public bool RemoveLockContext(PayloadLockContext context)
        {
            // Check Context is valid and hasn't yet been released
            if ((m_Contexts & context.Value) == 0)
                return false;

            m_Contexts &= (~context.Value);
            m_CurrCount--;
            return true;
        }

        private ulong m_Contexts;
        private int m_CurrCount;
        private int m_TotalCount;
    }
}
