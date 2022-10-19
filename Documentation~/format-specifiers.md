## Format specifiers

The Logging package supports two format specifier types and a subset of their functionality:

* Standard format specifiers
* Custom format specifiers

## Standard numeric format specifiers

A standard numeric format string takes the form `[format specifier][precision specifier]`, where the required **format specifier** specifies the type of number format, and the optional precision specifier is an unsigned integer that specifies the minimum amount of digits present in the string. The case of the format specifier might or might not be relevant, and the exact meaning of it is specific to the format specifier in question.

Examples of format items where their format strings parse as standard numeric format strings are `{0:D12}`, `{0:x}`, `{0,5:d3}`. In the first example, `D` is the format specifier, and it has a precision specifier of `12`.

The supported standard format specifiers are:

* `Dd`: Integers with optional negative sign. Integral types only.
* `Xx`: Hexadecimal string representation of integer. Integral types only.

For more information, see Microsoft's documentation on [Standard Numeric Format Strings](https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings).

### Decimal format specifier (D)

`Dd` is the **decimal format specifier.** This converts a number to a string of base-ten digits, prefixed by a `-` if the number is negative. It isn't case-sensitive: both `D` and `d` yield identical results. 

The **precision specifier** indicates the minimum number of digits in the resulting formatted string: if the string representation of the number is smaller in length than the specified precision (not counting the `-`), the string is padded with zeroes to the left of the original number to fit.

For example:

```c#
int value;

value = 12345;
Log.Info("{0:D}", value);
Log.Info("{0:D8}", value);

value = -12345;
Log.Info("{0:D}", value);
Log.Info("{0:D8}", value);
```

Output:
```
12345
00012345
-12345
-00012345
```

For more information, see Microsoft's documentation on [Decimal Format Specifier (D)](https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#decimal-format-specifier-d).

### Hexadecimal format specifier (X)

`Xx` is the **hexadecimal format specifier**. This converts a number to its string representation written in base-sixteen digits. The case of the hexadecimal format specifier indicates whether to use upper or lowercase letters when appropriate for hexadecimal digits. For example, `X` produces `ABCDEF` and `x` produces `abcdef`. 

The **precision specifier** indicates the minimum number of digits in the resulting formatted string: if the string representation of the number is smaller in length than the specified precision, the string is padded with zeroes to the left of the original number to fit.

For example:

```c#
int value;

value = 0x2045e;
Log.Info("0:x", value);
Log.Info("0:X", value);
Log.Info("0:X8", value);

value = 123456789;
Log.Info("0:X", value);
Log.Info("0:X2", value);
```

Output:
```
2045e
2045E
0002045E
75BCD15
75BCD15
```

For more information, see Microsoft's documentation on [Hexadecimal Format Specifier (X)](https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#hexadecimal-format-specifier-x).

## Custom numeric format specifiers

A **custom numeric format string** contains one or more custom numeric specifiers. Whereas standard numeric specifiers are mutually exclusive from each other, a custom numeric format string can contain lots of permutations and combinations of custom numeric format specifiers.

`{0:#}` is an example that contains a single **custom specifier** of `#`. `{0:#####}` is an example that contains five `#` specifiers in a row. `{0:##0,0.00#}` combines all four supported specifiers in the same string.

The Logging package supports the following custom format specifiers:

* `0`: Zero-placeholder symbol.
* `#`: Digit-placeholder symbol.
* `.`: Decimal separator symbol.
* `,`: Group separator symbol or number scaling specifier, depending on the context of where it was placed.
* `\`: Escape character symbol.
* Pairs of `'` or `"` that surround other characters. For example, `'string'` or `"string"`: Literal string delimiters.

These next specifiers aren't supported and they don't fully function. Unless escaped or enclosed inside of a literal string specifier, they aren't treated as literal characters:

* `%`: Percentage placeholder.
* `‰`: Per mille placeholder.
* `;`: Section specifier.
* `E0`, `e0`, `E+0`, `e+0`, `E-0`, `e-0`: Exponential specifiers.

Any character not otherwise specified above is instead treated as a literal character and is copied directly to the result string.

For more information, see Microsoft's documentation on [Custom Numeric Format Strings](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings).

### The "0" custom specifier

`0` is a zero-placeholder symbol. If the value being formatted has a digit in the position where zero appears in the format string, the digit is copied to the string; otherwise, a zero appears in the result string. The position of the leftmost zero before the decimal point and the rightmost zero after the decimal point determines the range of digits that are always present in the result string. 

Example:

```c#
int value = 123;
Log.Info("{0:00000}", value);

value = 1234567890;
Log.Info("{0:0}", value);
```

