using System;

namespace Unity.Logging
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NotLogged : Attribute
    {
    }
}
