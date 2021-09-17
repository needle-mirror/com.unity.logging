using System.Linq;
using Microsoft.CodeAnalysis;
using SourceGenerator.Logging;
using SourceGenerator.Logging.Declarations;

namespace MainLoggingGenerator.Extractors
{
    public static class StructFieldExtractor
    {
        public static LogStructureFieldData Extract(GeneratorExecutionContext m_Context,
            LogTypesGenerator gen, IFieldSymbol field, LogCallArgumentData argument)
        {
            LogStructureFieldData data = default;

            // Only value types fields are currently supported; reference types are ignored
            if (field.Type.IsRefLikeType)
            {
                m_Context.LogCompilerWarningReferenceType(argument.Expression);
                return default;
            }

            // // Make sure it's actually a value type
            // // Docs: ...for an unconstrained type parameter, IsReferenceType and IsValueType will both return false.
            // if (!field.Type.IsValueType)
            // {
            //     m_Context.LogCompilerErrorCannotUseField(field, argument.Expression.GetLocation());
            //     return default;
            // }

            if (field.Type.TypeKind == TypeKind.Enum)
            {
                m_Context.LogCompilerWarningEnumUnsupportedAsField(argument.Expression, field);
                return default;
            }

            if (field.DeclaredAccessibility != Accessibility.Public)
            {
                m_Context.LogCompilerWarningNotPublic(argument.Expression);
                return default;
            }

            if (!field.CanBeReferencedByName)
            {
                // Silently ignore; this should always be true for fields and so the check may be unnecessary
                return default;
            }

            // Handle field according to special type:
            // - Primitive value types (int, DateTime, etc.) form the base types and don't need any other processing
            // - Other structure types must be processed to generate their own LogStructureDefinitionData
            // - All other types are invalid and those fields will be ignored
            switch (field.Type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_Char:
                case SpecialType.System_DateTime:
                case SpecialType.System_Decimal:
                case SpecialType.System_Double:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_IntPtr:
                case SpecialType.System_SByte:
                case SpecialType.System_Single:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                case SpecialType.System_UIntPtr:

                    data = new LogStructureFieldData(field, "");
                    break;

                // A value type without any SpecialType should be a struct
                // Attempt to generate or retrieve struct data for this field (recursively) and use that to initialize the field,
                // but if that fails (unable to generate meaningful struct) then simply use the field's type as-is.
                //
                // NOTE: Fields with an "opaque" struct type must be tagged with a Formatter in order to actually output text.
                case SpecialType.None:

                    if (LogTypesGenerator.ExtractLogCallStructureInstance(gen, field.Type, default, out var structData))
                    {
                        data = new LogStructureFieldData(field, structData.GeneratedTypeName);
                    }
                    else
                    {
                        // TODO: Need to check if the field and/or type has a custom Formatter or not, if it doesn't and we don't provide a default Formatter for
                        // the type, then exclude it from the generated struct data. There's no point paying the memory cost for data that won't contribute any log output.
                        data = new LogStructureFieldData(field, "");
                    }
                    break;

                default:
                {
                    m_Context.LogCompilerErrorUnsupportedField(field, argument.Expression.GetLocation());
                    return default;
                }
            }

            return data;
        }
    }
}
