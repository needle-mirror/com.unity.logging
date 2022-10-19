using System;
using Unity.Collections;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Class that is used for NativeText to extract Burst friendly data (at this moment NativeText has DisposeSentinel that is not directly supported by burst)
    /// </summary>
    public readonly struct NativeTextBurstWrapper
    {
        /// <summary>
        /// NativeText's unsafe pointer
        /// </summary>
        public readonly IntPtr ptr;
        /// <summary>
        /// Length of the NativeText
        /// </summary>
        public readonly int len;

        private NativeTextBurstWrapper(NativeText nt)
        {
            ptr = Unity.Logging.Internal.UnsafeWrapperUtility.GetPointer(nt);
            len = nt.Length;
        }

        /// <summary>
        /// Implicit conversion NativeText -> NativeTextBurstWrapper
        /// </summary>
        /// <param name="nt">NativeText to convert</param>
        /// <returns>Returns NativeTextBurstWrapper</returns>
        public static implicit operator NativeTextBurstWrapper(NativeText nt)
        {
            return new NativeTextBurstWrapper(nt);
        }
    }
}
