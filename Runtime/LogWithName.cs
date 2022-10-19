using System;

namespace Unity.Logging
{
    /// <summary>
    /// Attribute to set a custom name that is different from field/property name
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class LogWithName : Attribute
    {
        /// <summary>
        /// Name that should be used instead of the field/property name
        /// </summary>
        public string ReplacedName;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="newName">Set the name that should be used instead of the field/property name</param>
        public LogWithName(string newName)
        {
            ReplacedName = newName;
        }
    }
}
