using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Logging.Internal;

namespace Unity.Logging
{
    /// <summary>
    /// Burst-friendly way to represent a formatter
    /// </summary>
    public struct FormatterStruct : IFormatter
    {
        public Unity.Logging.Sinks.OnLogMessageFormatterDelegate OnFormatMessage;

        public byte UseTextBlittable;

        public bool UseText
        {
            get => UseTextBlittable != 0;
            set => UseTextBlittable = (byte)((value) ? 1 : 0);
        }
        public bool IsCreated => OnFormatMessage.IsCreated;

        public bool AppendDelimiter(ref UnsafeText output)
        {
            return output.Append(',') == FormatError.None && output.Append(' ') == FormatError.None;
        }

        public bool WriteChild<T>(ref UnsafeText output, in FixedString512Bytes fieldName, ref T field, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth) where T : unmanaged, IWriterFormattedOutput
        {
            var res = true;
            if (UseText)
            {

            }
            else
            {
                res = output.Append('"') == FormatError.None &&
                          output.Append(fieldName) == FormatError.None &&
                          output.Append('"') == FormatError.None &&
                          output.Append(':') == FormatError.None;
            }

            return field.WriteFormattedOutput(ref output, ref this, ref memAllocator, ref currArgSlot, depth) && res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, char c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return output.Append('"') == FormatError.None &&
                   FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   output.Append('"') == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, bool b, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, b, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }


            return FormatString.AppendLowcase(ref output, b, ref currArgSlot) == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, sbyte c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, byte c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, short c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, ushort c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, int c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, uint c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, long c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, ulong c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, IntPtr p, ref ArgumentInfo currArgSlot)
        {
            return WriteProperty(ref output, in fieldName, p.ToInt64(), ref currArgSlot);
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, UIntPtr p, ref ArgumentInfo currArgSlot)
        {
            return WriteProperty(ref output, in fieldName, p.ToUInt64(), ref currArgSlot);
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, double c, ref ArgumentInfo currArgSlot)
        {
            return WriteProperty(ref output, in fieldName, (float)c, ref currArgSlot);
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, decimal c, ref ArgumentInfo currArgSlot)
        {
            return WriteProperty(ref output, in fieldName, (float)c, ref currArgSlot);
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, float c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        public bool WriteProperty(ref UnsafeText output, in FixedString512Bytes fieldName, PayloadHandle payload, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return output.Append('"') == FormatError.None &&
                       Unity.Logging.Builder.AppendStringAsPayloadHandle(ref output, payload, ref memAllocator) &&
                       output.Append('"') == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return output.Append('"') == FormatError.None &&
                   Unity.Logging.Builder.AppendStringAsPayloadHandle(ref output, payload, ref memAllocator) &&
                   output.Append('"') == FormatError.None &&
                   res;
        }

        public bool WriteProperty<T>(ref UnsafeText output, in FixedString512Bytes fieldName, in T fs, ref ArgumentInfo currArgSlot) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            if (UseText)
            {
                return output.Append(in fs) == FormatError.None;
            }

            var res = true;

            if (fieldName.IsEmpty == false)
            {
                res = output.Append('"') == FormatError.None &&
                      output.Append(fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return output.Append('"') == FormatError.None &&
                   FormatString.Append(ref output, fs, ref currArgSlot) == FormatError.None &&
                   output.Append('"') == FormatError.None &&
                   res;
        }

        public unsafe bool WriteUTF8String(ref UnsafeText output, byte* ptr, int lengthBytes, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
                return output.Append(ptr, lengthBytes) == FormatError.None;

            return output.Append('"') == FormatError.None &&
                   output.Append(ptr, lengthBytes) == FormatError.None &&
                   output.Append('"') == FormatError.None;
        }

        public bool BeforeObject(ref UnsafeText output)
        {
            if (UseText)
            {
                return output.Append('[') == FormatError.None;
            }
            return output.Append('{') == FormatError.None;
        }

        public bool AfterObject(ref UnsafeText output)
        {
            if (UseText)
            {
                return output.Append(']') == FormatError.None;
            }
            return output.Append('}') == FormatError.None;
        }

        public bool BeginProperty(ref UnsafeText output, ref FixedString512Bytes fieldName)
        {
            if (UseText)
            {
                return true;
            }
            else
            {
                return output.Append('"') == FormatError.None &&
                       output.Append(fieldName) == FormatError.None &&
                       output.Append('"') == FormatError.None &&
                       output.Append(':') == FormatError.None;
            }
        }

        public bool EndProperty(ref UnsafeText output, ref FixedString512Bytes fieldName)
        {
            return true;
        }
    }
}
