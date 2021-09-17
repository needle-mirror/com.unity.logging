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

        public LogCallArgumentData(ITypeSymbol typeSymbol, string argType, string litValue, ExpressionSyntax expressionSyntax, FixedStringUtils.FSType fixedStringType = default)
        {
            FixedStringType = fixedStringType;
            Symbol = typeSymbol;
            ArgumentTypeName = argType;
            FullArgumentTypeName = Common.GetFullyQualifiedTypeNameFromSymbol(typeSymbol);
            UniqueId = Common.CreateMD5String(FullArgumentTypeName + typeSymbol.Name);
            Expression = expressionSyntax;
            GeneratedTypeName = $"{ArgumentTypeName}_{UniqueId}";

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

            IsBurstable = FixedStringType.IsValid || FixedStringUtils.IsSpecialSerializableType(Symbol) || Symbol.IsReferenceType == false;
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

        public string GetParameterTypeForUser(bool blittableOnly, int i)
        {
            if (IsSpecialSerializableType())
            {
                if (blittableOnly)
                {
                    if (Symbol.SpecialType is SpecialType.System_Char or SpecialType.System_Boolean)
                        return $"int iarg{i}";
                }

                return ArgumentTypeName + $" arg{i}";
            }

            return FullGeneratedTypeName + $" arg{i}";
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
