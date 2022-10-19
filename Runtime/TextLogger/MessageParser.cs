using Unity.Collections;

namespace Unity.Logging
{
    /// <summary>
    /// Static class used to parse message part of the log message.
    /// </summary>
    public static class MessageParser
    {
        /// <summary>
        /// Parser state
        /// </summary>
        public enum ParseContextResult
        {
            /// <summary>
            /// No arguments were parsed
            /// </summary>
            NoArgs,
            /// <summary>
            /// Argument was parsed
            /// </summary>
            NormalArg,
            /// <summary>
            /// { symbol
            /// </summary>
            EscOpenBrace,
            /// <summary>
            /// } symbol
            /// </summary>
            EscCloseBrace
        };

        /// <summary>
        /// Parses the UTF8 string and returns the argument if it was found
        /// </summary>
        /// <param name="rawBuffer">UTF8 string pointer</param>
        /// <param name="rawBufferLength">UTF8 string length in bytes</param>
        /// <param name="currMsgSegment">Segment in the string to parse. Will be changed by the method</param>
        /// <param name="argSlot">Argument that was found</param>
        /// <returns>Result of the parsing</returns>
        public static unsafe ParseContextResult FindNextParseStringSegment(in byte* rawBuffer, in int rawBufferLength, ref ParseSegment currMsgSegment, out ParseSegment argSlot)
        {
            var endMsgOffset = rawBufferLength;
            var argOpenOffset = -1;
            var argCloseOffset = -1;
            var validSlot = false;
            var escapeOpenBrace = false;
            var escapeCloseBrace = false;

            var currWorkingIndex = currMsgSegment.Offset;
            var readIndex = currWorkingIndex;

            while (readIndex < endMsgOffset)
            {
                currWorkingIndex = readIndex;

                // Iterate over each UTF-8 char until we find the start of a format argument slot '{' or reach the end
                // of the message segment (meaning has no format slots)
                if (Unicode.Utf8ToUcs(out var currChar, rawBuffer, ref readIndex, rawBufferLength) != ConversionError.None)
                    continue;

                // End of message string
                if (readIndex >= endMsgOffset)
                    break;

                // "Peek" at next char but don't advance our readIndex
                var tempPos = readIndex;
                if (Unicode.Utf8ToUcs(out var nextChar, rawBuffer, ref tempPos, rawBufferLength) != ConversionError.None)
                    continue;

                // Check for escaped braces ("{{" and "}}") which we'll treat as special case format slots were the
                // double brace is replaced by an argument string holding a single brace char
                if (currChar.value == '{' && nextChar.value == '{')
                {
                    argOpenOffset = currWorkingIndex;
                    argCloseOffset = currWorkingIndex + 1;
                    escapeOpenBrace = true;
                }
                else if (currChar.value == '}' && nextChar.value == '}')
                {
                    argOpenOffset = currWorkingIndex;
                    argCloseOffset = currWorkingIndex + 1;
                    escapeCloseBrace = true;
                }
                else if (currChar.value == '{' && tempPos < endMsgOffset)
                {
                    // Found a valid Open to a parameter slot, make sure it also has a valid Close
                    argOpenOffset = currWorkingIndex;
                    readIndex = tempPos;

                    while (readIndex < endMsgOffset)
                    {
                        currWorkingIndex = readIndex;

                        if (Unicode.Utf8ToUcs(out currChar, rawBuffer, ref readIndex, rawBufferLength) != ConversionError.None)
                            continue;

                        // Find the first closing brace
                        // NOTE: Won't check for escaping '}' because of this situation: "{{{0}}}" which we expect to output: "{hello}"
                        // If we check for escaped brace we'll end up with "{0}}": first pair of closing braces produces a '}' which invalidates format expansion
                        if (currChar.value == '}')
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

                argSlot = new ParseSegment {
                    Offset = argOpenOffset,
                    Length = argCloseOffset - argOpenOffset + 1
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
                var remainingLength = endMsgOffset - currMsgSegment.Offset;
                if (remainingLength > 0)
                {
                    while (remainingLength > 0 && rawBuffer[currMsgSegment.Offset + remainingLength - 1] == 0)
                        remainingLength--;
                }

                currMsgSegment.Length = remainingLength;
            }

            return result;
        }
    }
}
