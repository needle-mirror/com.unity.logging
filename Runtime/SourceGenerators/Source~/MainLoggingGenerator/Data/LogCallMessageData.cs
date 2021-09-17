using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator.Logging
{
    public readonly struct LogCallMessageData : IEquatable<LogCallMessageData>
    {
        public readonly ITypeSymbol          Symbol;
        public readonly ExpressionSyntax     Expression;
        public readonly string               MessageType;
        public readonly string               LiteralValue;
        public readonly bool                 IsBurstable;
        public readonly FixedStringUtils.FSType FixedStringType;

        public bool IsLiteral => LiteralValue != null;
        public bool IsValid => Symbol != null;

        public LogCallMessageData(ITypeSymbol symbol, ExpressionSyntax expressionSyntax, string msgType, string litText, FixedStringUtils.FSType fixedStringType = default)
        {
            FixedStringType = fixedStringType;
            Symbol = symbol;
            MessageType = msgType;
            Expression = expressionSyntax;
            LiteralValue = litText;
            IsBurstable = FixedStringUtils.GetFSType(msgType).IsValid;
        }

        public static LogCallMessageData LiteralAsFixedString(ITypeSymbol symbol, ExpressionSyntax expressionSyntax, FixedStringUtils.FSType fixedStringType, string litText)
        {
            return new LogCallMessageData(symbol, expressionSyntax, fixedStringType.Name, litText, fixedStringType);
        }

        public bool Equals(LogCallMessageData other)
        {
            return MessageType == other.MessageType;
        }

        public override string ToString()
        {
            return $"{{[FormatMessage] type=<{MessageType}> literal=<{LiteralValue}> isBurstable=<{IsBurstable}> | expression=<{Expression}>}}";
        }

        public string GetParameterTypeForUser()
        {
            return MessageType == "object" ? "string" : MessageType;
        }
    }
}
