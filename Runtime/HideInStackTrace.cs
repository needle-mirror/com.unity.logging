namespace Unity.Logging
{
    /// <summary>
    /// Hides methods or any class' methods in the stacktrace in logging
    /// </summary>
    public class HideInStackTrace : System.Attribute
    {
        /// <summary>
        /// Hides methods or any class' methods in the stacktrace in logging
        /// </summary>
        /// <param name="hideEverythingInside">If true - every call inside will be hidden. If false - only this method/class' methods will be hidden</param>
        public HideInStackTrace(bool hideEverythingInside = false)
        {
            HideEverythingInside = hideEverythingInside;
        }

        /// <summary>
        /// If true - every call inside will be hidden. If false - only this method/class' methods will be hidden
        /// </summary>
        public readonly bool HideEverythingInside;
    }
}
