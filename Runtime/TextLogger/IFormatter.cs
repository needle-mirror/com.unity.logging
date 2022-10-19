using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging
{
    /// <summary>
    /// Interface that describes how to convert some object context into string
    /// </summary>
    public interface IFormatter
    {
        /// <summary>
        /// True if the formatter is initialized and valid
        /// </summary>
        public bool IsCreated { get; }

        /// <summary>
        /// Object begins. Should be called before <see cref="AfterObject"/>
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <returns>True if successful</returns>
        bool BeforeObject(ref UnsafeText output);

        /// <summary>
        /// Object ends. Should be called after <see cref="BeforeObject"/>
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <returns>True if successful</returns>
        bool AfterObject(ref UnsafeText output);

        /// <summary>
        /// Property starts. Should be called before <see cref="EndProperty"/>
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <returns>True if successful</returns>
        bool BeginProperty(ref UnsafeText output, string fieldName);

        /// <summary>
        /// Property ends. Should be called after <see cref="BeginProperty"/>
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <returns>True if successful</returns>
        bool EndProperty(ref UnsafeText output, string fieldName);

        /// <summary>
        /// Property starts. Should be called before <see cref="EndProperty"/>
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <typeparam name="T">Unmanaged UTF8 string</typeparam>
        /// <returns>True if successful</returns>
        bool BeginProperty<T>(ref UnsafeText output, ref T fieldName) where T : unmanaged, INativeList<byte>, IUTF8Bytes;

        /// <summary>
        /// Property ends. Should be called after <see cref="BeginProperty"/>
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <typeparam name="T">Unmanaged UTF8 string</typeparam>
        /// <returns>True if successful</returns>
        bool EndProperty<T>(ref UnsafeText output, ref T fieldName) where T : unmanaged, INativeList<byte>, IUTF8Bytes;

        /// <summary>
        /// Append a delimiter if any between properties / objects
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <returns>True if successful</returns>
        bool AppendDelimiter(ref UnsafeText output);

        /// <summary>
        /// Writes a UTF8 string
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="ptr">Pointer to UTF8 string</param>
        /// <param name="lengthBytes">Length of the UTF8 string in bytes</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        unsafe bool WriteUTF8String(ref UnsafeText output, byte* ptr, int lengthBytes, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes a child mirror struct as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="field">Child mirror struct to write</param>
        /// <param name="memAllocator"></param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <param name="depth"></param>
        /// <typeparam name="T">Child mirror struct - unmanaged, ILoggableMirrorStruct</typeparam>
        /// <returns>True if successful</returns>
        bool WriteChild<T>(ref UnsafeText output, string fieldName, ref T field, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth) where T : unmanaged, ILoggableMirrorStruct;

        /// <summary>
        /// Writes UTF8 string as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="fs">UTF8 string to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <typeparam name="T">Unmanaged UTF8 string</typeparam>
        /// <returns>True if successful</returns>
        bool WriteProperty<T>(ref UnsafeText output, string fieldName, in T fs, ref ArgumentInfo currArgSlot) where T : unmanaged, INativeList<byte>, IUTF8Bytes;

        /// <summary>
        /// Writes UTF8 string in <see cref="PayloadHandle"/> as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="payload">PayloadHandle that contains UTF8 string to write</param>
        /// <param name="memAllocator">Memory allocator that owns the payload handle</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, PayloadHandle payload, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes char as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">Char to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, char c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes bool as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="b">Bool to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, bool b, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes sbyte as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">SByte to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, sbyte c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes byte as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">Byte to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, byte c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes short as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">Short to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, short c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes ushort as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">UShort to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, ushort c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes int as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">Int to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, int c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes uint as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">UInt to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, uint c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes long as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">Long to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, long c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes ulong as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">ULong to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, ulong c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes IntPtr as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="p">IntPtr to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, IntPtr p, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes UIntPtr as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="p">UIntPtr to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, UIntPtr p, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes float as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">Primitive to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, float c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes double as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">Double to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, double c, ref ArgumentInfo currArgSlot);

        /// <summary>
        /// Writes decimal as a property
        /// </summary>
        /// <param name="output">UnsafeText where to append the text representation</param>
        /// <param name="fieldName">Name of the field that can be used by formatter</param>
        /// <param name="c">Decimal to write</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True if successful</returns>
        bool WriteProperty(ref UnsafeText output, string fieldName, decimal c, ref ArgumentInfo currArgSlot);
    }
}
