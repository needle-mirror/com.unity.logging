using System;
using Unity.Collections;
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
            /// <summary>
            /// Not specified. Default
            /// </summary>
            Default = 0,
            /// <summary>
            /// Represents the @ destructure operator
            /// </summary>
            Destructure,
            /// <summary>
            /// Represents the $ stringify operator
            /// </summary>
            Stringify
        }

        /// <summary>
        /// Type of the hole
        /// </summary>
        public enum HoleType : byte
        {
            /// <summary>
            /// User named this one - not a built-in hole name
            /// </summary>
            UserDefined,

            /// <summary>
            /// {Timestamp}
            /// </summary>
            BuiltinTimestamp,

            /// <summary>
            /// {Level}
            /// </summary>
            BuiltinLevel,

            /// <summary>
            /// {Stacktrace}
            /// </summary>
            BuiltinStacktrace,

            /// <summary>
            /// {Message} used only in templates
            /// </summary>
            BuiltinMessage,

            /// <summary>
            /// {NewLine}
            /// </summary>
            BuiltinNewLine,

            /// <summary>
            /// {Properties}. Reserved
            /// </summary>
            BuiltinProperties
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
        /// Non-Zero if created
        /// </summary>
        public readonly byte IsValidByte;

        /// <summary>
        /// True if created
        /// </summary>
        public bool IsValid => IsValidByte != 0;

        /// <summary>
        /// <see cref="HoleType"/> of this hole.
        /// </summary>
        public readonly HoleType Type;

        /// <summary>
        /// True if the <see cref="HoleType"/> is not built-in, but user defined.
        /// </summary>
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

        /// <summary>
        /// Constructor for {0}, {3}, {42} type of holes
        /// </summary>
        /// <param name="index">Integer in the hole</param>
        private ArgumentInfo(int index)
        {
            Index = index;
            IsValidByte = 1;

            Destructing = DestructingType.Default;
            Name = default;
            Alignment = 0;
            Format = default;

            Type = HoleType.UserDefined;
        }

        /// <summary>
        /// Constructor of the <see cref="ArgumentInfo"/>
        /// </summary>
        /// <param name="index">Integer in the hole, like {0} or {42}, if not set see Name</param>
        /// <param name="name">Name of the hole, like {thisIsName} or default if Index is used instead</param>
        /// <param name="destructingType">@, $ or default</param>
        /// <param name="format">Format - custom string after :</param>
        /// <param name="alignment">Integer after comma, before :</param>
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

        /// <summary>
        /// Parses <see cref="ArgumentInfo"/> from UTF8 string's segment
        /// </summary>
        /// <param name="rawMsgBuffer">Pointer to UTF8 string</param>
        /// <param name="rawMsgBufferLength">Full length of the UTF8 string</param>
        /// <param name="currArgSegment">Segment to parse inside the UTF8 string</param>
        /// <returns>Parsed ArgumentInfo or default if error occured</returns>
        public static unsafe ArgumentInfo ParseArgument(byte* rawMsgBuffer, int rawMsgBufferLength, in ParseSegment currArgSegment)
        {
            if (rawMsgBufferLength == 0 || currArgSegment.Length <= 2 || currArgSegment.OffsetEnd > rawMsgBufferLength)
                return default;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.AreEqual((byte)'{', rawMsgBuffer[currArgSegment.Offset]);
            Assert.AreEqual((byte)'}', rawMsgBuffer[currArgSegment.Offset + currArgSegment.Length - 1]);
#endif

            if (currArgSegment.Length == 3)
            {
                // fast track for {0}, {1}, ... {9}
                var c = rawMsgBuffer[currArgSegment.Offset + 1];
                if (c >= '0' && c <= '9')
                {
                    return Number(c - '0');
                }
            }

            var rawString = new FixedString512Bytes();
            rawString.Append(&rawMsgBuffer[currArgSegment.Offset], currArgSegment.Length);

            var bodySegment = ParseSegment.Reduce(currArgSegment, 1, 1);

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
                var rawStringLocalAlignmentSegment = ParseSegment.Local(currArgSegment, alignmentSegment);

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

            var rawStringLocalNameSegment = ParseSegment.Local(currArgSegment, bodySegment);

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

        /// <summary>
        /// Returns an index of the context argument to use in WriteMessage or builtin code
        /// </summary>
        /// <param name="rawBuffer">Pointer to argument UTF8 string</param>
        /// <param name="argSlotSegment">Segment inside the UTF8 string</param>
        /// <param name="isThisTemplate">True if this is a template (so {Message} is valid)</param>
        /// <returns>Index of the context argument to use in WriteMessage or builtin code</returns>
        public static unsafe int RetrieveContextArgumentIndex(in byte* rawBuffer, in ParseSegment argSlotSegment, bool isThisTemplate)
        {
            var arg = ArgumentInfo.ParseArgument(rawBuffer, argSlotSegment.OffsetEnd, argSlotSegment);

            return RetrieveContextArgumentIndex(arg, isThisTemplate);
        }

        /// <summary>
        /// Builtin code for the timestamp context argument
        /// </summary>
        public const int BuiltInTimestampId = -100;

        /// <summary>
        /// Builtin code for the level context argument
        /// </summary>
        public const int BuiltInLevelId = -101;

        /// <summary>
        /// Builtin code for the message context argument
        /// </summary>
        public const int BuiltInMessage = -102;

        /// <summary>
        /// Builtin code for the stacktrace context argument
        /// </summary>
        public const int BuiltInStackTrace = -103;

        /// <summary>
        /// Builtin code for the newline context argument
        /// </summary>
        public const int BuiltInNewLine = -104;

        /// <summary>
        /// Builtin code for the properties context argument
        /// </summary>
        public const int BuiltInProperties = -105;
    }
}
