using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using LoggingCommon;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    class LogTypesEmitter
    {
        private LogTypesEmitter()
        {
        }

        public static StringBuilder Emit(in GeneratorExecutionContext context, in LogStructureTypesData structData)
        {
            using var _ = new Profiler.Auto("LogTypesEmitter.Emit");

            var emitter = new LogTypesEmitter
            {
                m_Context = context,
                m_StructData = structData,
            };

            var namespaceList = structData.StructTypes.Select(x => x.ContainingNamespace).Distinct().ToImmutableArray();

            var sb = new StringBuilder();
            sb.Append(EmitSourceFileHeader(namespaceList));
            sb.Append(emitter.EmitTextLogStructureTypeDefinitions(namespaceList));

            return sb;
        }

        private static StringBuilder EmitSourceFileHeader(ImmutableArray<string> namespaceList)
        {
            var sb = new StringBuilder();
            sb.Append(EmitStrings.SourceFileHeader);
            foreach (var name in namespaceList)
            {
                sb.AppendLine($"using {name};");
            }
            sb.AppendLine(EmitStrings.SourceFileFooter);
            return sb;
        }

        private StringBuilder EmitTextLogStructureTypeDefinitions(ImmutableArray<string> namespaceList)
        {
            using var _ = new Profiler.Auto("LogTypesEmitter.EmitTextLogStructureTypeDefinitions");

            var sb = new StringBuilder();

            foreach (var name in namespaceList)
            {
                sb.AppendFormat(EmitStrings.TextLoggerTypesNamespaceEnclosure,
                    name,
                    EmitTextLogStructureTypeDefinitionsForNamespace(name)
                );
            }

            return sb;
        }

        private StringBuilder EmitTextLogStructureTypeDefinitionsForNamespace(string currNamespace)
        {
            var sb = new StringBuilder();

            var structList = m_StructData.StructTypes.Where(x => x.ContainingNamespace == currNamespace);
            foreach (var inst in structList)
            {
                sb.AppendFormat(EmitStrings.TextLoggerTypesDefinition,
                    inst.GeneratedTypeName,
                    EmitTextLogStructureContent(inst)
                );
            }

            return sb;
        }

        private StringBuilder EmitTextLogStructureContent(in LogStructureDefinitionData currStruct)
        {
            var sb = new StringBuilder();

            sb.Append(EmitTextLogStructureIdField(currStruct));
            sb.Append(EmitTextLogStructureFields(currStruct));
            sb.Append(EmitTextLogStructureWriter(currStruct));
            sb.Append(EmitTextLogStructureTypeId(currStruct));
            sb.Append(EmitTextLogStructureConversion(currStruct));

            return sb;
        }

        private StringBuilder EmitTextLogStructureIdField(in LogStructureDefinitionData currStruct)
        {
            var sb = new StringBuilder();

            // Generated structs are identified by a Type ID integer value.
            // This value is set in a const field within the struct declaration, but the value must
            // also be assigned to a field for a given instance of this struct type. This value is
            // serialized with the rest of the fields and used by the log Sink to identify message data.
            //
            // NOTE: The TypeID field must be the first field in the struct; the Lister message parser will
            // read the TypeId as the first 32-bit value in the struct's byte buffer.

            sb.AppendFormat(EmitStrings.TextLoggerTypesFieldMember,
                "ulong",
                m_StructData.StructIdFieldName
            );

            return sb;
        }

        private static StringBuilder EmitTextLogStructureFields(in LogStructureDefinitionData currStruct)
        {
            var sb = new StringBuilder();

            foreach (var f in currStruct.FieldData)
            {
                // attribute
                if (f.Symbol.Type.SpecialType == SpecialType.System_Boolean)
                    sb.AppendFormat(EmitStrings.TextLoggerTypesFieldMemberAttributeForBool); // "[MarshalAs(UnmanagedType.U1)]" to make burst happy

                // declaration
                if (f.Symbol.Type.SpecialType == SpecialType.System_Char)
                {
                    sb.AppendFormat(EmitStrings.TextLoggerTypesFieldMember, "int", f.FieldName); // char is not blittable
                }
                else
                {
                    sb.AppendFormat(EmitStrings.TextLoggerTypesFieldMember, f.FieldTypeName, f.FieldName);
                }
            }

            return sb;
        }

        private static StringBuilder EmitTextLogStructureTypeId(in LogStructureDefinitionData currStruct)
        {
            var sb = new StringBuilder();

            sb.AppendFormat(EmitStrings.TextLoggerTypesIdValue,
                currStruct.GeneratedTypeName,
                currStruct.TypeId.ToString());

            return sb;
        }

        private StringBuilder EmitTextLogStructureWriter(in LogStructureDefinitionData currStruct)
        {
            var sb = new StringBuilder();

            // TODO: This outputs a simple "default" formatting: structure data enclosed in '[' ']', fields comma-delimited, field names omitted.
            // However currently there's no way to customize formatted, and eventually need a way for user's to specify a custom format (function)
            // via the TextLogger attribute.

            sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterDefinition,
                EmitTextLogStructureWriterContent(currStruct),
                "[",
                "]");

            return sb;
        }

        private StringBuilder EmitTextLogStructureWriterContent(in LogStructureDefinitionData currStruct)
        {
            var sb = new StringBuilder();

            var first = true;
            foreach (var f in currStruct.FieldData)
            {
                sb.Append(EmitTextLogStructureWriteExpression(currStruct, f, first));
                first = false;
            }

            return sb;
        }

        private StringBuilder EmitTextLogStructureWriteExpression(in LogStructureDefinitionData currStruct, in LogStructureFieldData currField, bool isFirst)
        {
            var sb = new StringBuilder();

            if (!isFirst)
                sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWritePrimitiveDelimiter, ", ");

            if (currField.IsGeneratedType)
            {
                // If field is also a generated struct type, then simply call it's own Write method
                sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterInvokeOnStructField,
                    currField.FieldName);
            }
            else
            {
                // Generate default writers for each primitive field in the struct
                switch (currField.Symbol.Type.Name)
                {
                    // These types are supported directly by UnsafeText and don't require a cast
                    case "Char":
                        // cast needed, because it is stored as int (blittable), char is not blittable
                        sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWritePrimitiveFieldWithDefaultFormat, currField.FieldName, "(char)");
                        break;

                    case "Byte":
                    case "Int32":
                    case "UInt32":
                    case "Int64":
                    case "UInt64":
                    case "Single":
                        sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWritePrimitiveFieldWithDefaultFormat, currField.FieldName, "");
                        break;

                    // These types require and explicit cast to avoid compile errors
                    case "Double":
                        sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWritePrimitiveFieldWithDefaultFormat, currField.FieldName, "(float)");
                        break;
                    case "Int16":
                        sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWritePrimitiveFieldWithDefaultFormat, currField.FieldName, "(int)");
                        break;
                    case "UInt16":
                        sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWritePrimitiveFieldWithDefaultFormat, currField.FieldName, "(uint)");
                        break;
                    case "SByte":
                        sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWritePrimitiveFieldWithDefaultFormat, currField.FieldName, "(int)");
                        break;

                    case "Boolean":
                        sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWriteBooleanFieldWithDefaultFormat, currField.FieldName);
                        break;

                    // TODO: These should be formatted without throwing out the precision
                    case "Decimal":
                        sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWritePrimitiveFieldWithDefaultFormat, currField.FieldName, "(float)");
                        break;

                    // TODO: These should be formatted into hex strings, e.g. 0x0012345
                    case "IntPtr":
                    case "UIntPtr":
                        sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWritePrimitiveFieldWithDefaultFormat, currField.FieldName, "(ulong)");
                        break;

                    default:

                        // Attempt to generate formatted output for special case types, e.g. FixedString
                        if (!HandleSpecialCaseWriteExpressions(currStruct, currField, sb))
                        {
                            m_Context.LogCompilerWarning(CompilerMessages.OutputWriterError);
                        }
                        break;
                }
            }

            return sb;
        }

        private static bool HandleSpecialCaseWriteExpressions(in LogStructureDefinitionData currStruct, in LogStructureFieldData currField, StringBuilder sb)
        {
            // FixedStrings are supported by UnsafeText and can be appended directly
            if (currField.Symbol.Type.Name.StartsWith("FixedString"))
            {
                sb.AppendFormat(EmitStrings.TextLoggerTypesFormatterWritePrimitiveFieldWithDefaultFormat, currField.FieldName, "");
                return true;
            }

            return false;
        }

        private StringBuilder EmitTextLogStructureConversion(LogStructureDefinitionData currStruct)
        {
            var sb = new StringBuilder();

            sb.AppendFormat(EmitStrings.TextLoggerImplicitConversionDefinition,
                currStruct.GeneratedTypeName,
                currStruct.FullTypeName,
                EmitTypeConversionFieldCopy(currStruct));

            return sb;
        }

        private StringBuilder EmitTypeConversionFieldCopy(in LogStructureDefinitionData typeInstance)
        {
            var sb = new StringBuilder();

            // Initialize the TypeId field with const type value of the containing struct
            var constTypeFieldName = typeInstance.GeneratedTypeName + "_TypeIdValue";

            sb.AppendFormat(EmitStrings.TextLoggerImplicitConversionTypeIdCopy,
                m_StructData.StructIdFieldName,
                typeInstance.GeneratedTypeName);

            if (typeInstance.Symbol.IsTupleType)
            {
                // In case of tuple we're removing field names and using Item1, Item2, etc instead
                // This is to workaround issue when user has
                // Log.Info("{0}", (1, 2));
                // Log.Info("{0}", (b:1, c:2));
                // ambiguous error
                var i = 0;
                foreach (var field in typeInstance.FieldData)
                {
                    ++i;
                    sb.AppendFormat(EmitStrings.TextLoggerImplicitConversionFieldCopy,
                        field.FieldName, field.IsStatic ? $"{field.FieldTypeName}" : "arg", $"Item{i}");
                }
            }
            else
            {
                // Copy over each field from the user's struct
                foreach (var field in typeInstance.FieldData)
                {
                    sb.AppendFormat(EmitStrings.TextLoggerImplicitConversionFieldCopy,
                        field.FieldName, field.IsStatic ? $"{field.FieldTypeName}" : "arg", field.FieldName);
                }
            }

            return sb;
        }

        private GeneratorExecutionContext           m_Context;
        private LogStructureTypesData         m_StructData;
    }
}
