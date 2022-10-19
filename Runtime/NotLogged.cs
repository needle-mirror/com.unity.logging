using System;

namespace Unity.Logging
{
    /// <summary>
    /// Attribute to ignore field/property name from being serialized in logging
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NotLogged : Attribute
    {
    }
}
