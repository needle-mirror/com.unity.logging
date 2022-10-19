using Unity.Collections;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Structure that describes numeric formatting
    /// </summary>
    /// <remarks>
    ///
    /// For more information on numeric formatting, see Microsoft's documentation on <a href="https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings">standard numeric formatting</a> and <a href="https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings">custom format strings</a>.
    /// </remarks>
    public readonly struct NumericFormat
    {
        /// <summary>
        /// Letter-specifier. For {0:C2} this is 2
        /// </summary>
        public readonly uint Precision; //used for LeftZeroes
        /// <summary>
        /// Letter-specifier. For {0:C2} this is C
        /// </summary>
        public readonly Specifier Spec;
        /// <summary>
        /// If Letter-specifier is uppercase
        /// </summary>
        public readonly bool UpperCase;

        // custom specifier data:

        // "The position of the leftmost zero before the decimal point and the rightmost zero after the decimal point determines the range of digits that are always present in the result string."
        // So the outermost zeroes determine minimum precision, while real precision may vary
        // {0:00000000} 12386       = 00012386
        // {0:00000000} 12386000    = 12386000
        // {0:G8} 12386000          = 12386000
        // {0:G5} 12386             = 12386

        /// <summary>
        /// Number of digits to insert comma between character groups, e.g. a GroupSeparator of 3 applied to 1234567890 = 1,234,567,890
        /// </summary>
        public readonly uint GroupSeparatorDigits;

        /// <summary>
        /// Number of times to divide number by 1,000
        /// </summary>
        public readonly uint NumberScalingSeparators;

        /// <summary>
        /// Standard format specifier. See https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
        /// </summary>
        public enum Specifier : byte
        {
            /// <summary>
            /// Format string is empty.
            /// </summary>
            None = 0,

            /// <summary>
            ///'C' or 'c'. A currency value.
            /// All numeric types.
            /// </summary>
            Currency,
            /// <summary>
            /// 'D' or 'd'. Integer digits with optional negative sign.
            /// Integral types only.
            /// </summary>
            Decimal,
            /// <summary>
            /// 'E' or 'e'. Exponential notation.
            /// All numeric types.
            /// </summary>
            Scientific,
            /// <summary>
            /// 'F' or 'f'. Integral and decimal digits with optional negative sign.
            /// All numeric types.
            /// </summary>
            FixedPoint,
            /// <summary>
            /// 'G' or 'g'. The more compact of either fixed-point or scientific notation.
            /// All numeric types.
            /// </summary>
            General,
            /// <summary>
            /// 'N' or 'n'. Integral and decimal digits, group separators, and a decimal separator with optional negative sign.
            /// All numeric types.
            /// </summary>
            Number,
            /// <summary>
            /// 'P' or 'p'. Number multiplied by 100 and displayed with a percent symbol.
            /// All numeric types.
            /// </summary>
            Percent,
            /// <summary>
            /// 'R' or 'r'. A string that can round-trip to an identical number.
            /// Single, Double, and BigInteger.
            /// </summary>
            RoundTrip,
            /// <summary>
            /// 'X' or 'x'. A hexadecimal string.
            /// Integral types only.
            /// </summary>
            Hex,
            /// <summary>
            /// Any specifier string that does not fit the format of a standard specifier string is a custom specifier string.
            /// All numeric types.
            /// </summary>
            /// <remarks>
            /// A custom specifier string may contain one or more custom specifiers in sequence.
            /// For instance, while '0' is a single custom format specifier, a custom format specifier string of "000" represents three separate and individual '0' custom format specifiers.
            /// Currently supported custom specifiers: '0', '#', '.', and ','. Other characters passed in a custom specifier string in this manner are copied to the result string unchanged.
            /// <seealso cref="CustomSpecifier"/>
            /// </remarks>
            Custom
        }

        /// <summary>
        /// Custom format specifier. See https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings
        /// </summary>
        public enum CustomSpecifier : byte
        {
            /// <summary>
            /// '0'. Serves as a zero-placeholder symbol.
            /// Replaces the zero with the corresponding digit if one is present; otherwise, zero appears in the result string.
            /// </summary>
            Zero,
            /// <summary>
            /// '#'. Serves as a digit-placeholder symbol.
            /// Replaces the "#" symbol with the corresponding digit if one is present; otherwise, no digit appears in the result string.
            /// This effectively truncates non-significant 0s in the input string.
            /// </summary>
            Digit,
            /// <summary>
            /// '.'. Inserts a decimal separator into the result string.
            /// The first period in the format string determines the location of the decimal separator in the formatted value.
            /// Any additional periods are ignored.
            /// </summary>
            Decimal,
            /// <summary>
            /// ','. Serves two possible purposes: group separator or number scaling specifier.
            /// If a ',' is placed between two '0' or '#' placeholders, a group separator character is inserted between each number group in the integral part of the output (i.e. the side of the decimal point that describes whole numbers).
            /// If one or more ',' characters are placed immediately to the left of the explicit or implicit decimal point, these are instead number scaling specifiers. The number in the output will be divided by 1000 for each number scaling specifier.
            /// </summary>
            Comma,
            /// <summary>
            /// '%'
            /// </summary>
            Percentage,
            /// <summary>
            /// '‰'
            /// </summary>
            PerMille,
            /// <summary>
            /// One of 'E0', 'E-0', 'E+0', 'e0', 'e-0', 'e+0'
            /// </summary>
            Exponential,
            /// <summary>
            /// '\'
            /// </summary>
            Escape,
            /// <summary>
            /// A string surrounded by a pair of enclosing ' characters.
            /// </summary>
            LiteralDelimiterApostrophe,
            /// <summary>
            /// A string surrounded by a pair of enclosing " characters.
            /// </summary>
            LiteralDelimiterQuotationMark,
            /// <summary>
            /// ';'
            /// </summary>
            Section,
            /// <summary>
            /// Any character in a custom specifier string that doesn't match any known custom specifier is copied to the string unchanged.
            /// </summary>
            Other,
        }

        static Specifier FirstByteToSpecifier(byte firstByte)
        {
            switch (firstByte)
            {
                case (byte)'c':
                case (byte)'C':
                    return Specifier.Currency;
                case (byte)'d':
                case (byte)'D':
                    return Specifier.Decimal;
                case (byte)'e':
                case (byte)'E':
                    return Specifier.Scientific;
                case (byte)'f':
                case (byte)'F':
                    return Specifier.FixedPoint;
                case (byte)'g':
                case (byte)'G':
                    return Specifier.General;
                case (byte)'n':
                case (byte)'N':
                    return Specifier.Number;
                case (byte)'p':
                case (byte)'P':
                    return Specifier.Percent;
                case (byte)'r':
                case (byte)'R':
                    return Specifier.RoundTrip;
                case (byte)'x':
                case (byte)'X':
                    return Specifier.Hex;
                default:
                    return Specifier.Custom;
            }
        }

        internal static CustomSpecifier FirstByteToCustomSpecifier(byte specifierByte)
        {
            switch(specifierByte)
            {
                case (byte)'0':
                    return CustomSpecifier.Zero;
                case (byte)'#':
                    return CustomSpecifier.Digit;
                case (byte)'.':
                    return CustomSpecifier.Decimal;
                case (byte)',':
                    return CustomSpecifier.Comma;
                case (byte)'\\':
                    return CustomSpecifier.Escape;
                case (byte)'%':
                    return CustomSpecifier.Percentage;
                case (byte)'\'':
                    return CustomSpecifier.LiteralDelimiterApostrophe;
                case (byte)'\"':
                    return CustomSpecifier.LiteralDelimiterQuotationMark;
                case (byte)';':
                    return CustomSpecifier.Section;

                //Maybes:
                //Exponential specifiers take multiple characters, this signifies the potential start of one
                case (byte)'E':
                case (byte)'e':
                    return CustomSpecifier.Exponential;

                //'‰' is a Unicode character, 0xE280B0 in UTF-8 -- thankfully, the only UTF-8 character specifier
                case 0xE2:
                    return CustomSpecifier.PerMille;
                default:
                    return CustomSpecifier.Other;
            }
        }

        // Builds string that serves as a template intermediate string to later replace digit placeholders with actual placeholders
        // todo: change this to reference an 'in' variable string, and return FormatError of Append
        internal static FormatError GetCustomFormatTemplateString(ref FixedString512Bytes result, ref FixedString512Bytes fullFormat, bool emitLiterals = true)
        {
            int possibleLiteralStringStartIndex = -1;
            byte literalStringDelimiterToMatch = (byte)'\'';
            bool escapeCharacter = false;
            bool encounteredDecimal = false;
            int possibleExponentialStartIndex = -1;
            FormatError formatErrorResult = FormatError.None;

            for (int formatLoopIndex = 0; (formatLoopIndex < fullFormat.Length) && (formatErrorResult == FormatError.None); ++formatLoopIndex)
            {
                byte b = fullFormat.ElementAt(formatLoopIndex);

                // next character after '\' is guaranteed literal
                if (escapeCharacter)
                {
                    if (emitLiterals == true)
                    {
                        formatErrorResult = result.AppendRawByte(b);
                        if(formatErrorResult != FormatError.None)
                        {
                            return formatErrorResult;
                        }
                    }
                    escapeCharacter = false;
                }
                else if (possibleLiteralStringStartIndex >= 0)
                {
                    // walk to next '\'' byte or end of string
                    // if apostrophe found: append enclosed literal to result string, new index is second apostrophe index
                    // if end of string found instead: skip the apostrophe, resume at initial index, continue parsing rest of string as normal
                    int possibleLiteralStringEndIndex = formatLoopIndex;
                    while(possibleLiteralStringEndIndex < fullFormat.Length)
                    {
                        if (fullFormat.ElementAt(possibleLiteralStringEndIndex) == literalStringDelimiterToMatch)
                        {
                            if (emitLiterals == true)
                            {
                                for(int literalStringIndex = possibleLiteralStringStartIndex + 1; literalStringIndex < possibleLiteralStringEndIndex; ++literalStringIndex)
                                {
                                    byte literalByte = fullFormat.ElementAt(literalStringIndex);
                                    if (FirstByteToCustomSpecifier(literalByte) == CustomSpecifier.Zero || FirstByteToCustomSpecifier(literalByte) == CustomSpecifier.Digit)
                                    {
                                        // prepend with backslash if this is a digit placeholder
                                        formatErrorResult = result.AppendRawByte((byte)'\\');
                                        if(formatErrorResult != FormatError.None)
                                        {
                                            return formatErrorResult;
                                        }
                                    }

                                    formatErrorResult = result.AppendRawByte(literalByte);
                                    if(formatErrorResult != FormatError.None)
                                    {
                                        return formatErrorResult;
                                    }
                                }
                            }
                            formatLoopIndex = possibleLiteralStringEndIndex;
                            break;
                        }
                        else
                        {
                            ++possibleLiteralStringEndIndex;
                        }
                    }
                    possibleLiteralStringStartIndex = -1;
                }
                else if (possibleExponentialStartIndex >= 0)
                {
                    // E or e, followed by optional +/-, then 1 to n zeroes
                    // if that pattern breaks before reaching a zero, doesn't form an exponential specifier
                    int possibleExponentialEndIndex = formatLoopIndex;
                    if (b == (byte)'+' || b == (byte)'-')
                    {
                        // skip to next byte for now
                        ++possibleExponentialEndIndex;
                    }
                    // if we reached the past end of the string in this manner, Ee+- are literals, not specifiers
                    if (possibleExponentialEndIndex >= fullFormat.Length)
                    {
                        possibleExponentialEndIndex = -1;
                    }
                    // if this next character is a zero, we've for-sure found an exponential specifier
                    else if (fullFormat.ElementAt(possibleExponentialEndIndex) == (byte)'0')
                    {
                        // iterate until either end of string reached or first non-zero character
                        // then set loop index to resume from after end of full exponential specifier pattern
                        // loop will naturally end at 1 byte past end of specifier pattern
                        while(possibleExponentialEndIndex < fullFormat.Length && fullFormat.ElementAt(possibleExponentialEndIndex) == (byte)'0')
                        {
                            possibleExponentialEndIndex += 1;
                        }

                        // went forward one too many steps, go back 1 now
                        formatLoopIndex = possibleExponentialEndIndex - 1;
                    }

                    // didn't match exponential specifier pattern, append initial 'Ee' byte, continue from loop index normally from just after 'Ee'
                    if (possibleExponentialEndIndex == -1)
                    {
                        formatLoopIndex -= 1;
                        formatErrorResult = result.AppendRawByte(fullFormat.ElementAt(formatLoopIndex));
                        if(formatErrorResult != FormatError.None)
                        {
                            return formatErrorResult;
                        }
                    }

                    possibleExponentialStartIndex = -1;
                }
                else
                {
                    switch(FirstByteToCustomSpecifier(b))
                    {
                        case CustomSpecifier.Other:
                            if (emitLiterals)
                            {
                                formatErrorResult = result.AppendRawByte(b);
                                if(formatErrorResult != FormatError.None)
                                {
                                    return formatErrorResult;
                                }
                            }
                            break;
                        case CustomSpecifier.Zero:
                        case CustomSpecifier.Digit:
                            formatErrorResult = result.AppendRawByte(b);
                            if(formatErrorResult != FormatError.None)
                            {
                                return formatErrorResult;
                            }
                            break;
                        case CustomSpecifier.Decimal:
                            if (!encounteredDecimal)
                            {
                                encounteredDecimal = true;
                                formatErrorResult = result.AppendRawByte(b);
                                if(formatErrorResult != FormatError.None)
                                {
                                    return formatErrorResult;
                                }
                            }
                            break;
                        case CustomSpecifier.Escape:
                            if (emitLiterals == true)
                            {
                                formatErrorResult = result.AppendRawByte(b);
                                if(formatErrorResult != FormatError.None)
                                {
                                    return formatErrorResult;
                                }
                            }
                            escapeCharacter = true;
                            break;
                        case CustomSpecifier.LiteralDelimiterApostrophe:
                            possibleLiteralStringStartIndex = formatLoopIndex;
                            literalStringDelimiterToMatch = (byte)'\'';
                            break;
                        case CustomSpecifier.LiteralDelimiterQuotationMark:
                            possibleLiteralStringStartIndex = formatLoopIndex;
                            literalStringDelimiterToMatch = (byte)'\"';
                            break;
                        case CustomSpecifier.Exponential:
                            // only actually a specifier if 2-3 specific bytes are in sequence
                            // if this is the last byte in the string, it has to be a literal
                            if (formatLoopIndex >= fullFormat.Length - 2)
                            {
                                possibleExponentialStartIndex = formatLoopIndex;
                            }
                            else if (emitLiterals == true)
                            {
                                formatErrorResult = result.AppendRawByte(b);
                                if(formatErrorResult != FormatError.None)
                                {
                                    return formatErrorResult;
                                }
                            }
                            break;
                        case CustomSpecifier.PerMille:
                            // only actually a specifier if 3 specific bytes are in sequence: 0xE280B0
                            // found E2, so now make sure the string has at least 2 more characters in string to check:
                            if (formatLoopIndex >= fullFormat.Length - 3)
                            {
                                if (fullFormat.ElementAt(formatLoopIndex + 1) == 0x80 && fullFormat.ElementAt(formatLoopIndex + 2) == 0xB0)
                                {
                                    //Per-mille specifier detected, skip past it and don't copy any of these three bytes to the template string
                                    formatLoopIndex += 2;
                                }
                                else if (emitLiterals == true)
                                {
                                    // it's just a non-specifier literal
                                    formatErrorResult = result.AppendRawByte(b);
                                    if(formatErrorResult != FormatError.None)
                                    {
                                        return formatErrorResult;
                                    }
                                }
                            }
                            else if (emitLiterals == true)
                            {
                                // it's just a non-specifier literal
                                formatErrorResult = result.AppendRawByte(b);
                                if(formatErrorResult != FormatError.None)
                                {
                                    return formatErrorResult;
                                }
                            }
                            break;
                        case CustomSpecifier.Comma:
                        case CustomSpecifier.Percentage:
                        case CustomSpecifier.Section:
                        default:
                            // don't carry non-escaped custom specifier bytes over to template string
                            break;
                    }
                }
            }
            return formatErrorResult;
        }

        /// <summary>
        /// Parses format string in the argument info structure.
        /// </summary>
        /// <param name="arg">Argument info from the message string</param>
        public static NumericFormat Parse(ref ArgumentInfo arg)
        {
            // https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings

            var n = arg.Format.Length;

            if (n == 0)
            {
                return NumericFormat.Empty();
            }

            var firstByte = arg.Format[0];
            var spec = FirstByteToSpecifier(firstByte);

            if (spec != Specifier.Custom)
            {
                var upperCase = firstByte >= 'A' && firstByte <= 'Z';
                if (n > 1)
                {
                    uint precision = 0;
                    var strCopy = arg.Format;
                    var offset = 1;
                    if (strCopy.Parse(ref offset, ref precision) == ParseError.None && offset == n)
                    {
                        // precision parsed successfully
                        return NumericFormat.Standard(spec, precision, upperCase);
                    }

                    // that's not a precision - probably custom format
                    return NumericFormat.Custom(ref strCopy);
                }

                // no precision
                return NumericFormat.Standard(spec, 0, upperCase);
            }
            else
            {
                var strCopy = arg.Format;
                return NumericFormat.Custom(ref strCopy);
            }
        }

        private NumericFormat(Specifier spec, uint precision = 0, bool upperCase = false, int decimalPointLocation = -1, uint decimalPrecision = 0, uint groupSeparatorDigits = 0, uint numberScalingSeparators = 0)
        {
            Spec = spec;
            Precision = precision;
            UpperCase = upperCase;

            GroupSeparatorDigits = groupSeparatorDigits;
            NumberScalingSeparators = numberScalingSeparators;
        }

        private static NumericFormat Empty()
        {
            return new NumericFormat(Specifier.None);
        }

        private static NumericFormat Standard(Specifier spec, uint precision, bool upperCase)
        {
            return new NumericFormat(spec, precision, upperCase);
        }

        internal static readonly FixedString32Bytes k_commaRune = ",";
        internal static readonly FixedString32Bytes k_decimalPointRune = ".";
        internal static readonly FixedString32Bytes k_zeroRune = "0";
        internal static readonly FixedString32Bytes k_digitRune = "#";

        internal const uint k_GroupSeparatorSize = 3;
        internal const uint k_NumberScalingDivisor = 1000;

        private static NumericFormat Custom(ref FixedString512Bytes format)
        {
            // copy data relevant for number without decorating characters and literals to a separate string
            FixedString512Bytes formatWithOnlyDigitsAndDecimalPoint = new();
            GetCustomFormatTemplateString(ref formatWithOnlyDigitsAndDecimalPoint, ref format, false);

            // parse position of decimal in format; if present in string, use this to figure out the semantics of other format specifiers
            int decimalPointLocation = format.IndexOf(k_decimalPointRune);
            int decimalPointLocationDigitsOnly = formatWithOnlyDigitsAndDecimalPoint.IndexOf(k_decimalPointRune);

            // 0 specifier, leftmost zero in integral portion and rightmost zero in decimal portion define total precision
            // in numeric specifiers only
            uint precision = 0;
            uint fractionalPrecision = 0;

            // both versions of the string -- with just digits and decimal point, and with all characters -- are useful for different reasons
            // integral portion of full string: not used immediately, but will be used later
            var integralString = format;
            if (decimalPointLocation != -1)
            {
                var tempString = new FixedString512Bytes();
                for(int i = 0; i < decimalPointLocation; ++i)
                {
                    tempString.AppendRawByte(integralString[i]);
                }
                integralString.CopyFrom(tempString);
            }

            // integral portion of string stripped of everything but zero/digit placeholders and decimal point:
            var integralStringDigitsOnly = formatWithOnlyDigitsAndDecimalPoint;
            if (decimalPointLocationDigitsOnly != -1)
            {
                var tempString = new FixedString512Bytes();
                for(int i = 0; i < decimalPointLocationDigitsOnly; ++i)
                {
                    tempString.Append(formatWithOnlyDigitsAndDecimalPoint[i]);
                }
                integralStringDigitsOnly.CopyFrom(tempString);
            }

            precision = (uint)integralStringDigitsOnly.Length;
            fractionalPrecision = decimalPointLocationDigitsOnly == -1 ? 0 : (uint)(formatWithOnlyDigitsAndDecimalPoint.Length - precision - 1);

            // , specifier, both cases: only relevant in integral portion of number, neither relevant to the right of the decimal point
            // back to using the full format string instead of the one stripped to just digit placeholders and the decimal point
            // group separator: is at least one ',' not immediately to the left of the decimal point?
            // e.g. #,#.# inserts commas after every third digit
            uint groupSeparatorSize = 0;

            // scaling separator: divide number by 1,000 once for each ',' character immediately to the left of decimal point
            // e.g. #,.# divides number by 1,000, #,,.# divides by 1,000,000, etc. -- variable is number of times to divide by 1,000
            uint numberScalingSeparators = 0;

            int firstCommaPosition = integralString.IndexOf(k_commaRune);

            // if no ',' character is present at all, don't do anything
            if (firstCommaPosition != -1)
            {
                int lastCommaPosition = integralString.LastIndexOf(k_commaRune);

                // first, see if there are any commas immediately to the left of decimal
                // if there are, these contiguous commas are the scaling separators.
                // count how many there are, determine how many times the value will be divided by 1000
                if (lastCommaPosition == integralString.Length - 1)
                {
                    while(integralString[(int)(integralString.Length - numberScalingSeparators - 1)] == ',')
                    {
                        ++numberScalingSeparators;
                    }
                }

                // position of first scaling (divide by 1000) separator
                var firstScalingSeparatorCommaPosition = integralString.Length - numberScalingSeparators;

                // handle any commas on the left of all zeroes/digits, e.g. the comma in ",#0" should be outright ignored but "#,0" should be paid attention to
                // leftmost index of either '#' or '0'
                var leftmostZero = integralString.IndexOf(k_zeroRune);
                var leftmostDigit = integralString.IndexOf(k_digitRune);
                int leftmostNumber = -1;
                if (leftmostZero != -1 && leftmostDigit != -1)
                {
                    // min of either of both are present
                    leftmostNumber = (leftmostZero <= leftmostDigit) ? leftmostZero : leftmostDigit;
                }
                else if (leftmostZero != -1 && leftmostDigit == -1)
                {
                    leftmostNumber = leftmostZero;
                }
                else if (leftmostZero == -1 && leftmostDigit != -1)
                {
                    leftmostNumber = leftmostDigit;
                }

                // group separator size is 0 by default (don't insert commas) or 3 (insert commas between every three digits in integral portion of number string)
                if (leftmostNumber >= 0 && firstCommaPosition > leftmostNumber && firstCommaPosition < firstScalingSeparatorCommaPosition)
                {
                    groupSeparatorSize = k_GroupSeparatorSize;
                }
            }

            return new NumericFormat(Specifier.Custom, precision, false, decimalPointLocationDigitsOnly, fractionalPrecision, groupSeparatorSize, numberScalingSeparators);
        }
    }
}
