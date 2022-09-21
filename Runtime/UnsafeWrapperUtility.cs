using System;
using Unity.Collections;

namespace Unity.Logging.Internal
{
    // Used to avoid unsafe code in users assemblies
    public static class UnsafeWrapperUtility
    {
        public static IntPtr GetPointer(NativeText nativeText)
        {
            unsafe
            {
                return new IntPtr(nativeText.GetUnsafePtr());
            }
        }
    }
}
