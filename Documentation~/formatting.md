# Composite Formatting and Format Specifiers

The Logging package allows the user to pass in strings alongside variables to perform formatting in a manner similar to `string.Format`.

## Composite Formatting

Composite Formatting should behave according to the specification laid out in this language-neutral document: [Message Templates](https://messagetemplates.org/). The closest implementation reference it adheres to is the C# specification. For more information, see [Composite Formatting](https://docs.microsoft.com/en-us/dotnet/standard/base-types/composite-formatting).

Logging statements roughly have this method signature (for between `0` and `n` *optional* additional arguments):

```
void StatementType<T>(in T msg, T0 arg0, ... Tn argn) where T : unmanaged, INativeList<byte>, IUTF8Bytes

// `StatementType` is a Logging statement, such as Log.Debug or Log.Info
// `T where T : unmanaged, INativeList<byte>, IUTF8Bytes` is most often in practice a Unity.Collections.FixedString[N]Bytes type, such as FixedString32Bytes or FixedString512Bytes
```

`arg0`, `arg1`, etc. are all optional. `T0`, `T1`, etc. must be serializable types.

For example:

```
Log.Info("Basic composite formatting: {0}", 123);
```

Here, the *fixed text* portion of `msg` is `"Basic composite formatting: "`. `{0}` is the only *format item* in this example, though users may specify more than one. In this example, the *format item* `{0}` corresponds to the argument passed in to `T0 arg0`, or the `int` 123.

The above example would print:

```
Basic composite formatting: 123
```

A `msg` string can contain any number of *format items* as substrings. A *format item* is a substring of text that matches this format:

* **Required:** Starts with a `{` open-curly-brace character
* **Required:** Immediately followed by a non-negative integer, the format item's *index*. An index of `0` corresponds to arg0, an index of `1` corresponds to arg1, etc.
* *Optional:* A `,` character immediately followed by *alignment*.
* *Optional:* A `:` character, immediately followed by a *format string*. A *format string* can currently **only** be either a *standard format specifier* string or a *custom format specifiers* string. **Only a subset of specifiers are currently supported.** If they are not listed in this document, they are not currently supported.
    * *Either:* A *standard numeric format specifier* string,
    * *Or:* A string of one or more *custom numeric format specifiers*.
* **Required:** Ends with a `}` closed-curly-brace character

In short, with optional components surrounded by square brackets (`[]`):

`{`*index*[`,`*alignment*][`:`*format string*]`}`

In practice, a *format item* can combine these components into statements that look like this: `{0,15:D12}`, where `0` is the *index*, `15` is the *alignment*, and `D12` is the *format string*. Both the *alignment* and *format string* are optional, e.g. `{0,15}`
and `{0:D12}` omit either optional component, and `{0}` omits both.

### Index

The required *index* of a *format item* is a number starting from 0 that identifies the *format item* to match the corresponding argument in the list of arguments.

If the contents of `msg` contain one or more *format items*, the *index* of any given *format* item corresponds to the sequence of arguments passed in. An index of `0` corresponds to `arg0`, an index of `1` corresponds to `arg1`, etc.

```
Log.Info("Multiple arguments: {0}, {1}, {2}", 12.50, 511, 32);
```

This would result in the log message result of:

```
Multiple arguments: 12.50, 511, 32
```

The string does not need to contain *format items* with *indices* in the same order as the list of arguments. Multiple format items may also specify the same *index* and reference the same argument; this is useful to apply different types of formatting to the same argument.

For example:

```
Log.Info("Out of order and multiple format items are okay: {2}, {0}, {1}, {2}, {0}", 12.50, 511, 32);
```

Should output:

```
Out of order and multiple format items are okay: 32, 12.50, 511, 32, 12.50
```

### Alignment

The optional *alignment* component of a *format item* is a signed integer that indicates the formmated string's preferred field width. The use of a `,` after the *index* signifies the start of the *alignment* component.

If the formatted string is less characters in length than the absolute value of the *alignment*, spaces are used to pad the remaining unused characters.

The sign of the *alignment* component indicates whether the formatted string is left-aligned or right-aligned: a *positive* alignment is *right-aligned* and adds padding to the left, and a *negative* alignment is *left-aligned* and adds padding to the right.

If the formatted string's length exceeds the preferred field width, the value of *alignment* is ignored, no padding is added. The formatted string results are not truncated when this occurs.

```
// First format item is left-aligned 20 spaces, second one is right-aligned 5 spaces:
Log.Info("Item #{0,-20}|{1,5}|", 1, 3125);

// First format item is left-aligned 20 spaces, second one's resulting formatted string exceeds alignment of 5:
Log.Info("Item #{0,-20}|{1,5}|", 2, 7583125);
```

```
Item #1                   | 3125|
Item #2                   |7583125|
```

### Format String

The optional *format string* corresponds to a format specifier for the appropriate type.

`com.unity.logging` currently supports a subset of format specifier types defined by the C# specification. For numeric types, it currently supports a subset of standard format specifiers and numeric format specifiers.

## Format Specifiers

`com.unity.logging` currently supports two format specifier types and a subset of their functionality:

* Standard format specifiers
* Custom format specifiers

Support for additional format specifiers may be added in the future, prioritized by their ease to implement and how useful they are.

## Standard Numeric Format Specifiers

For more information, see [Standard Numeric Format Strings](https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings).

A standard numeric format string takes the form `[format specifier][precision specifier]`, where the required *format specifier* specifies the type of number format, and the optional *precision specifier* is an unsigned integer that specifies the minimum amount of digits present in the string. The case of the *format specifier* might or might not be relevant, and the exact meaning of it is specific to the *format specifier* in question.

Examples of *format items* where their *format strings* parse as *standard numeric format strings* are `{0:D12}`, `{0:x}`, `{0,5:d3}`. In the first one, `D` is the *format specifier*, and it has a *precision specifier* of `12`.

Supported *standard format specifiers*:

* `Dd`: Integers with optional negative sign. Integral types only.
* `Xx`: Hexadecimal string representation of integer. Integral types only.

### Decimal format specifier (D)

For more information, see [Decimal Format Specifier (D)](https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#decimal-format-specifier-d).

`Dd` is the *decimal format specifier.* This converts a number to a string of base-ten digits, prefixed by a `-` if the number is negative. Its case is not relevant; both `D` and `d` yield identical results. The *precision specifier* indicates the minimum number of digits in the resulting formatted string: if the string representation of the number is smaller in length than the specified precision (not counting the `-`), the string is padded with zeroes to the left of the original number to fit.

Example:
```
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

### Hexadecimal format specifier (X)

For more information, see [Hexadecimal Format Specifier (X)](https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#hexadecimal-format-specifier-x).

`Xx` is the *hexadecimal format specifier*. This converts a numer to its string representation written in base-sixteen digits. The case of the *hexadecimal format specifier* indicates whether to use upper- or lowercase letters when appropriate for hexadecimal digits; e.g. `X` will produce `ABCDEF` and `x` will produce `abcdef`. The *precision specifier* indicates the minimum number of digits in the resulting formatted string: if the string representation of the number is smaller in length than the specified precision, the string is padded with zeroes to the left of the original number to fit.

Example:
```
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

## Custom Numeric Format Specifiers

For more information, see [Custom Numeric Format Strings](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings).

A *custom numeric format string* contains one or more *custom numeric specifiers*. Whereas *standard numeric specifiers* were mutually exclusive from each other, a *custom numeric format string* can contain many permutations and combinations of *custom numeric format specifiers.*

`{0:#}` is a trivial example that contains a single *custom specifier* of `#`. `{0:#####}` is an example that contains five `#` specifiers in a row. `{0:##0,0.00#}` combines all four currently supported specifiers in the same string.

**These *custom format specifiers* are supported by `com.unity.logging`**:

* `0`: Zero-placeholder symbol.
* `#`: Digit-placeholder symbol.
* `.`: Decimal separator symbol.
* `,`: Group separator symbol or number scaling specifier, depending on the context of where it was placed.
* `\`: Escape character symbol.
* Pairs of `'` or `"` that surround other characters, e.g. `'string'` or `"string"`: Literal string delimiters.

**These next four specifiers are not yet supported and they will not fully function, but unless escaped or enclosed inside of a literal string specifier, they will not be treated as literal characters:**

* `%`: Percentage placeholder.
* `‰`: Per mille placeholder.
* `;`: Section specifier.
* `E0`, `e0`, `E+0`, `e+0`, `E-0`, `e-0`: Exponential specifiers.

* Any character **not otherwise specified above** is instead treated as a *literal character* and is copied directly to the result string.

### The "0" Custom Specifier

For more information, see [The "0" Custom Specifier](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the-0-custom-specifier).

`0` is a zero-placeholder symbol: if the value being formatted has a digit in the position where zero appears in the format string, the digit is copied to the string; otherwise, a zero appears in the result string. The position of the leftmost zero before the decimal point and the rightmost zero after the decimal point determines the range of digits that are always present in the result string. 

Example:
```
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

### The "#" Custom Specifier

For more information, see [The "#" Custom Specifier](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the--custom-specifier).

`#` is a digit-placeholder symbol: if the value being formatted has a digit in the position where the `#` symbol appears in the format string, the digit is copied to the string; otherwise, nothing is stored in that position. The position of the leftmost zero before the decimal point and the rightmost zero after the decimal point determines the range of digits that are always present in the result string. The `#` specifier *never* displays a zero that is not a significant digit, even if zero is the only digit in the string: it will only do so if the zero is a significant digit in the number being displayed.

Example:
```
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

To return a result string in which absent digits or leading zeroes are replaced by spaces, use the *alignment component* from the *composite formatting* feature and specify a field width, as the following example illustrates:

Example:
```
int value = 324;
Log.Info("The value is: |{0,5:#}|", value);
```

Output:
```
The value is: |  324|
```

### The "." Custom Specifier

For more information, see [The "." Custom Specifier](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the--custom-specifier-1).

The "." custom format specifier inserts a localized decimal separator into the result string. The first period in the format string determines the location of the decimal separator in the formatted value; any additional periods are ignored.

Example:
```
int value = 86000;
Log.Info("{0:#.00}", value);
```

Output:
```
86000.00
```

### The "," Custom Specifier

For more information, see [The "." Custom Specifier](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the--custom-specifier-2).

The "," character serves as both a *group separator* and a *number scaling specifier*, depending on where in the *custom format string* that it's placed, relative to the number's decimal point.

* Group separator: If one or more commas are specified between two digit placeholders (0 or #) that format the integral digits of a number, a group separator character is inserted between each number group in the integral part of the output.
* Number scaling specifier: If one or more commas are specified immediately to the left of the explicit or implicit decimal point, the number to be formatted is divided by 1000 for each comma. For example, if the string "0,," is used to format the number 100 million, the output is "100".

Example:
```
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

### Character Literals

For more information, see [Character Literals](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#character-literals).

The "#", "0", ".", ",", "%", and "‰" symbols in a *format string* are interpreted as *format specifiers* rather than as *literal characters*. Depending on their position in a *custom format string*, the uppercase and lowercase "E" as well as the + and - symbols may also be interpreted as *format specifiers*. The "\" character is also special and serves as an *escape character* for the next character in the string.

It is possible to indicate that a group of characters are meant to be interpreted as *literal characters* by enclosing them in matching pairs of `'` or `"` characters.

Example:
```
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

### The "\" Escape Character

For more information, see [The "\" Escape Character](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-numeric-format-strings#the--escape-character).

The "#", "0", ".", ",", "%", and "‰" symbols in a format string are interpreted as format specifiers rather than as literal characters. Depending on their position in a custom format string, the uppercase and lowercase "E" as well as the + and - symbols may also be interpreted as format specifiers.

To prevent a character from being interpreted as a *format specifier*, you can precede it with a backslash, which is the escape character. The escape character signifies that the following character is a character literal that should be included in the result string unchanged.

To include a backslash in a result string, you must escape it with another backslash (`\\`). Additionally, when attempting to include a backslash in a *format specifier* in string literal form, as shown in the example below, you must also double-escape it in that situation.

Example:
```
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

## Other notes

Localization for numeric formats is not currently supported; e.g. decimal separators will currently be the `.` character, group separators will be the `,` character, group separator sizes are always 3 digits, etc.