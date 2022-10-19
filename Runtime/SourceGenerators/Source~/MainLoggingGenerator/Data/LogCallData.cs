using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceGenerator.Logging
{
    public readonly struct LogCallData : IEquatable<LogCallData>
    {
        public readonly LogCallMessageData        MessageData;
        public readonly List<LogCallArgumentData> ArgumentData;
        public readonly bool ShouldBeMarkedUnsafe;

        public bool IsValid => MessageData.IsValid && ArgumentData != null;

        // NOTE: UnsafeText / NativeText shouldn't use PayloadHandle for messages, since BuildMessage can handle them, like FixedString
        public bool ShouldUsePayloadHandleForMessage => MessageData.ShouldUsePayloadHandle;
        public bool HasLiteralStringMessage => MessageData.IsLiteral;

        public LogCallData(in LogCallMessageData msgData, IEnumerable<LogCallArgumentData> argData)
        {
            MessageData = msgData;
            ArgumentData = new List<LogCallArgumentData>(argData);

            ShouldBeMarkedUnsafe = MessageData.IsUnsafe || ArgumentData.Any(a => a.IsUnsafe);
        }

        public bool Equals(LogCallData other)
        {
            return MessageData.Equals(other.MessageData) && ArgumentData.SequenceEqual(other.ArgumentData);
        }

        public override string ToString()
        {
            if (MessageData.Omitted)
            {
                if (ArgumentData.Count > 0)
                    return $"message omitted as {MessageData.LiteralValue}, ({string.Join(", ", ArgumentData.Select(a => a.ArgumentTypeName + " arg"))})";
                return $"Message was omitted, without arguments. Please report a bug";
            }

            if (ArgumentData.Count > 0)
                return $"({MessageData.MessageType} msg, {string.Join(", ", ArgumentData.Select(a => a.ArgumentTypeName + " arg"))})";
            return $"({MessageData.MessageType} msg)";
        }
    }
}
