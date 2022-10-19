using System.Text;
using LoggingCommon;
using Microsoft.CodeAnalysis;

namespace SourceGenerator.Logging
{
    public readonly struct LogStructureFieldData
    {
        public readonly IFieldSymbol    Symbol;
        public readonly string          FieldTypeName;
        public readonly string          FieldName;
        public readonly bool            IsGeneratedType;
        public readonly bool            IsStatic;
        public readonly bool            NeedsPayload;

        public readonly string          PropertyNameForSerialization; // name in json for instance. user can set via LogWithName or FieldName is used

        public bool IsValid => Symbol != null;
        public bool IsUnsafe => IsValid && Symbol.Type.TypeKind == TypeKind.Pointer;

        public LogStructureFieldData(IFieldSymbol fieldSymbol, string generatedTypeName, string rename)
        {
            if (fieldSymbol.AssociatedSymbol is IPropertySymbol prop)
            {
                // property
                FieldName = prop.Name;
            }
            else
            {
                FieldName = fieldSymbol.Name;
            }

            PropertyNameForSerialization = string.IsNullOrEmpty(rename) ? FieldName : rename;

            Symbol = fieldSymbol;
            IsGeneratedType = !string.IsNullOrWhiteSpace(generatedTypeName);
            FieldTypeName = IsGeneratedType ? generatedTypeName : Common.GetFullyQualifiedTypeNameFromSymbol(fieldSymbol.Type);
            IsStatic = fieldSymbol.IsStatic;

            NeedsPayload = Symbol.Type.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String || FixedStringUtils.IsNativeOrUnsafeText(fieldSymbol.Type.Name);
        }

        public static LogStructureFieldData SpecialType(IFieldSymbol field, string rename)
        {
            return new LogStructureFieldData(field, "", rename);
        }

        public static LogStructureFieldData SystemString(IFieldSymbol field, string rename)
        {
            return new LogStructureFieldData(field, "", rename);
        }

        public static LogStructureFieldData MirrorStruct(IFieldSymbol field, string structDataGeneratedTypeName, string rename)
        {
            return new LogStructureFieldData(field, structDataGeneratedTypeName, rename);
        }

        public StringBuilder AppendFieldDeclaration(StringBuilder sb)
        {
            sb.Append($@"
        // Field name {PropertyNameForSerialization}");

            // attribute
            if (Symbol.Type.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_Boolean)
            {
                return sb.Append($@"
        public byte {FieldName};");
            }

            if (Symbol.Type.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_Char)
            {
                return sb.Append($@"
        public int {FieldName};");
            }

            if (Symbol.Type.TypeKind == TypeKind.Pointer)
            {
                return sb.Append($@"
        public IntPtr {FieldName};");
            }

            if (NeedsPayload)
            {
                return sb.Append($@"
        public PayloadHandle {FieldName};");
            }

            return sb.Append($@"
        public {FieldTypeName} {FieldName};");
        }

        public StringBuilder AppendFieldWriter(ContextWrapper ctx, StringBuilder sb)
        {
            if (IsGeneratedType)
            {
                // If field is also a generated struct type, then simply call it's own Write method
                return sb.Append($@"
            success = formatter.WriteChild(ref output, ""{PropertyNameForSerialization}"", ref {FieldName}, ref memAllocator, ref currArgSlot, depth + 1) && success;");
            }

            {
                // Generate default writers for each primitive field in the struct
                switch (Symbol.Type.Name)
                {
                    // These types are supported directly by UnsafeText and don't require a cast
                    case "Char":
                        // cast needed, because it is stored as int (blittable), char is not blittable
                        return sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{PropertyNameForSerialization}"", (char){FieldName}, ref currArgSlot) && success;");

                    case "Byte":
                    case "SByte":
                    case "Int16":
                    case "UInt16":
                    case "Int32":
                    case "UInt32":
                    case "Int64":
                    case "UInt64":
                    case "Single":
                        return sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{PropertyNameForSerialization}"", {FieldName}, ref currArgSlot) && success;");

                    // These types require and explicit cast to avoid compile errors
                    case "Double":
                        return sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{PropertyNameForSerialization}"", {FieldName}, ref currArgSlot) && success;");

                    case "Boolean":
                        return sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{PropertyNameForSerialization}"", {FieldName} != 0, ref currArgSlot) && success;");

                    // TODO: These should be formatted without throwing out the precision
                    case "Decimal":
                        return sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{PropertyNameForSerialization}"", {FieldName}, ref currArgSlot) && success;");

                    // TODO: These should be formatted into hex strings, e.g. 0x0012345
                    case "IntPtr":
                    case "UIntPtr":
                        return sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{PropertyNameForSerialization}"", {FieldName}, ref currArgSlot) && success;");

                    case "String":
                    case "UnsafeText":
                    case "NativeText":
                        return sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{PropertyNameForSerialization}"", {FieldName}, ref memAllocator, ref currArgSlot) && success;");

                    default:

                        var warningNeeded = true;
                        var fieldTypeName = Symbol.Type.Name;

                        if (fieldTypeName.StartsWith("FixedString"))
                        {
                            return sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{PropertyNameForSerialization}"", {FieldName}, ref currArgSlot) && success;");
                        }

                        if (Symbol.Type.TypeKind == TypeKind.Pointer)
                        {
                            return sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{PropertyNameForSerialization}"", {FieldName}, ref currArgSlot) && success;");
                        }

                        if (warningNeeded)
                        {
                            ctx.LogCompilerWarningOutputWriter(Symbol);
                        }
                        break;
                }
            }

            return sb;
        }

        public StringBuilder AppendFieldConvert(StringBuilder sb, string middle, string fieldName)
        {
            if (Symbol.Type.Name == "String")
            {
                return sb.Append(@$"
                {FieldName} = Unity.Logging.Builder.CopyStringToPayloadBuffer({middle}.{fieldName}, ref memAllocator, prependLength: true, deferredRelease: true),
");
            }

            if (Symbol.Type.Name == "NativeText" || Symbol.Type.Name == "UnsafeText")
            {
                return sb.Append(@$"
                {FieldName} = Unity.Logging.Builder.CopyCollectionStringToPayloadBuffer({middle}.{fieldName}, ref memAllocator, prependLength: true, deferredRelease: true),
");
            }

            if (Symbol.Type.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_Boolean)
            {
                return sb.Append(@$"
                {FieldName} = (byte)({middle}.{fieldName} ? 1 : 0),
");
            }

            if (Symbol.Type.TypeKind == TypeKind.Pointer)
            {
                return sb.Append(@$"
                {FieldName} = new IntPtr({middle}.{fieldName}),
");
            }

            return sb.Append(@$"
                {FieldName} = {middle}.{fieldName},
");
        }
    }
}
