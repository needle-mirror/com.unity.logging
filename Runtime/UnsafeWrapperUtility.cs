using System;
using Unity.Collections;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Used to avoid unsafe code in users assemblies
    /// </summary>
    public static class UnsafeWrapperUtility
    {
        /// <summary>
        /// Gets unsafePtr from the NativeText and returns its IntPtr
        /// </summary>
        /// <param name="nativeText">NativeText</param>
        /// <returns>NativeText's unsafe ptr</returns>
        public static IntPtr GetPointer(NativeText nativeText)
        {
            unsafe
            {
                return new IntPtr(nativeText.GetUnsafePtr());
            }
        }
    }
}
