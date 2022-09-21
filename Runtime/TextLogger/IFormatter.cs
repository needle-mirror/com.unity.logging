using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging
{
    public interface IFormatter
    {
        public bool IsCreated { get; }

        bool BeforeObject(ref UnsafeText output);
        bool AfterObject(ref UnsafeText output);

        bool BeginProperty(ref UnsafeText output, ref FixedString512Bytes fieldName);
        bool EndProperty(ref UnsafeText output, ref FixedString512Bytes fieldName);

        bool AppendDelimiter(ref UnsafeText output);
        unsafe bool WriteUTF8String(ref UnsafeText output, byte* ptr, int lengthBytes, ref ArgumentInfo currArgSlot);
        bool WriteChild<T>(ref UnsafeText output, in FixedString512Bytes fieldName, ref T field, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth) where T : unmanaged, IWriterFormattedOutput;
        bool WriteProperty<T>(ref UnsafeText output, in FixedString512Bytes fieldName, in T fs, ref ArgumentInfo currArgSlot) where T : unmanaged, INativeList<byte>, IUTF8Bytes;
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, PayloadHandle payload, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot);

        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, char c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, bool b, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, sbyte c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, byte c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, short c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, ushort c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, int c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, uint c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, long c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, ulong c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, IntPtr p, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, UIntPtr p, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, float c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, double c, ref ArgumentInfo currArgSlot);
        bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, decimal c, ref ArgumentInfo currArgSlot);
    }
}
