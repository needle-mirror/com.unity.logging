using System;

namespace Unity.Logging
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class LogWithName : Attribute
    {
        public string ReplacedName;

        public LogWithName(string newName)
        {
            ReplacedName = newName;
        }
    }
}
