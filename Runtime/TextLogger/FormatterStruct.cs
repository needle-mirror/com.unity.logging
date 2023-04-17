using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Logging.Internal;
using UnityEngine.Scripting;

namespace Unity.Logging
{
    /// <summary>
    /// Helper functions for Burst compatible string operations.
    /// </summary>
    public static class BurstStringWrapper
    {
        /// <summary>
        /// Appends string into UnsafeText. In Burst context <see cref="BurstStringWrapper.AppendString__Unmanaged"/> will be called instead
        /// </summary>
        /// <param name="output">UnsafeText append to</param>
        /// <param name="str">String that should be appended</param>
        /// <returns>FormatError from Append operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError AppendString(ref UnsafeText output, string str)
        {
            return output.Append(str);
        }

        /// <summary>
        /// Do not call directly! Use <see cref="BurstStringWrapper.AppendString"/> - burst will call this automatically if can
        /// </summary>
        /// <param name="output">UnsafeText append to</param>
        /// <param name="utf8Bytes">UTF8 string pointer</param>
        /// <param name="utf8Len">UTF8 string length</param>
        /// <returns>FormatError from Append operation</returns>
        [RequiredMember]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe FormatError AppendString__Unmanaged(ref UnsafeText output, byte* utf8Bytes, int utf8Len)
        {
            return output.Append(utf8Bytes, utf8Len);
        }


        /// <summary>
        /// Checks if the string is empty. In Burst context <see cref="BurstStringWrapper.AppendString__Unmanaged"/> will be called instead
        /// </summary>
        /// <param name="fieldName"></param>
        /// <returns>True if string is null or empty</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(string fieldName)
        {
            return string.IsNullOrEmpty(fieldName);
        }

        /// <summary>
        /// Do not call directly! Use <see cref="BurstStringWrapper.IsEmpty"/> - burst will call this automatically if can
        /// </summary>
        /// <param name="utf8Bytes">UTF8 string pointer</param>
        /// <param name="utf8Len">UTF8 string length</param>
        /// <returns>True if string is null or empty</returns>
        [RequiredMember]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool IsEmpty__Unmanaged(byte* utf8Bytes, int utf8Len)
        {
            return utf8Bytes == null || utf8Len <= 0;
        }
    }

    /// <inheritdoc cref="IFormatter"/>
    public struct FormatterStruct : IFormatter
    {
        /// <summary>
        /// Converts a <see cref="LogMessage"/> into <see cref="UnsafeText"/>
        /// </summary>
        public Unity.Logging.Sinks.OnLogMessageFormatterDelegate OnFormatMessage;

        /// <summary>
        /// It is a JSON formatter if 1, text if 0
        /// </summary>
        public byte UseTextBlittable;

        /// <summary>
        /// Checks if this is a JSON or text formatter
        /// </summary>
        /// <returns>
        /// Returns true if this is a text formatter, false if JSON
        /// </returns>
        public bool UseText
        {
            get => UseTextBlittable != 0;
            set => UseTextBlittable = (byte)((value) ? 1 : 0);
        }

        /// <summary>
        /// True if the structure was initialized
        /// </summary>
        public bool IsCreated => OnFormatMessage.IsCreated;

        /// <inheritdoc cref="IFormatter.AppendDelimiter"/>
        public bool AppendDelimiter(ref UnsafeText output)
        {
            return output.Append(',') == FormatError.None && output.Append(' ') == FormatError.None;
        }

        /// <inheritdoc cref="IFormatter.WriteChild{T}"/>
        public bool WriteChild<T>(ref UnsafeText output, string fieldName, ref T field, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth) where T : unmanaged, ILoggableMirrorStruct
        {
            if (depth > 8) return true;

            var res = true;
            if (UseText)
            {

            }
            else
            {
                res = output.Append('"') == FormatError.None &&
                          BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                          output.Append('"') == FormatError.None &&
                          output.Append(':') == FormatError.None;
            }

            return field.AppendToUnsafeText(ref output, ref this, ref memAllocator, ref currArgSlot, depth) && res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, char, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, char c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return output.Append('"') == FormatError.None &&
                   FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   output.Append('"') == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, bool, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, bool b, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, b, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }


