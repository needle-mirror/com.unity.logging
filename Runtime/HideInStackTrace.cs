namespace Unity.Logging
{
    /// <summary>
    /// This attribute is used to hide methods or any class' methods in the stacktrace in logging
    /// </summary>
    public class HideInStackTrace : System.Attribute
    {
        public HideInStackTrace(bool hideEverythingInside = false)
        {
            HideEverythingInside = hideEverythingInside;
        }

        public readonly bool HideEverythingInside;
    }
}
