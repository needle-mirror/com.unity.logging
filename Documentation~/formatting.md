# Composite formatting and format specifiers

You can pass in strings alongside variables to perform formatting in a manner similar to `string.Format`.

## Composite formatting

Composite formatting behaves according to the specification in this language-neutral document: [Message Templates](https://messagetemplates.org/). The closest implementation reference it adheres to is the C# specification. For more information, see Microsoft's documentation on [Composite Formatting](https://docs.microsoft.com/en-us/dotnet/standard/base-types/composite-formatting).

Logging statements have this method signature (for between `0` and `n` *optional* additional arguments):

```c#
void StatementType<T>(in T msg, T0 arg0, ... Tn argn) where T : unmanaged, INativeList<byte>, IUTF8Bytes

// `StatementType` is a Logging statement, such as Log.Debug or Log.Info
// `T where T : unmanaged, INativeList<byte>, IUTF8Bytes` is most often in practice a Unity.Collections.FixedString[N]Bytes type, such as FixedString32Bytes or FixedString512Bytes
```

`arg0`, `arg1`, etc. are all optional. `T0`, `T1`, etc. must be serializable types.

For example:

```c#
Log.Info("Basic composite formatting: {0}", 123);
```

Here, the **fixed text** portion of `msg` is `"Basic composite formatting: "`. `{0}` is the only **format item** in this example, you can specify more than one. In this example, the format item `{0}` corresponds to the argument passed in to `T0 arg0`, or the `int` 123.

The above example prints:

```
Basic composite formatting: 123
```

A `msg` string can contain any number of format items as substrings. A format item is a substring of text that must match this format:

* Starts with a `{` open-curly-brace character
* Immediately followed by a non-negative integer, the format item's **index**. An index of `0` corresponds to arg0, an index of `1` corresponds to arg1, etc.
* Optional: A `,` character followed by alignment.
* Optional: A `:` character, followed by a format string. A format string can only be either a standard format specifier string or a custom format specifiers string. Only the following subset of specifiers are supported:
    * Either a standard numeric format specifier string,
    * Or, a string of one or more custom numeric format specifiers.
* Ends with a `}` closed-curly-brace character

This example shows the format with optional components surrounded by square brackets (`[]`):

```
{`*index*[`,`*alignment*][`:`*format string*]`}
```

In practice, a format item combines these components into statements that look like this: `{0,15:D12}`, where `0` is the index, `15` is the **alignment**, and `D12` is the **format string**. Both the alignment and format string are optional. For example, `{0,15}`and `{0:D12}` omit either optional component, and `{0}` omits both.

### Index

The required index of a format item is a number starting from 0 that identifies the format item to match the corresponding argument in the list of arguments.

If the contents of `msg` contain one or more format items, the index of any given format item corresponds to the sequence of arguments passed in. An index of `0` corresponds to `arg0`, an index of `1` corresponds to `arg1`, etc. For example:

```
Log.Info("Multiple arguments: {0}, {1}, {2}", 12.50, 511, 32);
```

Results in the log message result of:

```
Multiple arguments: 12.50, 511, 32
```

The string doesn't need to contain format items with indices in the same order as the list of arguments. Multiple format items can specify the same index and reference in the same argument: this is useful to apply different types of formatting to the same argument.

For example:

```
Log.Info("Out of order and multiple format items are okay: {2}, {0}, {1}, {2}, {0}", 12.50, 511, 32);
```

Should output:

```
Out of order and multiple format items are okay: 32, 12.50, 511, 32, 12.50
```

### Alignment

The optional alignment component of a format item is a signed integer that indicates the formatted string's preferred field width. The use of a `,` after the index signifies the start of the alignment component.

If the formatted string has less characters in length than the absolute value of the alignment, spaces are used to pad the remaining unused characters.

The sign of the alignment component indicates whether the formatted string is left-aligned or right-aligned: a **positive** alignment is right-aligned and adds padding to the left. A **negative** alignment is left-aligned and adds padding to the right.

If the formatted string's length exceeds the preferred field width, the value of the alignment is ignored, and  no padding is added. The formatted string results aren't truncated when this happens.

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

### Format string

The optional **format string** corresponds to a format specifier for the appropriate type.

The Logging package supports a subset of format specifier types defined by the C# specification. For numeric types, it supports a subset of standard format specifiers and numeric format specifiers.

## Further information

* [Format specifiers](format-specifiers.md)