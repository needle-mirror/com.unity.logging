using Microsoft.CodeAnalysis;
using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    // Helper methods for processing FixedString types, in order for SourceGenerator to not take a dependency on DOTS Collections.
    public class FixedStringUtils
    {
        public struct FSType
        {
            public string Name;
            public int MaxLength;
            public bool IsValid => MaxLength > 0;
        }

        // Lookup table for each FixedString type name and max length
        // FixedStrings *should* be fairly settled by now, but any future changes will require an update to this table
        public readonly static FSType[] FSTypes =
        {
            new FSType
            {
                Name = "FixedString32Bytes",
                MaxLength = 29,
            },
            new FSType
            {
                Name = "FixedString64Bytes",
                MaxLength = 61,
            },
            new FSType
            {
                Name = "FixedString128Bytes",
                MaxLength = 125,
            },
            new FSType
            {
                Name = "FixedString512Bytes",
                MaxLength = 509,
            },
            new FSType
            {
                Name = "FixedString4096Bytes",
                MaxLength = 4093,
            },
        };

        public static FSType GetFSType(string typeName)
        {
            foreach (var fs in FSTypes)
            {
                if (fs.Name.ToLowerInvariant().Equals(typeName.ToLowerInvariant()))
                    return fs;
            }

            return new FSType();
        }

        public static bool IsFixedString(string typeName)
        {
            return GetFSType(typeName).IsValid;
        }

        public static bool IsSpecialSerializableType(ITypeSymbol Symbol)
        {
            switch (Symbol.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Char:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                    return true;
                default:
                    return GetFSType(Symbol.Name).IsValid; // it is fixed string
            }
        }

        public static FSType GetSmallestFixedStringTypeForMessage(string message, GeneratorExecutionContext context)
        {
            var length = System.Text.Encoding.UTF8.GetByteCount(message);

            foreach (var fs in FSTypes)
            {
                if (length <= fs.MaxLength)
                    return fs;
            }

            context.LogCompilerError(CompilerMessages.MessageFixedStringError);

            return default;
        }

        public static int CompareFixedStringMaxLengths(string leftFSTypeName, string rightFSTypeName)
        {
            var left = GetFSType(leftFSTypeName);
            if (!left.IsValid)
                throw new System.ArgumentException($"FixedString type name '{leftFSTypeName}' is invalid.");

            var right = GetFSType(rightFSTypeName);
            if (!right.IsValid)
                throw new System.ArgumentException($"FixedString type name '{rightFSTypeName}; is invalid.");

            return left.MaxLength.CompareTo(right.MaxLength);
        }
    }
}
