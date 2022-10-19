using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SourceGenerator.Logging.Declarations
{
    public class OutputPaths
    {
        public const string SourceGenTextLoggerTypesFileName = "TextLoggerTypes_Gen.cs";
        public const string SourceGenTextLoggerUserTypesFileName = "TextLoggerUserTypes_Gen.cs";
        public const string SourceGenTextLoggerMethodsFileName = "TextLoggerMethods_Gen.cs";
        public const string SourceGenTextLoggerParserFileName = "TextLoggerParser_Gen.cs";
    }

    public class CompilerMessages
    {
        // Tuple compile error layout: Code, Description
        public static readonly (string, string)GeneralException = ("LSG0001", "Failed generating source file due to an unhandled exception.");
        public static readonly (string, string)ReferenceError = ("LSG0002", "Field cannot be a reference type; Must be value type.");
        public static readonly (string, string)OutputWriterError = ("LSG0003", "Field type doesn't have a default output writer and will be excluded from output; A custom write must be provided.");
        public static readonly (string, string)FieldValueTypeError = ("LSG0004", "Field cannot be confirmed as a value type.");
        public static readonly (string, string)EnumsUnsupportedError = ("LSG0005", "Enum fields aren't (yet) supported.");
        public static readonly (string, string)PublicFieldsWarning = ("LSG0006", "Fields must be publicly/internaly accessible.");
        public static readonly (string, string)UnsupportedFieldTypeError = ("LSG0007", "Field type '{0}' isn't supported.");
        public static readonly (string, string)InvalidWriteCall = ("LSG0009", "Log call was made without any arguments");
        public static readonly (string, string)FileWriteException = ("LSG0010", "Failed to write source file to disk.");
        public static readonly (string, string)UnsupportedFixedStringType = ("LSG0011", "Message is an unsupported FixedString type.");
        public static readonly (string, string)UnknownFixedStringType = ("LSG0012", "FixedString type is not one of the known variations.");
        public static readonly (string, string)InvalidArgument = ("LSG0013", "Message argument is invalid / unsupported");
        public static readonly (string, string)MessageLengthError = ("LSG0014", "Default message format length is too long");
        public static readonly (string, string)MessageFixedStringError = ("LSG0015", "Message text cannot be represented using a FixedString.");

        public static readonly (string, string)MissingDecoratePropertyName = ("LSG0018", "Log.Decorate(...) must have a name (string/FixedString) as a first argument, but is");
        public static readonly (string, string)TooMuchDecorateArguments = ("LSG0019", "Too much arguments for Decorate() call");
        public static readonly (string, string)MissingDecorateArguments = ("LSG0020", "Log.Decorate(...) must have more than one argument (name), also a value - an object or a function expected");
        public static readonly (string, string)ExpectedBoolIn3rdDecorateArgument = ("LSG0021", ".Decorate(string message, delegate Func, bool isBurstable) is expected, but 3rd argument is not bool, but is");
        public static readonly (string, string)CannotBeVoidError = ("LSG0022", "You're trying to log a 'void' type, this is not supported");

        public static readonly (string, string)MessageErrorFieldNameConflict = ("LSG0023", "Structure has several fields with the same name '{0}'");

        public static readonly (string, string)LiteralMessageGeneralError = ("LSGW0000", "General error in the message");

        public static readonly (string, string)LiteralMessageMissingArgForHole = ("LSGW0001", "Missing argument for hole '{0}', please add arguments to the function call");
        public static readonly (string, string)LiteralMessageMissingHoleForArg = ("LSGW0002", "You're providing more arguments than expected, please add more holes to the message, or remove extra arguments");
        public static readonly (string, string)LiteralMessageInvalidArgument = ("LSGW0003", "Hole is malformed and will be ignored. Check syntax here: https://messagetemplates.org/");
        public static readonly (string, string)LiteralMessageMissingIndexArg = ("LSGW0004", "Hole {{{0}}} is missing");
        public static readonly (string, string)LiteralMessageRepeatingNamedArg = ("LSGW0005", "Repeated named holes are not allowed, '{0}' was mentioned twice");
    }


    internal class EmitStrings
    {
        public const string BurstCompileAttr = "[BurstCompile]";

        /// <summary>
        /// Source emitted at the top of generated source files.
        /// CS8123 - The tuple element name 'XXX' is ignored because a different name or no name is specified by the target type
        /// CS0105 - The using directive for 'YYY' appeared previously in this namespace
        /// CS0436 - The type 'ZZZ' conflicts with the imported type 'QQQ'
        /// </summary>
        public const string SourceFileHeader = @"#pragma warning disable CS8123, CS0105, CS0436
#pragma warning disable 0168 // variable declared but not used.
#pragma warning disable 0219 // variable assigned but not used.
#pragma warning disable 0414 // private field assigned but not used.
#pragma warning disable 0436 // Type 'Log' conflicts with another one in case of InternalVisibleTo
";

        /// <summary>
        /// Source emitted at the bottom of generated source files.
        /// </summary>
        public const string SourceFileFooter = @"
#pragma warning restore CS8123, CS0105, CS0436
#pragma warning restore 0168 // variable declared but not used.
#pragma warning restore 0219 // variable assigned but not used.
#pragma warning restore 0414 // private field assigned but not used.
#pragma warning restore 0436 // Type 'Log' conflicts with another one in case of InternalVisibleTo
";

        public static readonly string[] StdIncludes = new[]
        {
            "System",
            "System.Runtime.InteropServices",
            "Unity.Burst",
            "Unity.Collections",
            "Unity.Collections.LowLevel.Unsafe",
            "Unity.Logging",
            "Unity.Logging.Internal",
            "Unity.Logging.Sinks"
        };

        static string m_SourceFileHeaderIncludes = null;
        public static string SourceFileHeaderIncludes
        {
            get
            {
                if (string.IsNullOrEmpty(m_SourceFileHeaderIncludes))
                {
                    m_SourceFileHeaderIncludes = GenerateIncludeHeader(new HashSet<string>(StdIncludes));
                }

                return m_SourceFileHeaderIncludes;
            }
        }

        public static string GenerateIncludeHeader(HashSet<string> stdIncludes)
        {
            var lines = stdIncludes.OrderBy(s => s).Select(s => $"using {s};");
            return string.Join(Environment.NewLine, lines);
        }
    }
}