Output:
```
00123
1234567890
```

For more information, see Microsoft's documentation on [The "0" Custom Specifier](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the-0-custom-specifier).

### The "#" custom specifier

`#` is a digit-placeholder symbol. If the value being formatted has a digit in the position where the `#` symbol appears in the format string, the digit is copied to the string; otherwise, nothing is stored in that position. 

The position of the leftmost zero before the decimal point and the rightmost zero after the decimal point determines the range of digits that are always present in the result string. The `#` specifier never displays a zero that is not a significant digit, even if zero is the only digit in the string. It only does so if the zero is a significant digit in the number being displayed.

Example:
```c#
int value = 123;
Log.Info("{0:#####}", value));

value = 123456;
Log.Info("{0:[##-##-##]}", value);

value = 1234567890;
Log.Info("{0:#}", value);
Log.Info("{0:(###) ###-####}", value);
```

Output:
```
123
[12-34-56]
1234567890
(123) 456-7890
```

To return a result string where absent digits or leading zeroes are replaced by spaces, use the **alignment component** from the composite formatting*feature and specify a field width, as the following example illustrates:

Example:

```c#
int value = 324;
Log.Info("The value is: |{0,5:#}|", value);
```

Output:
```
The value is: |  324|
```

For more information, see Microsoft's documentation on [The "#" Custom Specifier](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the--custom-specifier).

### The "." custom specifier

The `.` custom format specifier inserts a localized decimal separator into the result string. The first period in the format string determines the location of the decimal separator in the formatted value. Any additional periods are ignored.

Example:

```c#
int value = 86000;
Log.Info("{0:#.00}", value);
```

Output:
```
86000.00
```

For more information, see Microsoft's documentation on [The "." Custom Specifier](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the--custom-specifier-1).

### The "," custom specifier

The `,` character serves as both a group separator and a number scaling specifier, depending on where in the custom format string that it's placed, relative to the number's decimal point.

* **Group separator**: If one or more commas are specified between two digit placeholders (`0` or `#`) that format the integral digits of a number, a group separator character is inserted between each number group in the integral part of the output.
* **Number scaling specifier**: If one or more commas are specified immediately to the left of the explicit or implicit decimal point, the number to be formatted is divided by 1,000 for each comma. For example, if the string `"0,,"` is used to format the number 100 million, the output is `"100"`.

Example:
```c#
int value;

value = 86000;
Log.Info("{0:#,#.0}", value);

value = 1234567890;
Log.Info("{0:#,#.0}", value);
Log.Info("{0:#,.0}", value);
Log.Info("{0:#,,.0}, value");
Log.Info("{0:#,#,.0}, value");
Log.Info("{0:#,#,,.0}, value");
Log.Info("{0:#,#,}, value");
Log.Info("{0:#,#,,}, value");
```

Output:
```
86,000.0
1,234,567,890.0
1234567.9
1234.6
1,234,567.9
1,234.6
1,234,568
1,235
```

For more information, see Microsoft's documentation on [The "." Custom Specifier](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the--custom-specifier-2).

### Character literals

The `#`, `0`, `.`, `,`, `%`, and `‰` symbols in a format string are interpreted as format specifiers rather than as literal characters. Depending on their position in a custom format string, the uppercase and lowercase `E` and the `+` and `-` symbols may also be interpreted as format specifiers. The "\" character is also special and serves as an escape character for the next character in the string.

To indicate that a group of characters are meant to be interpreted as literal characters, enclose them in matching pairs of `'` or `"` characters.

Example:
```c#
int value;

value = 123;
Log.Info("{0:|'000'|000}", value);
Log.Info("{0:|'#0.,%‰E+0'| #}", value);

```

Output:
```
|000|123
|#0.,%‰E+0| 123
```

To prevent these characters from being interpreted as a format specifier, you can precede it with a backslash (`\`), which is the escape character. The escape character signifies that the following character is a character literal that should be included in the result string unchanged.

To include a backslash in a result string, you must escape it with another backslash (`\\`). Additionally, when attempting to include a backslash in a format specifier in string literal form, as shown in the example below, you must also double-escape it in that situation.

Example:
```c#
int value;

value = 123;
Log.Info("{0:\\##.0}", value);

value = 0;
Log.Info("{0:\\##.0}", value);
Log.Info("{0:\\#0.0}", value);
```

Output:
```
#123.0
#.0
#0.0
```

For more information, see Microsoft's documentation on [Character Literals](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#character-literals) and [The "\" Escape Character](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the--escape-character).

## Other notes

Localization for numeric formats isn't currently supported. For example, decimal separators are the `.` character, group separators are the `,` character, and group separator sizes are always 3 digits.
