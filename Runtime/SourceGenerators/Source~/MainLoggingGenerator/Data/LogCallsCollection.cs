using System.Collections.Generic;

namespace SourceGenerator.Logging
{
    public readonly struct LogCallsCollection
    {
        public readonly Dictionary<LogCallKind, List<LogCallData>>         InvokeInstances;
        public readonly Dictionary<LogCallKind, List<LogCallArgumentData>> UniqueArgumentData;
        public bool IsValid => InvokeInstances != null && InvokeInstances.Count > 0;

        public LogCallsCollection(Dictionary<LogCallKind, List<LogCallData>> instances, Dictionary<LogCallKind, List<LogCallArgumentData>> uniqueArgs)
        {
            InvokeInstances = instances;
            UniqueArgumentData = uniqueArgs;
        }
    }
}
