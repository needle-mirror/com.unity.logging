using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator.Logging;
using SourceGenerator.Logging.Declarations;

namespace MainLoggingGenerator.Extractors
{
    public static class ArgumentTypeExtractor
    {
        private static bool IsSyntaxKindValidLiteralExpression(ExpressionSyntax expression)
        {
            switch (expression.Kind())
            {
                case SyntaxKind.NumericLiteralExpression:
                case SyntaxKind.CharacterLiteralExpression:
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                case SyntaxKind.TrueLiteralExpression:
                case SyntaxKind.NullLiteralExpression:
                    return true;
            }

            return false;
        }

        public static LogCallArgumentData Extract(GeneratorExecutionContext m_Context, ExpressionSyntax expression, TypeInfo typeInfo, out string qualifiedName)
        {
            var typeSymbol = typeInfo.Type;

            var data = default(LogCallArgumentData);
            qualifiedName = "";

            if (typeSymbol is null or IErrorTypeSymbol)
            {
                return data; // compilation error
            }

            // TODO: Find a way to use ConvertedType on log arguments without picking up the generated types.
            //
            // ITypeInfo.ConvertedType returns the type of the argument after it's undergone implicit conversion. Since we generate
            // implicit operators to automatically copy data from the user's struct type to our own internal types, ConvertedType
            // can resolve to our internal types if these generated structs are already present in the syntax tree. By just using Type
            // we avoid this problem, but it means other implicit conversions will be ignored as well; unsure if this is a problem or not.

            // First check if this argument type has already been used, and if so return existing data
            qualifiedName = Common.GetFullyQualifiedTypeNameFromSymbol(typeSymbol);

            string literalValue = null;
            if (IsSyntaxKindValidLiteralExpression(expression) && expression is LiteralExpressionSyntax exp)
            {
                literalValue = exp.Token.ValueText;
            }

            if (typeSymbol.SpecialType == SpecialType.System_Void)
            {
                m_Context.LogCompilerErrorVoidType(expression.GetLocation());
            }
            else if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                m_Context.LogCompilerErrorEnumUnsupported(expression.GetLocation());

                // NOTE: We can't call ToString() on the enum variable (not Burstable) but instead we could create a lookup table
                // within the generated struct mapping values to their string equivalents, extracted from the syntax tree.
            }
            else if (LogMethodGenerator.IsValidFixedStringType(m_Context, typeSymbol, out var fsType))
            {
                // FixedString types are supported directly and will be treated like primitive value types
                data = LogCallArgumentData.LiteralAsFixedString(typeSymbol, fsType, literalValue, expression);
            }
            else if (typeSymbol.IsValueType)
            {
                // Other non-struct value types *should* work, e.g. int or float, we'll generate a struct with a single field for the argument value
                data = new LogCallArgumentData(typeSymbol, typeSymbol.Name, literalValue, expression);
            }
            else if (string.IsNullOrEmpty(literalValue) == false)
            {
                // A literal (string or otherwise) may be used as a structure argument; will be passed via FixedString struct

                var fixedString = FixedStringUtils.GetSmallestFixedStringTypeForMessage(literalValue, m_Context);

                if (fixedString.IsValid)
                {
                    qualifiedName = "Unity.Collections." + fixedString;

                    data = LogCallArgumentData.LiteralAsFixedString(typeSymbol, fixedString, literalValue, expression);
                }
            }
            else if (typeSymbol.TypeKind == TypeKind.Structure)
            {
                // Typically log argument are structures (structs)
                data = new LogCallArgumentData(typeSymbol, typeSymbol.Name, literalValue, expression);
            }
            else
            {
                m_Context.LogCompilerErrorInvalidArgument(typeInfo, expression);
            }

            return data;
        }
    }
}
