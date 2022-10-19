using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging
{
    /// <summary>
    /// Utilities for writing JSON
    /// </summary>
    [BurstCompile]
    public static class JsonWriter
    {
        /// <summary>
        /// Appends escaped JSON UTF8 string into <see cref="UnsafeText"/>
        /// </summary>
        /// <param name="jsonOutput">UnsafeText append to</param>
        /// <param name="rawMsgBuffer">UTF8 string to escape</param>
        /// <param name="rawMsgBufferLength">Length of the UTF8 string to escape</param>
        public static unsafe void AppendEscapedJsonString(ref UnsafeText jsonOutput, byte* rawMsgBuffer, int rawMsgBufferLength)
        {
            for (var offset = 0; offset < rawMsgBufferLength;)
            {
                Unicode.Utf8ToUcs(out var rune, rawMsgBuffer, ref offset, rawMsgBufferLength);

                // https://www.json.org/json-en.html
                // https://www.ietf.org/rfc/rfc4627.txt

                // must be escaped:
                //quotation mark, reverse solidus, and the control characters (U+0000
                //through U+001F).

                var controlCharC0 = rune.value <= 0x1F || rune.value == 0x7F;
                var controlCharC1 = rune.value >= 0x80 && rune.value <= 0x9F;
                var controlUnicode = rune.value == 0x85;

                var controlChar = controlCharC0 || controlCharC1 || controlUnicode;

                if (controlChar == false)
                {
                    var prependSlash = rune.value == '"' || rune.value == '\\'; // || rune.value == '/';
                    if (prependSlash)
                    {
                        jsonOutput.Append('\\');
                    }

                    jsonOutput.Append(rune);
                }
                else
                {
                    if (rune.value == 0)
                    {
                        // null terminator
                        break;
                    }

                    if (rune.value == 8)
                    {
                        // \b  Backspace
                        jsonOutput.Append('\\');
                        jsonOutput.Append('b');
                    }
                    else if (rune.value == 12)
                    {
                        // \f  Form feed (0C)
                        jsonOutput.Append('\\');
                        jsonOutput.Append('f');
                    }
                    else if (rune.value == 10)
                    {
                        // \n linefeed (0A)
                        jsonOutput.Append('\\');
                        jsonOutput.Append('n');
                    }
                    else if (rune.value == 13)
                    {
                        // \r carriage return (0D)
                        jsonOutput.Append('\\');
                        jsonOutput.Append('r');
                    }
                    else if (rune.value == 9)
                    {
                        // \t tab
                        jsonOutput.Append('\\');
                        jsonOutput.Append('t');
                    }
                    else
                    {
                        jsonOutput.Append('\\');
                        jsonOutput.Append('u');
                        // 0000
                        var i = 0;
                        var n = rune.value;
                        FixedString32Bytes hexaDeciNum = default;
                        hexaDeciNum.Append('0');
                        hexaDeciNum.Append('0');
                        hexaDeciNum.Append('0');
                        hexaDeciNum.Append('0');
                        while (n != 0 && i <= 3)
                        {
                            var temp = n % 16;

                            if (temp < 10)
                                hexaDeciNum[3 - i] = (byte)(temp + '0');
                            else
                                hexaDeciNum[3 - i] = (byte)(temp + 'a' - 10);

                            i++;
                            n /= 16;
                        }
                        jsonOutput.Append(hexaDeciNum);
                    }
                }
            }
        }
    }
}
