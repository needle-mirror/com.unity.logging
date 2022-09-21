using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.Assertions;

namespace Unity.Logging.Internal
{
    [BurstCompile]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public static class FormatString
    {
        readonly struct DefaultNumericFormatData
        {
            public readonly int LeftSpaces;
            public readonly byte Minus;
            public readonly int LeftZeroes;
            public readonly int Data;
            public readonly int RightSpaces;
            public readonly int Total;

            public DefaultNumericFormatData(int lengthWithoutSign, ref NumericFormat numericFormatInfo, ref IntNumericFormatter numFormatter, int alignment, bool useMinus)
            {
                Data = lengthWithoutSign;
                Minus = useMinus ? numFormatter.sign : (byte)0;

                //                               lengthWithoutSign |  Precision  |  trailZero  |  Alignment  |  spaceCount
                // ({0,6:D4}, -42) --> ' -0042'         2          |      4      |     2       |      6      |      1
                // ({0,-5:D3},  42) --> '042  '         2          |      3      |     1       |      5      |      2
                // ({0,-5:D3}, -42) --> '-042 '         2          |      3      |     1       |      5      |      1
                // ({0,-5:D3}, -42) --> '-042 '         2          |      3      |     1       |      5      |      1
                // ({0,6:D4}), -543210 >'-543210'       6          |      4      |     0       |      6      |      0
                LeftZeroes = (int)(numericFormatInfo.Precision - Data);
                if (LeftZeroes < 0) LeftZeroes = 0;

                var leftAlignment = (alignment > 0);
                var absAlignment = alignment < 0 ? -alignment : alignment;
                var spaceCount = absAlignment - Minus - Data - LeftZeroes;
                if (spaceCount < 0) spaceCount = 0;

                if (leftAlignment)
                {
                    LeftSpaces = spaceCount;
                    RightSpaces = 0;
                }
                else
                {
                    LeftSpaces = 0;
                    RightSpaces = spaceCount;
                }

                Total = LeftSpaces + Minus + LeftZeroes + Data + RightSpaces;
            }
        }

        [SkipLocalsInit]
        private static FormatError AppendFormat<T>(ref T fs, ref IntNumericFormatter numFormatter, ref NumericFormat numericFormatInfo, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            if(numericFormatInfo.Spec == NumericFormat.Specifier.Custom)
            {
                return CustomNumericFormatInteger(ref fs, ref numFormatter, ref numericFormatInfo, ref arg);
            }
            // implicit else

            var lengthWithoutSign = 0;
            var useMinus = true;

            switch (numericFormatInfo.Spec)
            {
                case NumericFormat.Specifier.Decimal:
                {
                    lengthWithoutSign = FormatStringUtils.LengthDec(in numFormatter, false);
                    break;
                }
                case NumericFormat.Specifier.Hex:
                {
                    useMinus = false;
                    lengthWithoutSign = FormatStringUtils.LengthHex(in numFormatter, false);
                    break;
                }
                default:
                    if (numFormatter.IsNegative)
                        return fs.Append(-(long)numFormatter.number);
                    return fs.Append(numFormatter.number);
            }

            var info = new DefaultNumericFormatData(lengthWithoutSign, ref numericFormatInfo, ref numFormatter, arg.Alignment, useMinus);

            var fsOffset = fs.Length;
            if (fs.TryResize(fs.Length + info.Total, NativeArrayOptions.UninitializedMemory) == false)
                return FormatError.Overflow;

            unsafe
            {
                var data = &fs.GetUnsafePtr()[fsOffset];
                var offset = 0;
                for (var i = 0; i < info.LeftSpaces; i++)
                    data[offset++] = (byte)' ';
                if (info.Minus == 1)
                    data[offset++] = (byte)'-';
                for (var i = 0; i < info.LeftZeroes; i++)
                    data[offset++] = (byte)'0';

                switch (numericFormatInfo.Spec)
                {
                    case NumericFormat.Specifier.Decimal:
                    {
                        FormatStringUtils.AppendDec(in numFormatter, &data[offset], info.Data);
                        break;
                    }
                    case NumericFormat.Specifier.Hex:
                    {
                        FormatStringUtils.AppendHex(in numFormatter, &data[offset], info.Data, numericFormatInfo.UpperCase);
                        break;
                    }
                }

                offset += info.Data;
                for (var i = 0; i < info.RightSpaces; i++)
                    data[offset++] = (byte)' ';
            }

            return FormatError.None;
        }

        // Custom formatting lives in its own method here, away from standard numeric formatting
        private static FormatError CustomNumericFormatInteger<T>(ref T fs, ref IntNumericFormatter numFormatter, ref NumericFormat numericFormatInfo, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            FormatError formatError = FormatError.None;

            // handle number scaling separator, round trailing portion further away from 0
            ulong effectiveValue = numFormatter.number;
            ulong remainder = 0;
            for (uint i = 0; i < numericFormatInfo.NumberScalingSeparators; ++i)
            {
                ulong tempRemainderPortion = effectiveValue % NumericFormat.k_NumberScalingDivisor;
                for(uint j = 0; j < i; ++j)
                {
                    tempRemainderPortion *= NumericFormat.k_NumberScalingDivisor;
                }
                remainder += tempRemainderPortion;
                effectiveValue /= NumericFormat.k_NumberScalingDivisor;
            }

            // now we should have a string with the raw numbers
            // integer type only ever has implicit decimal separator, never explicitly defined
            var integralIntermediateString = new FixedString512Bytes();
            integralIntermediateString.Append(effectiveValue);

            var fractionalIntermediateString = new FixedString512Bytes();
            fractionalIntermediateString.Append(remainder);

            var fullFormat = arg.Format;
            var templateString = new FixedString512Bytes();
            NumericFormat.GetCustomFormatTemplateString(ref templateString, ref fullFormat);

            // walk from decimal point to copy intermediate values to final string:
            int decimalPointLocationInTemplate = templateString.IndexOf(NumericFormat.k_decimalPointRune);
            int templateIntegralLength = decimalPointLocationInTemplate > 0 ? decimalPointLocationInTemplate : templateString.Length;

            // for integral: start at left of decimal point of intermediate value, then go from right to left
            var outputIntegralString = new FixedString512Bytes();
            var outputIntegralStringReverse = new FixedString512Bytes();
            int digitsCopiedFromIntermediateIntegralString = 0;
            int digitsInOutput = 0;

            // expend template string first
            for (int spacesLeftOfDecimalInTemplate = 1; spacesLeftOfDecimalInTemplate <= templateIntegralLength; ++spacesLeftOfDecimalInTemplate)
            {
                byte currentByteInTemplate = templateString[templateIntegralLength - spacesLeftOfDecimalInTemplate];
                if (currentByteInTemplate != (byte)'0' && currentByteInTemplate != (byte)'#')
                {
                    // if literal in template, just append the literal
                    outputIntegralStringReverse.AppendRawByte(currentByteInTemplate);
                }
                else
                {
                    // check if this 0 or # is escaped, if next space left is '\'
                    if (spacesLeftOfDecimalInTemplate + 1 <= templateIntegralLength)
                    {
                        if (templateString.ElementAt(templateIntegralLength - (spacesLeftOfDecimalInTemplate + 1)) == (byte)'\\')
                        {
                            outputIntegralStringReverse.AppendRawByte(currentByteInTemplate);
                            ++spacesLeftOfDecimalInTemplate;
                            continue;
                        }
                    }

                    // past this point, it's a non-literal digit placeholder for sure
                    // if group separator specifier was found, append group separator character after enough digits if another is being added
                    // e.g. if this is our fourth digit, insert the comma before the fourth digit
                    if (numericFormatInfo.GroupSeparatorDigits > 0 && (digitsInOutput >= numericFormatInfo.GroupSeparatorDigits) && (digitsInOutput % numericFormatInfo.GroupSeparatorDigits == 0))
                    {
                        outputIntegralStringReverse.Append(NumericFormat.k_commaRune);
                    }

                    // digit placeholder in template
                    switch(currentByteInTemplate)
                    {
                        case (byte)'0':
                            // if we have any intermediate digits left, copy them here, otherwise append '0' itself
                            byte toAppend;
                            if (digitsCopiedFromIntermediateIntegralString < integralIntermediateString.Length)
                            {
                                toAppend = integralIntermediateString[integralIntermediateString.Length - 1 - digitsCopiedFromIntermediateIntegralString];
                                ++digitsCopiedFromIntermediateIntegralString;
                                ++digitsInOutput;
                            }
                            else
                            {
                                toAppend = (byte)'0';
                                ++digitsInOutput;
                            }
                            outputIntegralStringReverse.AppendRawByte(toAppend);
                            break;
                        case (byte)'#':
                            // if we have any intermediate digits left, copy them here, otherwise take no action
                            if (digitsCopiedFromIntermediateIntegralString < integralIntermediateString.Length)
                            {
                                outputIntegralStringReverse.AppendRawByte(integralIntermediateString[integralIntermediateString.Length - 1 - digitsCopiedFromIntermediateIntegralString]);
                                ++digitsCopiedFromIntermediateIntegralString;
                                ++digitsInOutput;
                            }
                            break;
                    }
                    // finally, is this the last placeholder zero/digit in the integral portion of the template?
                    // if so, emit the rest of the digits from the intermediate string before moving onto literals
                    if (digitsCopiedFromIntermediateIntegralString >= numericFormatInfo.Precision)
                    {
                        // done with template, now straight copy to the end of the intermediate integral string
                        while(digitsCopiedFromIntermediateIntegralString < integralIntermediateString.Length)
                        {
                            // if group separator specifier was found, append group separator character after enough digits if another is being added
                            // e.g. if this is our fourth digit, insert the comma before the fourth digit
                            if (numericFormatInfo.GroupSeparatorDigits > 0 && (digitsInOutput >= numericFormatInfo.GroupSeparatorDigits) && (digitsInOutput % numericFormatInfo.GroupSeparatorDigits == 0))
                            {
                                outputIntegralStringReverse.Append(NumericFormat.k_commaRune);
                            }

                            outputIntegralStringReverse.AppendRawByte(integralIntermediateString[integralIntermediateString.Length - 1 - digitsCopiedFromIntermediateIntegralString]);
                            ++digitsCopiedFromIntermediateIntegralString;
                            ++digitsInOutput;
                        }
                    }
                }
            }

            // done with template, now straight copy to the end of the intermediate integral string
            while(digitsCopiedFromIntermediateIntegralString < integralIntermediateString.Length)
            {
                // if group separator specifier was found, append group separator character after enough digits if another is being added
                // e.g. if this is our fourth digit, insert the comma before the fourth digit
                if (numericFormatInfo.GroupSeparatorDigits > 0 && (digitsInOutput >= numericFormatInfo.GroupSeparatorDigits) && (digitsInOutput % numericFormatInfo.GroupSeparatorDigits == 0))
                {
                    outputIntegralStringReverse.Append(NumericFormat.k_commaRune);
                }

                outputIntegralStringReverse.AppendRawByte(integralIntermediateString[integralIntermediateString.Length - 1 - digitsCopiedFromIntermediateIntegralString]);
                ++digitsCopiedFromIntermediateIntegralString;
                ++digitsInOutput;
            }

            // finally reverse the built integral string
            for (int outputIntegralStringReverseIndex = 0; outputIntegralStringReverseIndex < outputIntegralStringReverse.Length; ++outputIntegralStringReverseIndex)
            {
                outputIntegralString.AppendRawByte(outputIntegralStringReverse[outputIntegralStringReverse.Length - 1 - outputIntegralStringReverseIndex]);
            }

            // for fractional: start right of decimal point, then go from left to right
            var outputFractionalString = new FixedString512Bytes();
            if (decimalPointLocationInTemplate > 0)
            {
                int digitsCopiedFromIntermediateFractionalString = 0;
                // expend template characters first
                for(int spacesRightOfDecimalInTemplate = 1; spacesRightOfDecimalInTemplate < templateString.Length - decimalPointLocationInTemplate; ++spacesRightOfDecimalInTemplate)
                {
                    byte currentByteInTemplate = templateString[decimalPointLocationInTemplate + spacesRightOfDecimalInTemplate];

                    if (currentByteInTemplate != (byte)'0' && currentByteInTemplate != (byte)'#')
                    {
                        // if character is escaped, skip to next character
                        if (currentByteInTemplate == (byte)'\\' && spacesRightOfDecimalInTemplate + 1 < templateString.Length - decimalPointLocationInTemplate)
                        {
                            spacesRightOfDecimalInTemplate++;
                            currentByteInTemplate = templateString[decimalPointLocationInTemplate + spacesRightOfDecimalInTemplate];
                        }
                        // if literal in template, just append the literal
                        outputFractionalString.AppendRawByte(currentByteInTemplate);
                    }
                    else
                    {
                        // digit placeholder in template
                        byte toAppend = (byte)'#';
                        switch(currentByteInTemplate)
                        {
                            case (byte)'0':
                                // if we have any intermediate digits left, copy them here, otherwise append '0' itself
                                if (digitsCopiedFromIntermediateFractionalString < fractionalIntermediateString.Length)
                                {
                                    toAppend = fractionalIntermediateString[digitsCopiedFromIntermediateFractionalString];
                                    ++digitsCopiedFromIntermediateFractionalString;
                                }
                                else
                                {
                                    toAppend = (byte)'0';
                                }
                                break;
                            default:
                            case (byte)'#':
                                // if we have any intermediate digits left, copy them here, otherwise do nothing
                                if (digitsCopiedFromIntermediateFractionalString < fractionalIntermediateString.Length)
                                {
                                    toAppend = fractionalIntermediateString[digitsCopiedFromIntermediateFractionalString];
                                    ++digitsCopiedFromIntermediateFractionalString;
                                }
                                break;
                        }
                        outputFractionalString.AppendRawByte(toAppend);
                    }
                    // done with template; non-significant fractional characters are truncated
                }
            }

            FixedString512Bytes preAlignmentAndSignString = new();

            formatError = preAlignmentAndSignString.Append(outputIntegralString);
            if(formatError == FormatError.None)
            {
                if(outputFractionalString.Length > 0)
                {
                    formatError = preAlignmentAndSignString.Append(NumericFormat.k_decimalPointRune);
                    if(formatError == FormatError.None)
                    {
                        formatError = preAlignmentAndSignString.Append(outputFractionalString);
                    }
                }
            }

            if (formatError != FormatError.None)
            {
                return formatError;
            }

            int minus = numFormatter.IsNegative ? 1 : 0;
            var leftAlignment = (arg.Alignment > 0);
            var absAlignment = arg.Alignment < 0 ? -arg.Alignment : arg.Alignment;
            var spaceCount = absAlignment - minus - preAlignmentAndSignString.Length;
            if (spaceCount < 0) spaceCount = 0;
            int leftSpaces = 0;
            int rightSpaces = 0;
            if (leftAlignment)
            {
                leftSpaces = spaceCount;
            }
            else
            {
                rightSpaces = spaceCount;
            }

            FixedString512Bytes finalResultString = new();

            for(int i = 0; i < leftSpaces; i++)
            {
                finalResultString.Append((byte)' ');
            }
            if (minus > 0)
            {
                finalResultString.Append((byte)'-');
            }
            finalResultString.Append(preAlignmentAndSignString);
            for(int i = 0; i < rightSpaces; i++)
            {
                finalResultString.Append((byte)' ');
            }

            formatError = fs.Append(finalResultString);

            return formatError;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, sbyte input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var numericFormatInfo = NumericFormat.Parse(ref arg);
            if (numericFormatInfo.Spec == NumericFormat.Specifier.None)
                return fs.Append(input);

            var numFormatter = new IntNumericFormatter(input);
            return AppendFormat(ref fs, ref numFormatter, ref numericFormatInfo, ref arg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, byte input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var numericFormatInfo = NumericFormat.Parse(ref arg);
            if (numericFormatInfo.Spec == NumericFormat.Specifier.None)
                return fs.Append(input);

            var numFormatter = new IntNumericFormatter(input);
            return AppendFormat(ref fs, ref numFormatter, ref numericFormatInfo, ref arg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, char input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            return fs.Append(input);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, short input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var numericFormatInfo = NumericFormat.Parse(ref arg);
            if (numericFormatInfo.Spec == NumericFormat.Specifier.None)
                return fs.Append(input);

            var numFormatter = new IntNumericFormatter(input);
            return AppendFormat(ref fs, ref numFormatter, ref numericFormatInfo, ref arg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, ushort input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var numericFormatInfo = NumericFormat.Parse(ref arg);
            if (numericFormatInfo.Spec == NumericFormat.Specifier.None)
                return fs.Append(input);

            var numFormatter = new IntNumericFormatter(input);
            return AppendFormat(ref fs, ref numFormatter, ref numericFormatInfo, ref arg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, int input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var numericFormatInfo = NumericFormat.Parse(ref arg);
            if (numericFormatInfo.Spec == NumericFormat.Specifier.None)
                return fs.Append(input);

            var numFormatter = new IntNumericFormatter(input);
            return AppendFormat(ref fs, ref numFormatter, ref numericFormatInfo, ref arg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, uint input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var numericFormatInfo = NumericFormat.Parse(ref arg);
            if (numericFormatInfo.Spec == NumericFormat.Specifier.None)
                return fs.Append(input);

            var numFormatter = new IntNumericFormatter(input);
            return AppendFormat(ref fs, ref numFormatter, ref numericFormatInfo, ref arg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, long input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var numericFormatInfo = NumericFormat.Parse(ref arg);
            if (numericFormatInfo.Spec == NumericFormat.Specifier.None)
                return fs.Append(input);

            var numFormatter = new IntNumericFormatter(input);
            return AppendFormat(ref fs, ref numFormatter, ref numericFormatInfo, ref arg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, ulong input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            var numericFormatInfo = NumericFormat.Parse(ref arg);
            if (numericFormatInfo.Spec == NumericFormat.Specifier.None)
                return fs.Append(input);

            var numFormatter = new IntNumericFormatter(input);
            return AppendFormat(ref fs, ref numFormatter, ref numericFormatInfo, ref arg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, float input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            return fs.Append(input);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, double input, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            return Append(ref fs, (float)input, ref arg); // TODO: add double support to com.unity.collections
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T>(ref T fs, bool b, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            return fs.Append(b ? (FixedString32Bytes)"True" : (FixedString32Bytes)"False");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError AppendLowcase<T>(ref T fs, bool b, ref ArgumentInfo arg) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            return fs.Append(b ? (FixedString32Bytes)"true" : (FixedString32Bytes)"false");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormatError Append<T,T2>(ref T fs, in T2 input, ref ArgumentInfo arg)
            where T : unmanaged, INativeList<byte>, IUTF8Bytes
            where T2 : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            return fs.Append(input);
        }
    }

    public readonly struct IntNumericFormatter
    {
        public readonly ulong number;
        public readonly byte sign;
        public readonly byte bytes;

        public IntNumericFormatter(in long input)
        {
            bytes = 8;
            if (input < 0)
            {
                number = (ulong)input;
                sign = 1;
            }
            else
            {
                number = (ulong)input;
                sign = 0;
            }
        }
        public IntNumericFormatter(in ulong input)
        {
            bytes = 8;
            sign = 0;
            number = input;
        }

        public IntNumericFormatter(in int input)
        {
            bytes = 4;
            if (input < 0)
            {
                number = (ulong)(uint)input;
                sign = 1;
            }
            else
            {
                number = (ulong)input;
                sign = 0;
            }
        }
        public IntNumericFormatter(in uint input)
        {
            bytes = 4;
            sign = 0;
            number = (ulong)input;
        }

        public IntNumericFormatter(in short input)
        {
            bytes = 2;
            if (input < 0)
            {
                number = (ushort)input;
                sign = 1;
            }
            else
            {
                number = (ulong)input;
                sign = 0;
            }
        }
        public IntNumericFormatter(in ushort input)
        {
            bytes = 2;
            sign = 0;
            number = (ulong)input;
        }

        public IntNumericFormatter(in sbyte input)
        {
            bytes = 1;
            if (input < 0)
            {
                number = (byte)input;
                sign = 1;
            }
            else
            {
                number = (ulong)input;
                sign = 0;
            }
        }
        public IntNumericFormatter(in byte input)
        {
            bytes = 1;
            sign = 0;
            number = (ulong)input;
        }

        public bool IsNegative => sign == 1;

        public long SignedNumber()
        {
            Assert.IsTrue(IsNegative);
            var res = -(long)number;

            switch (bytes)
            {
                case 4: res &= 0xFFFFFFFF;
                    break;
                case 2: res &= 0xFFFF;
                    break;
                case 1: res &= 0xFF;
                    break;
            }
            if (res > 0)
                res = -res;
            return res;
        }
    }

    public readonly struct FloatNumericFormatter
    {
        enum FloatingPointFlags : byte
        {
            Normal,
            Infinity,
            NaN
        }

        public readonly float number;
        public readonly byte sign;


        public FloatNumericFormatter(in float input)
        {
            number = input;
            if(input < 0)
            {
                sign = 1;
            }
            else
            {
                sign = 0;
            }
        }
    }

    public static class FormatStringUtils
    {
        public static int LengthDec(in IntNumericFormatter integer, bool withSign)
        {
            if (integer.number == 0)
                return 1;

            var res = withSign ? integer.sign : 0;
            if (integer.IsNegative)
            {
                for (var n = integer.SignedNumber(); n != 0; n /= 10)
                    ++res;
            }
            else
            {
                for (var n = integer.number; n != 0; n /= 10)
                    ++res;
            }

            return res;
        }

        public static unsafe void AppendDec(in IntNumericFormatter integer, byte* dst, int neededLength)
        {
            if (neededLength <= 0) return;

            if (integer.number == 0)
            {
                dst[0] = (byte)'0';
                return;
            }

            var offset = neededLength;
            if (integer.IsNegative)
            {
                var num = integer.SignedNumber();
                do
                {
                    var digit = (byte)-(num % 10);
                    dst[--offset] = (byte)('0' + digit);
                    num /= 10;
                }
                while (num != 0);
            }
            else
            {
                var num = integer.number;
                do
                {
                    var digit = (byte)(num % 10);
                    dst[--offset] = (byte)('0' + digit);
                    num /= 10;
                }
                while (num != 0);
            }
            Assert.AreEqual(0, offset);
        }

        public static int LengthHex(in IntNumericFormatter integer, bool withSign)
        {
            if (integer.number == 0)
                return 1;

            ulong n = integer.number;

            var res = withSign ? integer.sign : 0;
            for (; n != 0; n >>= 4)
                ++res;
            return res;
        }

        public static unsafe void AppendHex(in IntNumericFormatter integer, byte* dst, int neededLength, bool upperCase)
        {
            if (neededLength <= 0) return;

            if (integer.number == 0)
            {
                dst[0] = (byte)'0';
                return;
            }

            FixedString32Bytes digitsLow = "0123456789abcdef";
            FixedString32Bytes digitsUp = "0123456789ABCDEF";
            ref var digits = ref upperCase ? ref digitsUp : ref digitsLow;

            var offset = neededLength;
            var num = integer.number;

            do
            {
                dst[--offset] = digits[(int)(num & 0xF)];
                num >>= 4;
            }
            while (num != 0);
            Assert.AreEqual(0, offset);
        }
    }
}
