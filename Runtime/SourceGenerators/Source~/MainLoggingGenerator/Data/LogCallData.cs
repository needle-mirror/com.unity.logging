using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceGenerator.Logging
{
    public readonly struct LogCallData : IEquatable<LogCallData>
    {
        public readonly LogCallMessageData        MessageData;
        public readonly List<LogCallArgumentData> ArgumentData;

        public bool IsValid => MessageData.IsValid && ArgumentData != null;

        public bool IsBurstable { get; }

        public LogCallData(in LogCallMessageData msgData, IEnumerable<LogCallArgumentData> argData)
        {
            MessageData = msgData;
            ArgumentData = new List<LogCallArgumentData>(argData);

            IsBurstable = MessageData.IsBurstable && ArgumentData.All(arg => arg.IsBurstable);
        }

        public bool Equals(LogCallData other)
        {
            return MessageData.Equals(other.MessageData) && ArgumentData.SequenceEqual(other.ArgumentData);
        }

        public override string ToString()
        {
            if (ArgumentData.Count > 0)
                return $"{MessageData.ToString()} | {string.Join(", ", ArgumentData.Select(a => a.ToString()))}\n\t\tCall is {(IsBurstable ? "burstable" : "not compatible with Burst")}";
            return MessageData.ToString();
        }
    }
}
