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
    }
}
