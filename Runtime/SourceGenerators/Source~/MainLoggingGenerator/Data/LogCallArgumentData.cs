using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator.Logging
{
    public readonly struct LogCallArgumentData : IEquatable<LogCallArgumentData>
    {
        public readonly ITypeSymbol Symbol;
        public readonly string      ArgumentTypeName;
        public readonly string      FullArgumentTypeName;
        public readonly string      UniqueId;
        public readonly string      GeneratedTypeName;
        public readonly string      FullGeneratedTypeName;
        public readonly string      LiteralValue;
        public readonly ExpressionSyntax Expression;
        public readonly FixedStringUtils.FSType FixedStringType;
        public bool IsLiteral => LiteralValue != null;
        public bool IsValid => Symbol != null;
        public bool IsBurstable { get; }
        public bool IsNonLiteralString => IsString && !IsLiteral;
        public bool IsString => Symbol != null && Symbol.SpecialType == SpecialType.System_String;
        public bool IsManagedString => IsString && (FixedStringType.IsValid == false && IsNativeOrUnsafeText == false);

        public readonly bool IsNativeText;
        public readonly bool IsUnsafeText;
        public readonly bool IsNativeOrUnsafeText;
        public bool IsEnum => Symbol != null && Symbol.TypeKind == TypeKind.Enum;
        public bool IsConvertibleToString =>
                        Symbol != null &&
                        Symbol.SpecialType == SpecialType.None &&
                        Symbol.TypeKind != TypeKind.Struct &&
                        FixedStringType.IsValid == false &&
                        (IsNativeOrUnsafeText || Symbol.IsReferenceType || Symbol.TypeKind == TypeKind.Enum);
        public bool IsReferenceType => FixedStringType.IsValid == false  && Symbol != null && Symbol.IsReferenceType;
        public bool ShouldUsePayloadHandle => (IsString || IsConvertibleToString) && IsNativeOrUnsafeText == false;

        public LogCallArgumentData(ITypeSymbol typeSymbol, string argType, string litValue, ExpressionSyntax expressionSyntax, FixedStringUtils.FSType fixedStringType = default)
        {
            FixedStringType = fixedStringType;
            Symbol = typeSymbol;
            ArgumentTypeName = argType;
            FullArgumentTypeName = Common.GetFullyQualifiedTypeNameFromSymbol(typeSymbol);
            UniqueId = Common.CreateMD5String(ArgumentTypeName + FullArgumentTypeName + typeSymbol.Name);
            Expression = expressionSyntax;

            if (typeSymbol.SpecialType == SpecialType.System_String)
            {
                GeneratedTypeName = ArgumentTypeName;
            }
            else
            {
                GeneratedTypeName = $"{ArgumentTypeName}_{UniqueId}";
            }

            // The TypeGenerator will place the generated struct in the same namespace as the user's type, unless
            // the user's struct was declared in the "root" namespace. We'd like to avoid adding generated types
            // to the global namespace so substitute Unity.Logging instead.

            var containingNamespace = Common.GetFullyQualifiedNameSpaceFromNamespaceSymbol(typeSymbol.ContainingNamespace);
            if (!string.IsNullOrWhiteSpace(containingNamespace))
            {
                FullGeneratedTypeName = "global::" + Common.GetFullyQualifiedNameSpaceFromNamespaceSymbol(typeSymbol.ContainingNamespace) + "." + GeneratedTypeName;
            }
            else
            {
                FullGeneratedTypeName = "global::Unity.Logging." + GeneratedTypeName;
            }

            LiteralValue = litValue;

            IsNativeOrUnsafeText = LiteralValue == null && Symbol != null && (Symbol.Name == "UnsafeText" || Symbol.Name == "NativeText");
            IsNativeText = IsNativeOrUnsafeText && Symbol.Name == "NativeText";
            IsUnsafeText = IsNativeOrUnsafeText && Symbol.Name == "UnsafeText";

            IsBurstable = IsNativeOrUnsafeText || FixedStringType.IsValid || FixedStringUtils.IsSpecialSerializableType(Symbol) ||
                            typeSymbol.SpecialType == SpecialType.System_String ||
                            typeSymbol.TypeKind == TypeKind.Struct ||
                            typeSymbol.TypeKind == TypeKind.Enum ||
                            typeSymbol.IsReferenceType;
        }

        public static LogCallArgumentData LiteralAsFixedString(ITypeSymbol typeSymbol, FixedStringUtils.FSType fixedString, string argText, ExpressionSyntax expression)
        {
            return new LogCallArgumentData(typeSymbol, fixedString.Name, argText, expression, fixedString);
        }

        public bool Equals(LogCallArgumentData other)
        {
            return UniqueId == other.UniqueId;
        }

        public bool IsSpecialSerializableType()
        {
            if (IsValid == false)
                return false;

            return FixedStringType.IsValid || FixedStringUtils.IsSpecialSerializableType(Symbol);
        }

        public override string ToString()
        {
            if (IsSpecialSerializableType())
                return $"{{[Argument. Special type] literal=<{LiteralValue}> isBurstable=<{IsBurstable}> | expression=<{Expression}>}}";
            return $"{{[Argument] genTypeName=<{GeneratedTypeName}> literal=<{LiteralValue}> isBurstable=<{IsBurstable}> | expression=<{Expression}>}}";
        }

        public (string type, string name) GetParameterTypeForUser(bool blittableOnly, int i)
        {
            if (IsSpecialSerializableType())
            {
                if (blittableOnly)
                {
                    if (Symbol.SpecialType is SpecialType.System_Char or SpecialType.System_Boolean)
                        return ("int", $"iarg{i}");
                }

                return (ArgumentTypeName, $"arg{i}");
            }

            if (Symbol.SpecialType == SpecialType.System_String)
            {
                if (blittableOnly)
                    return ("PayloadHandle", $"arg{i}");

                return (FullArgumentTypeName, $"arg{i}");
            }

            if (IsNativeOrUnsafeText)
            {
                if (blittableOnly)
                    return ("PayloadHandle", $"arg{i}");
                return (FullArgumentTypeName, $"arg{i}");
            }

            if (Symbol.TypeKind == TypeKind.Enum)
            {
                if (blittableOnly)
                    return ("PayloadHandle", $"arg{i}");
                return (FullArgumentTypeName, $"arg{i}");
            }

            if (Symbol.IsReferenceType)
            {
                if (blittableOnly)
                    return ("PayloadHandle", $"arg{i}");
                return (FullArgumentTypeName, $"arg{i}");
            }

            // blittable - use mirror struct
            if (blittableOnly)
                return (FullGeneratedTypeName, $"arg{i}");

            // visible to user - use original struct
            return (FullArgumentTypeName, $"arg{i}");
        }

        public string GetInvocationParam(int i)
        {
            if (Symbol.SpecialType == SpecialType.System_Char)
                return $"(int)arg{i}"; // casting to int

            if (Symbol.SpecialType == SpecialType.System_Boolean)
                return $"(arg{i} ? 1 : 0)"; // casting to int

            return $"arg{i}";
        }

        public string GetCastCode(int i)
        {
            if (IsSpecialSerializableType())
            {
                if (Symbol.SpecialType == SpecialType.System_Char)
                    return $"var arg{i} = (char)iarg{i};";

                if (Symbol.SpecialType == SpecialType.System_Boolean)
                    return $"var arg{i} = iarg{i} == 1;";
            }

            return "";
        }
    }
}
