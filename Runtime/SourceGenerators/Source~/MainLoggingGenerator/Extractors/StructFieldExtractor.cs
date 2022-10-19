using System;
using System.Linq;
using LoggingCommon;
using Microsoft.CodeAnalysis;
using SourceGenerator.Logging;
using SourceGenerator.Logging.Declarations;

namespace MainLoggingGenerator.Extractors
{
    public static class StructFieldExtractor
    {
        static bool ShouldIgnore(ISymbol symbol, out string rename)
        {
            rename = null;
            var attributes = symbol.GetAttributes();
            foreach (var a in attributes)
            {
                var s = a.AttributeClass?.ToString();

                if (s.Contains(".NonSerializedAttribute") || s == "Unity.Logging.NotLogged")
                    return true;

                if (s == "Unity.Logging.LogWithName")
                {
                    try
                    {
                        if (a.ConstructorArguments.Length > 0)
                            rename = (string)a.ConstructorArguments[0].Value;
                    }
                    catch
                    {
                        rename = null;
                    }
                }

            }

            return false;
        }

        public static LogStructureFieldData Extract(ContextWrapper ctx, LogTypesGenerator gen, IFieldSymbol field, LogCallArgumentData argument)
        {
            // we skip [NonSerialized] fields
            if (ShouldIgnore(field, out var rename))
                return default;

            // Only value types fields are currently supported; reference types are ignored
            if (field.Type.IsRefLikeType)
            {
                ctx.LogCompilerWarningReferenceType(argument.Expression);
                return default;
            }

            if (field.Type.TypeKind == TypeKind.Enum)
            {
                ctx.LogCompilerWarningEnumUnsupportedAsField(argument.Expression, field);
                return default;
            }

            bool isProperty = field.AssociatedSymbol != null;

            // from https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.ifieldsymbol.associatedsymbol?view=roslyn-dotnet-4.2.0
            // a backing variable for an automatically generated property or a field-like event, returns that property/event
            // basically means <getSetterProperty>k__BackingField for public FixedString32Bytes getSetterProperty {get; set;}
            if (isProperty)
            {
                if (field.AssociatedSymbol is IPropertySymbol prop)
                {
                    if (prop.GetMethod == null)
                        return default; // cannot get the value, ignore property silently

                    if (ShouldIgnore(prop, out var rename2))
                        return default;

                    rename ??= rename2;

                    if (prop.GetMethod.DeclaredAccessibility != Accessibility.Public && prop.GetMethod.DeclaredAccessibility != Accessibility.Internal)
                    {
                        // Silently ignore private/protected properties
                        return default;
                    }
                }
            }
            else
            {
                if (field.DeclaredAccessibility != Accessibility.Public && field.DeclaredAccessibility != Accessibility.Internal)
                {
                    // Silently ignore private/protected fields
                    return default;
                }

                if (!field.CanBeReferencedByName)
                {
                    // Silently ignore; this should always be true for fields and so the check may be unnecessary
                    return default;
                }
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

                    return LogStructureFieldData.SpecialType(field, rename);

                case SpecialType.System_String:
                    return  LogStructureFieldData.SystemString(field, rename);

                // A value type without any SpecialType should be a struct
                // Attempt to generate or retrieve struct data for this field (recursively) and use that to initialize the field,
                // but if that fails (unable to generate meaningful struct) then simply use the field's type as-is.
                //
                // NOTE: Fields with an "opaque" struct type must be tagged with a Formatter in order to actually output text.
                case SpecialType.None:

                    if (LogTypesGenerator.ExtractLogCallStructureInstance(ctx, gen, field.Type, default, out var structData))
                    {
                        return LogStructureFieldData.MirrorStruct(field, structData.GeneratedTypeName, rename);
                    }

                    // TODO: Need to check if the field and/or type has a custom Formatter or not, if it doesn't and we don't provide a default Formatter for
                    // the type, then exclude it from the generated struct data. There's no point paying the memory cost for data that won't contribute any log output.
                    return new LogStructureFieldData(field, "", rename);

                default:
                {
                    ctx.LogCompilerErrorUnsupportedField(field, argument.Expression.GetLocation());
                    return default;
                }
            }
        }
    }
}
