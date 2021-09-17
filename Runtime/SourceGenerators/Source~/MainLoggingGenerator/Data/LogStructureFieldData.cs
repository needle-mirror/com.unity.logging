using Microsoft.CodeAnalysis;

namespace SourceGenerator.Logging
{
    public readonly struct LogStructureFieldData
    {
        public readonly IFieldSymbol    Symbol;
        public readonly string          FieldTypeName;
        public readonly string          FieldName;
        public readonly string          Formatter;
        public readonly bool            IsGeneratedType;
        public readonly bool            IsStatic;

        public bool IsValid => Symbol != null;
        public bool IsTaggedForLogging => !string.IsNullOrEmpty(Formatter);

        public LogStructureFieldData(IFieldSymbol fieldSymbol, string generatedTypeName)
        {
            Symbol = fieldSymbol;
            FieldName = fieldSymbol.Name;
            IsGeneratedType = !string.IsNullOrWhiteSpace(generatedTypeName);
            FieldTypeName = IsGeneratedType ? generatedTypeName : Common.GetFullyQualifiedTypeNameFromSymbol(fieldSymbol.Type);
            Formatter = "";
            IsStatic = fieldSymbol.IsStatic;
        }
    }
}
