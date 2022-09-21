using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceGenerator.Logging
{
    public readonly struct LogCallMessageData : IEquatable<LogCallMessageData>
    {
        public readonly ITypeSymbol          Symbol;
        public readonly ExpressionSyntax     Expression;
        public readonly string               MessageType;
        public readonly string               LiteralValue;
        public readonly string               FullArgumentTypeName;
        public readonly FixedStringUtils.FSType FixedStringType;
        public readonly bool IsNativeText;
        public readonly bool IsUnsafeText;
        public readonly bool IsNativeOrUnsafeText;
        public readonly bool Omitted;
        public bool IsLiteral => LiteralValue != null;
        public bool IsValid => Symbol != null;
        public bool IsNonLiteralString => IsString && !IsLiteral;
        public bool IsString => Symbol != null && Symbol.SpecialType == SpecialType.System_String;
        public bool IsManagedString => IsString && (FixedStringType.IsValid == false && FixedStringUtils.IsNativeOrUnsafeText(MessageType) == false);
        public bool IsReferenceType => Symbol != null && Symbol.IsReferenceType;
        public bool ShouldUsePayloadHandle => FixedStringType.IsValid == false && IsReferenceType;

        public LogCallMessageData(ITypeSymbol symbol, ExpressionSyntax expressionSyntax, string msgType, string litText, FixedStringUtils.FSType fixedStringType = default)
        {
            FixedStringType = fixedStringType;
            Symbol = symbol;
            MessageType = msgType;
            Expression = expressionSyntax;
            LiteralValue = litText;

            IsNativeOrUnsafeText = LiteralValue == null && Symbol != null && (Symbol.Name == "UnsafeText" || Symbol.Name == "NativeText");
            IsNativeText = IsNativeOrUnsafeText && Symbol.Name == "NativeText";
            IsUnsafeText = IsNativeOrUnsafeText && Symbol.Name == "UnsafeText";

            FullArgumentTypeName = Common.GetFullyQualifiedTypeNameFromSymbol(symbol);
            Omitted = false;
        }

        // Omitted message data
        public LogCallMessageData(INamedTypeSymbol symbol, string litText)
        {
            FixedStringType = default;
            Symbol = symbol;
            MessageType = "string";
            Expression = null;

            IsNativeOrUnsafeText = false;
            IsNativeText = false;
            IsUnsafeText = false;

            FullArgumentTypeName = Common.GetFullyQualifiedTypeNameFromSymbol(symbol);

            LiteralValue = litText;
            Omitted = true;
        }

        public static LogCallMessageData FixedString(ITypeSymbol symbol, ExpressionSyntax expressionSyntax, FixedStringUtils.FSType fixedStringType)
        {
            return new LogCallMessageData(symbol, expressionSyntax, fixedStringType.Name, null, fixedStringType);
        }

        public const string DefaultFixedString32Literal = "Default";
        public static LogCallMessageData FixedString32(GeneratorExecutionContext context)
        {
            var str = context.Compilation.GetSpecialType(SpecialType.System_String);
            return new LogCallMessageData(str, null, FixedStringUtils.Smallest.Name, DefaultFixedString32Literal, FixedStringUtils.Smallest);
        }

        public static LogCallMessageData LiteralAsFixedString(ITypeSymbol symbol, ExpressionSyntax expressionSyntax, string messageText)
        {
            return new LogCallMessageData(symbol, expressionSyntax, "string", messageText);
        }

        public static LogCallMessageData OmittedLiteralAsFixedString(INamedTypeSymbol symbol, string messageText)
        {
            return new LogCallMessageData(symbol, messageText);
        }

        public bool Equals(LogCallMessageData other)
        {
            return Omitted == other.Omitted && MessageType == other.MessageType;
        }

        public override string ToString()
        {
            return $"{{[FormatMessage] type=<{MessageType}> literal=<{LiteralValue}> | expression=<{Expression}>}}";
        }

        public string GetParameterTypeForUser(bool blittable = false)
        {
            if (blittable)
            {
                if (ShouldUsePayloadHandle)
                    return "PayloadHandle";

                if (FixedStringType.IsValid == false && Symbol.IsReferenceType)
                    return FullArgumentTypeName;
            }

            if (MessageType == "object")
            {
                if (FixedStringType.IsValid)
                    return MessageType;
                return FullArgumentTypeName;
            }
            return MessageType;
        }

        public Location GetLocation(GeneratorExecutionContext context, int segmentOffset, int segmentLength)
        {
            var loc = Expression?.GetLocation();

            if (loc != null)
            {
                if (IsLiteral && loc.IsInSource && loc.SourceSpan.Length >= segmentOffset)
                {
                    var newLoc = loc;
                    try
                    {
                        var start = loc.SourceSpan.Start + segmentOffset+1;
                        loc = Location.Create(loc.SourceTree, TextSpan.FromBounds(start, start + segmentLength));
                    }
                    catch
                    {
                        loc = newLoc;
                    }
                }
            }
            else
            {
                loc = context.Compilation.SyntaxTrees.First().GetRoot().GetLocation();
            }

            return loc;
        }
    }
}
