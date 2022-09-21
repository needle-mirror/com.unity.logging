using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MainLoggingGenerator.Extractors
{
    public class MessageParserAnalysis
    {
        public readonly bool Success;
        public readonly ParseResult ParseRes;

        public List<ArgumentSegment> Arguments => ParseRes.ArgsSegments;
        public bool HasNamedArgument => Arguments.Any(a => string.IsNullOrEmpty(a.argumentInfo.Name) == false);

        public MessageParserAnalysis(string messageLiteral)
        {
            ParseRes = DoLoggingLikeParsing(messageLiteral);
            Success = ParseRes.Success;
        }

        public class ParseResult
        {
            public bool Success = true;
            public readonly List<ArgumentSegment> ArgsSegments = new();
            public readonly List<Segments> Segments = new ();
            public readonly List<Segments> Errors = new ();

            public FormatError Append(string messageLiteral, int offset, int length)
            {
                Segments.Add(new StringSegment(messageLiteral.Substring(offset, length), new ParseSegment { Offset = offset, Length = length}));
                return FormatError.None;
            }

            public FormatError Append(char c, int offset)
            {
                Segments.Add(new StringSegment(c+"", new ParseSegment { Offset = offset, Length = 1}));
                return FormatError.None;
            }

            public void AppendStackTrace(ParseSegment seg)
            {
                Segments.Add(new StackTraceSegment(seg));
            }

            public void AppendLevel(ParseSegment seg)
            {
                Segments.Add(new LevelSegment(seg));
            }

            public void AppendTimestamp(ParseSegment seg)
            {
                Segments.Add(new TimestampSegment(seg));
            }

            public void AppendArgument(int contextIndex, ArgumentInfo argumentInfo, ParseSegment seg)
            {
                var a = new ArgumentSegment(contextIndex, argumentInfo, seg);
                ArgsSegments.Add(a);
                Segments.Add(a);
            }

            public void ErrorInvalidArgument(ParseSegment seg)
            {
                Errors.Add(new ErrorInvalidArgument(seg));
                Success = false;
            }

            public void AppendNewLine(ParseSegment seg)
            {
                Segments.Add(new NewLineSegment(seg));
            }

            public void AppendMessage(ParseSegment seg)
            {
                Segments.Add(new MessageSegment(seg));
            }

            public void AppendProperties(ParseSegment seg)
            {
                Segments.Add(new PropertiesSegment(seg));
            }
        }

        // FOLLOWING CODE MUST BE IN-SYNC WITH LOGGING IMPLEMENTATION

        private ParseResult DoLoggingLikeParsing(string messageLiteral, bool isTemplate = false)
        {
            ParseResult res = new ParseResult();

            var rawMsgBuffer = messageLiteral;
            var rawMsgBufferLength = messageLiteral.Length;

            var currMsgSegment = new ParseSegment();
            var argIndexInString = -1;
            var done = false;
            var success = true;
            do
            {
                var result = FindNextParseStringSegment(messageLiteral, ref currMsgSegment, out var currArgSlot);

                success = res.Append(messageLiteral, currMsgSegment.Offset, currMsgSegment.Length) == FormatError.None && success;

                switch (result)
                {
                    case ParseContextResult.EscOpenBrace:
                    {
                        success = res.Append('{', currMsgSegment.Offset) == FormatError.None && success;
                        break;
                    }
                    case ParseContextResult.EscCloseBrace:
                    {
                        success = res.Append('}', currMsgSegment.Offset) == FormatError.None && success;
                        break;
                    }
                    case ParseContextResult.NormalArg:
                    {
                        var arg = ParseArgument(rawMsgBuffer, currArgSlot.OffsetEnd, currArgSlot, isTemplate);

                        if (arg.IsValid)
                            ++argIndexInString;

                        if (arg.IsBuiltInLevel)
                        {
                            res.AppendLevel(currArgSlot);
                        }
                        else if (arg.IsBuiltInTimestamp)
                        {
                            res.AppendTimestamp(currArgSlot);
                        }
                        else if (arg.IsBuiltInStackTrace)
                        {
                            res.AppendStackTrace(currArgSlot);
                        }
                        else if (arg.IsBuiltInNewLine)
                        {
                            res.AppendNewLine(currArgSlot);
                        }
                        else if (arg.IsBuiltInMessage)
                        {
                            res.AppendMessage(currArgSlot);
                        }
                        else if (arg.IsBuiltInProperties)
                        {
                            res.AppendProperties(currArgSlot);
                        }
                        else if (arg.IsBuiltIn == false)
                        {
                            var contextIndex = arg.Index;
                            if (arg.Name != "")
                            {
                                contextIndex = argIndexInString;
                            }

                            res.AppendArgument(contextIndex, arg, currArgSlot);
                        }
                        else
                        {
                            res.ErrorInvalidArgument(currArgSlot);
                            success = false;
                        }

                        break;
                    }
                    case ParseContextResult.NoArgs:
                        done = true;

                        break;
                }

                currMsgSegment.Offset = currArgSlot.Offset + currArgSlot.Length;
                if (currMsgSegment.Offset >= rawMsgBufferLength)
                    done = true;
            }
            while (!done);

            return res;
        }

        internal static ArgumentInfo ParseArgument(string rawMsgBuffer, int length, in ParseSegment currArgSlot, bool isTemplate)
        {
            int rawMsgBufferLength = length;

            if (rawMsgBufferLength == 0 || currArgSlot.Length <= 2 || currArgSlot.OffsetEnd > rawMsgBufferLength)
                return default;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.AreEqual('{', rawMsgBuffer[currArgSlot.Offset]);
        Assert.AreEqual('}', rawMsgBuffer[currArgSlot.Offset + currArgSlot.Length - 1]);
#endif

            if (currArgSlot.Length == 3)
            {
                // fast track for {0}, {1}, ... {9}
                var c = rawMsgBuffer[currArgSlot.Offset + 1];
                if (c >= '0' && c <= '9')
                {
                    return ArgumentInfo.Number(c - '0');
                }
            }

            var rawString = rawMsgBuffer.Substring(currArgSlot.Offset, currArgSlot.Length);

            var bodySegment = ParseSegment.Reduce(currArgSlot, 1, 1);

            ArgumentInfo.DestructingType destructingType = ArgumentInfo.DestructingType.Default;

            var firstCharInName = rawMsgBuffer[bodySegment.Offset];
            if (firstCharInName == '@' || firstCharInName == '$')
            {
                destructingType = firstCharInName == '@' ? ArgumentInfo.DestructingType.Destructure : ArgumentInfo.DestructingType.Stringify;
                bodySegment = ParseSegment.Reduce(bodySegment, 1, 0);
            }

            if (bodySegment.Length <= 0)
                return default; // no name

            var bodySegmentEnd = bodySegment.OffsetEnd;
            var bodySegmentNewEnd = bodySegmentEnd;

            var formatSegment = new ParseSegment { Length = -1, Offset = -1 };
            var alignmentSegment = new ParseSegment { Length = -1, Offset = -1 };

            for (var i = bodySegment.Offset; i < bodySegmentEnd;)
            {
                var prevI = i;
                Unicode_Utf8ToUcs(out var rune, rawMsgBuffer, ref i, rawMsgBufferLength);

                if (rune == ':')
                {
                    if (bodySegmentNewEnd > prevI)
                        bodySegmentNewEnd = prevI;

                    if (alignmentSegment.Offset != -1) // we detected alignmentSegment before, finish it here
                        alignmentSegment.Length = prevI - alignmentSegment.Offset;

                    // everything to the right is a 'format'
                    formatSegment = ParseSegment.RightPart(bodySegment, i);
                    break;
                }

                if (rune == ',')
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

            string format = "";
            if (formatSegment.IsValid)
            {
                format += rawMsgBuffer.Substring(formatSegment.Offset, formatSegment.Length);

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

                if (ParseInt(rawString, ref alignmentSegmentParseOffset, ref alignment))
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

            string name = "";
            var index = 0;

            var nameSegmentParseOffset = rawStringLocalNameSegment.Offset;
            if (ParseInt(rawString, ref nameSegmentParseOffset, ref index) && nameSegmentParseOffset == rawStringLocalNameSegment.OffsetEnd)
            {
                // name is just an integer

                if (index == 0 && rawStringLocalNameSegment.Length > 1) // -0
                    return default;

                if (index < 0)
                    return default;
            }
            else
            {
                name = rawMsgBuffer.Substring(bodySegment.Offset, bodySegment.Length);

                foreach (var rune in name)
                {
                    var valid = rune != '.' && rune != '{' && rune != '}' && rune != '-' && rune != '@' && rune != '$' && rune != '&' && rune != ' ';
                    //var valid = rune.value == '_' || char.IsLetterOrDigit((char)rune.value); //-- not burst compatible

                    if (valid == false)
                        return default;
                }

                index = -1;
            }

            return new ArgumentInfo(index, name, destructingType, format, alignment, isTemplate);
        }

        private static bool ParseInt(string fs, ref int offset, ref int value)
        {
            int resetOffset = offset;
            int sign = 1;
            if (offset < fs.Length)
            {
                if (fs[offset] == '+')
                    ++offset;
                else if (fs[offset] == '-')
                {
                    sign = -1;
                    ++offset;
                }
            }

            int digitOffset = offset;
            value = 0;
            while (offset < fs.Length && char.IsDigit(fs[offset]))
            {
                value *= 10;
                value += fs[offset] - '0';
                ++offset;
            }
            value = sign * value;

            // If there was no number parsed, revert the offset since it's a syntax error and we might
            // have erroneously parsed a '-' or '+'
            if (offset == digitOffset)
            {
                offset = resetOffset;
                return false;
            }

            return true;
        }

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
            public readonly string Name; //  [0-9A-z_]+

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
            public readonly string Format; // [^\{]+

            /// <summary>
            /// True if created
            /// </summary>
            public readonly byte IsValidByte;
            public bool IsValid => IsValidByte != 0;

            public readonly HoleType Type;

            public bool IsBuiltIn => Type != HoleType.UserDefined;
            public bool IsBuiltInTimestamp => Type == HoleType.BuiltinTimestamp;
            public bool IsBuiltInLevel => Type == HoleType.BuiltinLevel;
            public bool IsBuiltInMessage => Type == HoleType.BuiltinMessage;
            public bool IsBuiltInStackTrace => Type == HoleType.BuiltinStacktrace;
            public bool IsBuiltInNewLine => Type == HoleType.BuiltinNewLine;
            public bool IsBuiltInProperties => Type == HoleType.BuiltinProperties;

            /// <summary>
            /// Create a hole from a number (index)
            /// </summary>
            /// <param name="i">index to use</param>
            /// <returns>ArgumentInfo with Index == i</returns>
            public static ArgumentInfo Number(int i)
            {
                return new ArgumentInfo(i);
            }

            public override string ToString()
            {
                if (IsValid == false)
                    return $"[Not Valid]";

                if (IsBuiltIn)
                {
                    return Type.ToString();
                }

                if (string.IsNullOrEmpty(Name) == false)
                    return $"{{{Name}}}";
                return $"{{{Index}}}";
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

            public ArgumentInfo(int index, string name, DestructingType destructingType, string format, int alignment, bool isTemplate)
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
                    else if (isTemplate && Name == "Message") Type = HoleType.BuiltinMessage;
                    else if (Name == "NewLine") Type = HoleType.BuiltinNewLine;
                    else if (Name == "Timestamp") Type = HoleType.BuiltinTimestamp;
                    else if (Name == "Properties") Type = HoleType.BuiltinProperties;
                    else if (Name == "Stacktrace") Type = HoleType.BuiltinStacktrace;
                    // if you add new builtin properties - make sure the 'if' is correct
                }

                IsValidByte = 1;
            }
        }

        internal enum ParseContextResult { NoArgs, NormalArg, EscOpenBrace, EscCloseBrace };

        public struct ParseSegment
        {
            public int Offset;
            public int Length;

            public bool IsValid => Offset >= 0 && Length >= 0;
            public int OffsetEnd => Offset + Length;

            public static ParseSegment Reduce(in ParseSegment origin, int BytesFromLeft, int BytesFromRight)
            {
                //Assert.IsTrue(origin.IsValid);
                return new ParseSegment
                {
                    Offset = origin.Offset + BytesFromLeft,
                    Length = origin.Length - BytesFromLeft - BytesFromRight
                };
            }

            public static ParseSegment LeftPart(in ParseSegment origin, int splitPoint)
            {
                // Assert.IsTrue(origin.IsValid);
                // Assert.IsTrue(splitPoint >= origin.Offset);
                // Assert.IsTrue(splitPoint < origin.Offset + origin.Length);
                return new ParseSegment
                {
                    Offset = origin.Offset,
                    Length = splitPoint - origin.Offset
                };
            }

            public static ParseSegment RightPart(in ParseSegment origin, int splitPoint)
            {
// #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
//             Assert.IsTrue(origin.IsValid);
//             Assert.IsTrue(splitPoint >= origin.Offset);
//             Assert.IsTrue(splitPoint < origin.Offset + origin.Length);
// #endif
                return new ParseSegment
                {
                    Offset = splitPoint,
                    Length = origin.Length - (splitPoint - origin.Offset)
                };
            }

            public static ParseSegment Local(in ParseSegment parent, in ParseSegment convertThis)
            {
// #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
//             Assert.IsTrue(parent.IsValid);
//             Assert.IsTrue(convertThis.IsValid);
// #endif
                return new ParseSegment
                {
                    Offset = convertThis.Offset - parent.Offset,
                    Length = convertThis.Length
                };
            }
        }

        internal static ParseContextResult FindNextParseStringSegment(string str, ref ParseSegment currMsgSegment, out ParseSegment argSlot)
        {
            var rawBuffer = str;
            var rawBufferLength = str.Length;

            int endMsgOffset = rawBufferLength;
            int argOpenOffset = -1;
            int argCloseOffset = -1;
            bool validSlot = false;
            bool escapeOpenBrace = false;
            bool escapeCloseBrace = false;

            int currWorkingIndex = currMsgSegment.Offset;
            int readIndex = currWorkingIndex;

            while (readIndex < endMsgOffset)
            {
                currWorkingIndex = readIndex;

                // Iterate over each UTF-8 char until we find the start of a format argument slot '{' or reach the end
                // of the message segment (meaning has no format slots)
                if (Unicode_Utf8ToUcs(out var currChar, rawBuffer, ref readIndex, rawBufferLength) != ConversionError.None)
                    continue;

                // End of message string
                if (readIndex >= endMsgOffset)
                    break;

                // "Peek" at next char but don't advance our readIndex
                int tempPos = readIndex;
                if (Unicode_Utf8ToUcs(out var nextChar, rawBuffer, ref tempPos, rawBufferLength) != ConversionError.None)
                    continue;

                // Check for escaped braces ("{{" and "}}") which we'll treat as special case format slots were the
                // double brace is replaced by an argument string holding a single brace char
                if (currChar == '{' && nextChar == '{')
                {
                    argOpenOffset = currWorkingIndex;
                    argCloseOffset = currWorkingIndex + 1;
                    escapeOpenBrace = true;
                }
                else if (currChar == '}' && nextChar == '}')
                {
                    argOpenOffset = currWorkingIndex;
                    argCloseOffset = currWorkingIndex + 1;
                    escapeCloseBrace = true;
                }
                else if (currChar == '{' && tempPos < endMsgOffset)
                {
                    // Found a valid Open to a parameter slot, make sure it also has a valid Close
                    argOpenOffset = currWorkingIndex;
                    readIndex = tempPos;

                    while (readIndex < endMsgOffset)
                    {
                        currWorkingIndex = readIndex;

                        if (Unicode_Utf8ToUcs(out currChar, rawBuffer, ref readIndex, rawBufferLength) != ConversionError.None)
                            continue;

                        // Find the first closing brace
                        // NOTE: Won't check for escaping '}' because of this situation: "{{{0}}}" which we expect to output: "{hello}"
                        // If we check for escaped brace we'll end up with "{0}}": first pair of closing braces produces a '}' which invalidates format expansion
                        if (currChar == '}')
                        {
                            argCloseOffset = currWorkingIndex;
                            break;
                        }
                    }
                }
                else if (tempPos >= endMsgOffset)
                {
                    // Even if found a valid '{' we're at the end of the message so won't be a valid close
                    break;
                }
                else continue;

                if (argOpenOffset >= 0 && argCloseOffset >= 0 && argCloseOffset > argOpenOffset && argOpenOffset >= currMsgSegment.Offset)
                {
                    validSlot = true;
                    break;
                }
            }

            // If a valid slot was found, update the parser's state to save the arg's position
            // Otherwise advance the read length to the end of the message string (no valid slots).
            ParseContextResult result;
            if (validSlot)
            {
                currMsgSegment.Length = argOpenOffset - currMsgSegment.Offset;

                argSlot = new ParseSegment
                {
                    Offset = argOpenOffset,
                    Length = argCloseOffset - argOpenOffset + 1,
                };

                if (escapeOpenBrace)
                {
                    result = ParseContextResult.EscOpenBrace;
                }
                else if (escapeCloseBrace)
                {
                    result = ParseContextResult.EscCloseBrace;
                }
                else
                {
                    result = ParseContextResult.NormalArg;
                }
            }
            else
            {
                argSlot = new ParseSegment();
                result = ParseContextResult.NoArgs;

                // Disregard any NULs at the end of the buffer; any 0 values at the end should just be padding
                int remainingLength = endMsgOffset - currMsgSegment.Offset;
                if (remainingLength > 0)
                {
                    while (remainingLength > 0 && rawBuffer[currMsgSegment.Offset + remainingLength - 1] == 0)
                        remainingLength--;
                }
                currMsgSegment.Length = remainingLength;
            }

            return result;
        }

        /// <summary>
        /// Kinds of conversion errors.
        /// </summary>
        public enum ConversionError
        {
            /// <summary>
            /// No conversion error.
            /// </summary>
            None,

            /// <summary>
            /// The target storage does not have sufficient capacity.
            /// </summary>
            Overflow,

            /// <summary>
            /// The bytes do not form a valid character.
            /// </summary>
            Encoding,

            /// <summary>
            /// The rune is not a valid code point.
            /// </summary>
            CodePoint,
        }

        private static ConversionError Unicode_Utf8ToUcs(out char o, string rawBuffer, ref int readIndex, int rawBufferLength)
        {
            o = '\0';

            if (readIndex < 0) return ConversionError.Overflow;
            if (readIndex >= rawBufferLength) return ConversionError.Overflow;

            o = rawBuffer[readIndex];
            ++readIndex;

            return ConversionError.None;
        }
    }

    internal class ErrorInvalidArgument : Segments
    {
        public ErrorInvalidArgument(MessageParserAnalysis.ParseSegment segment) : base(segment)
        {
        }

        public override string ToString()
        {
            return $"Invalid argument at {segment.Offset}";
        }
    }

    public class ArgumentSegment : Segments
    {
        public readonly int contextIndex;
        public readonly MessageParserAnalysis.ArgumentInfo argumentInfo;
        public ArgumentSegment(int contextIndex, MessageParserAnalysis.ArgumentInfo argumentInfo, MessageParserAnalysis.ParseSegment segment) : base(segment)
        {
            this.contextIndex = contextIndex;
            this.argumentInfo = argumentInfo;
        }

        public override string ToString()
        {
            return $"[Arg {argumentInfo}]";
        }
    }

    internal class NewLineSegment : Segments
    {
        public override string ToString() => "[NewLine]";

        public NewLineSegment(MessageParserAnalysis.ParseSegment seg) : base(seg)
        {
        }
    }
    internal class PropertiesSegment : Segments
    {
        public override string ToString() => "[Properties]";

        public PropertiesSegment(MessageParserAnalysis.ParseSegment seg) : base(seg)
        {
        }
    }
    internal class MessageSegment : Segments
    {
        public override string ToString() => "[Message]";

        public MessageSegment(MessageParserAnalysis.ParseSegment seg) : base(seg)
        {
        }
    }
    internal class StackTraceSegment : Segments
    {
        public override string ToString() => "[StackTrace]";

        public StackTraceSegment(MessageParserAnalysis.ParseSegment seg) : base(seg)
        {
        }
    }

    internal class LevelSegment : Segments
    {
        public override string ToString() => "[Level]";

        public LevelSegment(MessageParserAnalysis.ParseSegment seg) : base(seg)
        {
        }
    }

    internal class TimestampSegment : Segments
    {
        public override string ToString() => "[Timestamp]";

        public TimestampSegment(MessageParserAnalysis.ParseSegment seg) : base(seg)
        {
        }
    }

    internal class StringSegment : Segments
    {
        private string data;
        public StringSegment(string substring, MessageParserAnalysis.ParseSegment seg) : base(seg)
        {
            data = substring;
        }

        public override string ToString()
        {
            return data;
        }
    }

    public class Segments
    {
        public readonly MessageParserAnalysis.ParseSegment segment;

        public Segments(MessageParserAnalysis.ParseSegment seg)
        {
            this.segment = seg;
        }
    }

    public enum FormatError
    {
        /// <summary>
        /// No error.
        /// </summary>
        None,

        /// <summary>
        /// The target storage does not have sufficient capacity.
        /// </summary>
        Overflow,
    }
}
