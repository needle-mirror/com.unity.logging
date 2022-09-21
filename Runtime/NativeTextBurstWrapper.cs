using System;
using Unity.Collections;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Class that is used for NativeText to extract Burst friendly data (at this moment NativeText has DisposeSentinel that is not directly supported by burst)
    /// </summary>
    public readonly struct NativeTextBurstWrapper
    {
        public readonly IntPtr ptr;
        public readonly int len;

        private NativeTextBurstWrapper(NativeText nt)
        {
            ptr = Unity.Logging.Internal.UnsafeWrapperUtility.GetPointer(nt);
            len = nt.Length;
        }

        public static implicit operator NativeTextBurstWrapper(NativeText nt)
        {
            return new NativeTextBurstWrapper(nt);
        }
    }
}