            return FormatString.AppendLowcase(ref output, b, ref currArgSlot) == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, sbyte, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, sbyte c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, byte, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, byte c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, short, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, short c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, ushort, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, ushort c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, int, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, int c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, uint, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, uint c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, long, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, long c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, ulong, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, ulong c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, IntPtr, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, IntPtr p, ref ArgumentInfo currArgSlot)
        {
            return WriteProperty(ref output, fieldName, p.ToInt64(), ref currArgSlot);
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, UIntPtr, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, UIntPtr p, ref ArgumentInfo currArgSlot)
        {
            return WriteProperty(ref output, fieldName, p.ToUInt64(), ref currArgSlot);
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, double, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, double c, ref ArgumentInfo currArgSlot)
        {
            return WriteProperty(ref output, fieldName, (float)c, ref currArgSlot);
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, decimal, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, decimal c, ref ArgumentInfo currArgSlot)
        {
            return WriteProperty(ref output, fieldName, (float)c, ref currArgSlot);
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, float, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, float c, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return FormatString.Append(ref output, c, ref currArgSlot) == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty(ref UnsafeText, string, PayloadHandle, ref LogMemoryManager, ref ArgumentInfo)"/>
        public bool WriteProperty(ref UnsafeText output, string fieldName, PayloadHandle payload, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
            {
                return output.Append('"') == FormatError.None &&
                       Unity.Logging.Builder.AppendStringAsPayloadHandle(ref output, payload, ref memAllocator) &&
                       output.Append('"') == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return output.Append('"') == FormatError.None &&
                   Unity.Logging.Builder.AppendStringAsPayloadHandle(ref output, payload, ref memAllocator) &&
                   output.Append('"') == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteProperty{T}(ref UnsafeText, string, in T, ref ArgumentInfo)"/>
        public bool WriteProperty<T>(ref UnsafeText output, string fieldName, in T fs, ref ArgumentInfo currArgSlot) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            if (UseText)
            {
                return output.Append(in fs) == FormatError.None;
            }

            var res = true;

            if (BurstStringWrapper.IsEmpty(fieldName) == false)
            {
                res = output.Append('"') == FormatError.None &&
                      BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                      output.Append('"') == FormatError.None &&
                      output.Append(':') == FormatError.None;
            }

            return output.Append('"') == FormatError.None &&
                   FormatString.Append(ref output, fs, ref currArgSlot) == FormatError.None &&
                   output.Append('"') == FormatError.None &&
                   res;
        }

        /// <inheritdoc cref="IFormatter.WriteUTF8String"/>
        public unsafe bool WriteUTF8String(ref UnsafeText output, byte* ptr, int lengthBytes, ref ArgumentInfo currArgSlot)
        {
            if (UseText)
                return output.Append(ptr, lengthBytes) == FormatError.None;

            return output.Append('"') == FormatError.None &&
                   output.Append(ptr, lengthBytes) == FormatError.None &&
                   output.Append('"') == FormatError.None;
        }

        /// <inheritdoc cref="IFormatter.BeforeObject"/>
        public bool BeforeObject(ref UnsafeText output)
        {
            if (UseText)
            {
                return output.Append('[') == FormatError.None;
            }
            return output.Append('{') == FormatError.None;
        }

        /// <inheritdoc cref="IFormatter.AfterObject"/>
        public bool AfterObject(ref UnsafeText output)
        {
            if (UseText)
            {
                return output.Append(']') == FormatError.None;
            }
            return output.Append('}') == FormatError.None;
        }

        /// <inheritdoc cref="IFormatter.BeginProperty"/>
        public bool BeginProperty(ref UnsafeText output, string fieldName)
        {
            if (UseText)
            {
                return true;
            }
            else
            {
                return output.Append('"') == FormatError.None &&
                       BurstStringWrapper.AppendString(ref output, fieldName) == FormatError.None &&
                       output.Append('"') == FormatError.None &&
                       output.Append(':') == FormatError.None;
            }
        }

        /// <inheritdoc cref="IFormatter.BeginProperty{T}"/>
        public bool BeginProperty<T>(ref UnsafeText output, ref T fieldName) where T : unmanaged, IUTF8Bytes, INativeList<byte>
        {
            if (UseText)
            {
                return true;
            }
            else
            {
                unsafe
                {
                    var ptr = fieldName.GetUnsafePtr();
                    var len = fieldName.Length;

                    return output.Append('"') == FormatError.None &&
                           output.Append(ptr, len) == FormatError.None &&
                           output.Append('"') == FormatError.None &&
                           output.Append(':') == FormatError.None;
                }
            }
        }

        /// <inheritdoc cref="IFormatter.EndProperty"/>
        public bool EndProperty(ref UnsafeText output, string fieldName)
        {
            return true;
        }

        /// <inheritdoc cref="IFormatter.EndProperty{T}"/>
        public bool EndProperty<T>(ref UnsafeText output, ref T fieldName) where T : unmanaged, IUTF8Bytes, INativeList<byte>
        {
            return true;
        }
    }
}
