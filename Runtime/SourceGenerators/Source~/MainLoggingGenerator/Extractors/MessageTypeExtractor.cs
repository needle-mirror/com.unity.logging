using System;
using LoggingCommon;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator.Logging;

namespace MainLoggingGenerator.Extractors
{
    public static class MessageTypeExtractor
    {
        public static (LogCallMessageData data, bool messageOmitted) Extract(ContextWrapper m_Context, ExpressionSyntax expression, TypeInfo typeInfo)
        {
            // If the actual type is a regular C# string then use this type instead of the "Converted" type.
            // Basically, C# strings may be implicitly converted to FixedString, which we don't want. If the argument
            // is coming from a managed string source we have to keep that string type, otherwise the string contents
            // might get truncated when copied into a FixedString

            var typeSymbol = typeInfo.ConvertedType;
            if (typeInfo.Type?.Name == "String")
                typeSymbol = typeInfo.Type;

            LogCallMessageData data = default;
            var messageOmitted = false;

            if (expression is InterpolatedStringExpressionSyntax interpolatedString)
            {
                if (interpolatedString.Contents.Count == 1 && interpolatedString.Contents[0] is InterpolatedStringTextSyntax justText)
                {
                    // this is interpolated string, but without any holes. use it as a string literal
                    var messageText = justText.ToString();
                    data = LogCallMessageData.LiteralAsFixedString(typeSymbol, expression, messageText);
                }
                else
                {
                    data = new LogCallMessageData(typeSymbol, expression, "string", null);
                }
            }
            else if (expression.Kind() == SyntaxKind.StringLiteralExpression)
            {
                // Typically the log message is a string literal which can be extracted from the syntax tree
                var messageText = (expression as LiteralExpressionSyntax).Token.ValueText;
                data = LogCallMessageData.LiteralAsFixedString(typeSymbol, expression, messageText);
            }
            else if (typeSymbol != null)
            {
                if (typeSymbol.IsValueType && LogMethodGenerator.IsValidFixedStringType(m_Context, typeSymbol, out var fsType))
                {
                    // Message parameter is already a FixedString and can be used as-is
                    data = LogCallMessageData.FixedString(typeSymbol, expression, fsType);
                }
                else if (typeSymbol.IsValueType && FixedStringUtils.IsNativeOrUnsafeText(typeSymbol.Name))
                {
                    // UnsafeText or NativeText as a message
                    data = new LogCallMessageData(typeSymbol, expression, typeSymbol.Name, null);
                }
                else if (typeSymbol.SpecialType == SpecialType.System_String)
                {
                    // Message parameter is a managed string and will be converted to a Burstable copy in payload buffer
                    data = new LogCallMessageData(typeSymbol, expression, "string", null);
                }
                else
                {
                    // Assuming message has been omitted so just use a placeholder value for now
                    data = new LogCallMessageData(typeSymbol, expression, "string", null);
                    messageOmitted = true;
                }
            }
            else
            {
                m_Context.LogCompilerErrorInvalidArgument(typeInfo, expression);

                data = new LogCallMessageData();
            }

            return (data, messageOmitted);
        }
    }
}
