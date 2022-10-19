using System.Collections.Generic;
using LoggingCommon;
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

        public static LogCallArgumentData Extract(ContextWrapper context, ExpressionSyntax expression, TypeInfo typeInfo, out string qualifiedName)
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
                context.LogCompilerErrorVoidType(expression.GetLocation());
            }
            // else if (typeSymbol.TypeKind == TypeKind.Pointer)
            // {
            //     data = LogCallArgumentData.Pointer(typeSymbol, expression);
            // }
            else if (typeSymbol.TypeKind == TypeKind.Enum)
            {
                // NOTE: Enums will be handled as convertible-to-string, and passed as string in PayloadBuffer
                data = new LogCallArgumentData(typeSymbol, typeSymbol.Name, literalValue, expression);
            }
            else if (LogMethodGenerator.IsValidFixedStringType(context, typeSymbol, out var fsType))
            {
                // FixedString types are supported directly and will be treated like primitive value types
                data = LogCallArgumentData.LiteralAsFixedString(typeSymbol, fsType, literalValue, expression);
            }
            else if (FixedStringUtils.IsNativeOrUnsafeText(typeSymbol.Name))
            {
                // Unsafe/Native Text collection type
                data = new LogCallArgumentData(typeSymbol, typeSymbol.Name, literalValue, expression);
            }
            else if (typeSymbol.IsValueType)
            {
                // structs will generate a mirror blittable struct
                data = new LogCallArgumentData(typeSymbol, typeSymbol.Name, literalValue, expression);
            }
            else if (string.IsNullOrEmpty(literalValue) == false)
            {
                data = new LogCallArgumentData(typeSymbol, "string", literalValue, expression);
            }
            else if (typeSymbol.SpecialType == SpecialType.System_String)
            {
                // String will be converted to UnsafeString in log method
                data = new LogCallArgumentData(typeSymbol, "string", literalValue, expression);
            }
            else if (typeSymbol.IsReferenceType)
            {
                // These will be treated as strings using ToString()
                data = new LogCallArgumentData(typeSymbol, typeSymbol.Name, literalValue, expression);
            }
            else
            {
                context.LogCompilerErrorInvalidArgument(typeInfo, expression);
            }

            return data;
        }
    }
}
