using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Logging
{
    /// <summary>
    /// Parsed Argument/Hole data. See https://messagetemplates.org/ for Holes
    /// </summary>
    public readonly struct ArgumentInfo
    {
        /// <summary>
        /// Enum for the name - with @, with $ or without
        /// </summary>
        public enum DestructingType : byte
        {
            Default = 0,
            Destructure, // @
            Stringify    // $
        }

        public enum HoleType : byte
        {
            UserDefined,
            BuiltinTimestamp,
            BuiltinLevel,
            BuiltinStacktrace,
            BuiltinMessage,

            BuiltinNewLine,   // reserved
            BuiltinProperties // reserved
        }

        /// <summary>
        /// See <see cref="DestructingType"/>
        /// </summary>
        public readonly DestructingType Destructing;

        /// <summary>
        /// Name of the hole
        /// </summary>
        public readonly FixedString512Bytes Name; //  [0-9A-z_]+

        /// <summary>
        /// Indexed hole (if no <see cref="Name"/> was specified)
        /// </summary>
        public readonly int Index; // [0-9]+

        /// <summary>
        /// Alignment that is specified after ','
        /// </summary>
        public readonly int Alignment; // '-'? [0-9]+

        /// <summary>
        /// Format that is specified after ':'
        /// </summary>
        public readonly FixedString512Bytes Format; // [^\{]+

        /// <summary>
        /// True if created
        /// </summary>
        public readonly byte IsValidByte;
        public bool IsValid => IsValidByte != 0;

        public readonly HoleType Type;

        public bool IsBuiltIn => Type != HoleType.UserDefined;

        /// <summary>
        /// Create a hole from a number (index)
        /// </summary>
        /// <param name="i">index to use</param>
        /// <returns>ArgumentInfo with Index == i</returns>
        public static ArgumentInfo Number(int i)
        {
            return new ArgumentInfo(i);
        }

        public ArgumentInfo(int index)
        {
            Index = index;
            IsValidByte = 1;

            Destructing = DestructingType.Default;
            Name = default;
            Alignment = 0;
            Format = default;

            Type = HoleType.UserDefined;
        }

        public ArgumentInfo(int index, FixedString512Bytes name, DestructingType destructingType, FixedString512Bytes format, int alignment)
        {
            Index = index;

            Destructing = destructingType;
            Name = name;
            Alignment = alignment;
            Format = format;

            Type = HoleType.UserDefined;

            var length = Name.Length;
            if (length >= 5 && length <= 10) // Level is 5, Stacktrace is 10
            {
                if (Name == "Level") Type = HoleType.BuiltinLevel;
                else if (Name == "Message") Type = HoleType.BuiltinMessage;
                else if (Name == "NewLine") Type = HoleType.BuiltinNewLine;
                else if (Name == "Timestamp") Type = HoleType.BuiltinTimestamp;
                else if (Name == "Properties") Type = HoleType.BuiltinProperties;
                else if (Name == "Stacktrace") Type = HoleType.BuiltinStacktrace;
                // if you add new builtin properties - make sure the 'if' is correct
            }

            IsValidByte = 1;
        }

        public static unsafe ArgumentInfo ParseArgument(byte* rawMsgBuffer, int rawMsgBufferLength, in ParseSegment currArgSlot)
        {
            if (rawMsgBufferLength == 0 || currArgSlot.Length <= 2 || currArgSlot.OffsetEnd > rawMsgBufferLength)
                return default;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.AreEqual((byte)'{', rawMsgBuffer[currArgSlot.Offset]);
            Assert.AreEqual((byte)'}', rawMsgBuffer[currArgSlot.Offset + currArgSlot.Length - 1]);
#endif

            if (currArgSlot.Length == 3)
            {
                // fast track for {0}, {1}, ... {9}
                var c = rawMsgBuffer[currArgSlot.Offset + 1];
                if (c >= '0' && c <= '9')
                {
                    return Number(c - '0');
                }
            }

            var rawString = new FixedString512Bytes();
            rawString.Append(&rawMsgBuffer[currArgSlot.Offset], currArgSlot.Length);

            var bodySegment = ParseSegment.Reduce(currArgSlot, 1, 1);

            DestructingType destructingType = DestructingType.Default;

            var firstCharInName = rawMsgBuffer[bodySegment.Offset];
            if (firstCharInName == '@' || firstCharInName == '$')
            {
                destructingType = firstCharInName == '@' ? DestructingType.Destructure : DestructingType.Stringify;
                bodySegment = ParseSegment.Reduce(bodySegment, 1, 0);
            }

            if (bodySegment.Length <= 0)
                return default; // no name

            var bodySegmentEnd = bodySegment.OffsetEnd;
            var bodySegmentNewEnd = bodySegmentEnd;

            var formatSegment = new ParseSegment {Offset = -1, Length = -1};
            var alignmentSegment = new ParseSegment {Offset = -1, Length = -1};

            for (var i = bodySegment.Offset; i < bodySegmentEnd;)
            {
                var prevI = i;
                var res = Unicode.Utf8ToUcs(out var rune, rawMsgBuffer, ref i, rawMsgBufferLength);
                Assert.IsTrue(res == ConversionError.None);

                if (rune.value == ':')
                {
                    if (bodySegmentNewEnd > prevI)
                        bodySegmentNewEnd = prevI;

                    if (alignmentSegment.Offset != -1) // we detected alignmentSegment before, finish it here
                        alignmentSegment.Length = prevI - alignmentSegment.Offset;

                    // everything to the right is a 'format'
                    formatSegment = ParseSegment.RightPart(bodySegment, i);
                    break;
                }

                if (rune.value == ',')
                {
                    if (bodySegmentNewEnd > prevI)
                        bodySegmentNewEnd = prevI;

                    alignmentSegment.Offset = i;
                }
            }

            // we detected alignmentSegment before and didn't finish, so finish it here
            if (alignmentSegment.Offset != -1 && alignmentSegment.Length == -1)
            {
                alignmentSegment.Length = bodySegmentEnd - alignmentSegment.Offset;
            }

            FixedString512Bytes format = "";
            if (formatSegment.IsValid)
            {
                format.Append(&rawMsgBuffer[formatSegment.Offset], formatSegment.Length);

                // not burst compatible
                // foreach (var c in format)
                // {
                //     var valid = c.value == ' ' || c.value == '+' || Unicode.Rune.IsDigit(c) || char.IsLetterOrDigit((char)c.value) || char.IsPunctuation((char)c.value);
                //     if (valid == false)
                //         return default;
                // }
            }

            var alignment = 0;
            if (alignmentSegment.IsValid)
            {
                var rawStringLocalAlignmentSegment = ParseSegment.Local(currArgSlot, alignmentSegment);

                var alignmentSegmentParseOffset = rawStringLocalAlignmentSegment.Offset;

                if (rawString.Parse(ref alignmentSegmentParseOffset, ref alignment) == ParseError.None)
                {
                    if (alignment == 0) // ',-0' or ',0' are illegal
                        return default;

                    if (alignmentSegmentParseOffset != rawStringLocalAlignmentSegment.OffsetEnd) // there is an int, but also something else
                        return default;
                }
                else
                    return default; // not an int
            }

            if (bodySegmentNewEnd < bodySegmentEnd)
                bodySegment = ParseSegment.LeftPart(bodySegment, bodySegmentNewEnd);

            var rawStringLocalNameSegment = ParseSegment.Local(currArgSlot, bodySegment);

            FixedString512Bytes name = default;
            var index = 0;

            var nameSegmentParseOffset = rawStringLocalNameSegment.Offset;
            if (rawString.Parse(ref nameSegmentParseOffset, ref index) == ParseError.None && nameSegmentParseOffset == rawStringLocalNameSegment.OffsetEnd)
            {
                // name is just an integer

                if (index == 0 && rawStringLocalNameSegment.Length > 1) // -0
                    return default;

                if (index < 0)
                    return default;
            }
            else
            {
                name.Append(&rawMsgBuffer[bodySegment.Offset], bodySegment.Length);

                foreach (var rune in name)
                {
                    // According to https://messagetemplates.org/ name [0-9a-zA-Z_]+
                    var valid = (rune.value >= '0' && rune.value <= '9') ||
                                (rune.value >= 'a' && rune.value <= 'z') ||
                                (rune.value >= 'A' && rune.value <= 'Z') ||
                                rune.value == '_';

                    if (valid == false)
                        return default;
                }

                index = -1;
            }

            return new ArgumentInfo(index, name, destructingType, format, alignment);
        }

        public static int RetrieveContextArgumentIndex(in NativeArray<byte> msgBuffer, in ParseSegment argSlotSegment, bool isThisTemplate)
        {
            unsafe
            {
                var rawBuffer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(msgBuffer);
                return RetrieveContextArgumentIndex(in rawBuffer, in argSlotSegment, isThisTemplate);
            }
        }

        private static int RetrieveContextArgumentIndex(in ArgumentInfo arg, bool isThisTemplate)
        {
            if (arg.IsValid == false)
                return -1;

            if (!isThisTemplate)
            {
                switch (arg.Type)
                {
                    case HoleType.BuiltinTimestamp:  return BuiltInTimestampId;
                    case HoleType.BuiltinLevel:      return BuiltInLevelId;
                    case HoleType.BuiltinStacktrace: return BuiltInStackTrace;
                    case HoleType.BuiltinNewLine:    return BuiltInNewLine;
                    case HoleType.BuiltinProperties: return BuiltInProperties;

                    case HoleType.BuiltinMessage:
                    case HoleType.UserDefined: return arg.Index;
                    default: return -1;
                }
            }

            switch (arg.Type)
            {
                case HoleType.BuiltinTimestamp:  return BuiltInTimestampId;
                case HoleType.BuiltinLevel:      return BuiltInLevelId;
                case HoleType.BuiltinStacktrace: return BuiltInStackTrace;
                case HoleType.BuiltinMessage:    return BuiltInMessage;
                case HoleType.BuiltinNewLine:    return BuiltInNewLine;
                case HoleType.BuiltinProperties: return BuiltInProperties;
                default:                         return -1;
            }
        }

        public static unsafe int RetrieveContextArgumentIndex(in byte* rawBuffer, in ParseSegment argSlotSegment, bool isThisTemplate)
        {
            var arg = ArgumentInfo.ParseArgument(rawBuffer, argSlotSegment.OffsetEnd, argSlotSegment);

            return RetrieveContextArgumentIndex(arg, isThisTemplate);
        }

        public const int BuiltInTimestampId = -100;
        public const int BuiltInLevelId = -101;
        public const int BuiltInMessage = -102;
        public const int BuiltInStackTrace = -103;
        public const int BuiltInNewLine = -104;
        public const int BuiltInProperties = -105;
    }
}
