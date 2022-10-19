#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

using System;
using Unity.Burst;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Static class to deal with StackTrace capturing.
    /// Includes managed function pointers that can be called from Burst code. It also provides a way to defer analysis of the stack trace for faster performance.
    /// <para />
    /// Used from the codegen
    /// <seealso cref="StackTraceCapture"/>
    /// </summary>
    [HideInStackTrace]
    public static class ManagedStackTraceWrapper
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate long CaptureStackTraceDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void FreeStackTraceDelegate(long id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ToUnsafeTextTraceDelegate(long id, ref UnsafeText result);

        private static ConcurrentDictionary<long, StackTraceCapture.StackTraceData> s_StackTraces;
        private static long s_Counter;

        struct ManagedStackTraceWrapperKey {}
        static ManagedStackTraceWrapper()
        {
            Initialize();
        }

        /// <summary>
        /// Initializes everything needed for StackTraces to be captured. Should be called from managed (non-burst) environment
        /// </summary>
        [BurstDiscard]
        public static void Initialize()
        {
            if (s_StackTraces != null)
                return;

            StackTraceCapture.Initialize();
            s_StackTraces = new ConcurrentDictionary<long, StackTraceCapture.StackTraceData>();
            s_Counter = 0;

            Burst2ManagedCall<CaptureStackTraceDelegate, ManagedStackTraceWrapperKey>.Init(CaptureStackTrace);
            Burst2ManagedCall<FreeStackTraceDelegate, ManagedStackTraceWrapperKey>.Init(FreeStackTrace);
            Burst2ManagedCall<ToUnsafeTextTraceDelegate, ManagedStackTraceWrapperKey>.Init(ToUnsafeTextStackTrace);
        }

        [AOT.MonoPInvokeCallback(typeof(CaptureStackTraceDelegate))]
        [HideInStackTrace(hideEverythingInside:true)]
        private static long CaptureStackTrace()
        {
            var res = Interlocked.Increment(ref s_Counter);
            var st = StackTraceCapture.GetStackTrace();
            s_StackTraces.TryAdd(res, st);

            return res;
        }

        [AOT.MonoPInvokeCallback(typeof(FreeStackTraceDelegate))]
        private static void FreeStackTrace(long id)
        {
            if (s_StackTraces.TryRemove(id, out var data))
                StackTraceCapture.ReleaseStackTrace(data);
        }

        [AOT.MonoPInvokeCallback(typeof(ToUnsafeTextTraceDelegate))]
        private static void ToUnsafeTextStackTrace(long id, ref UnsafeText result)
        {
            var data = s_StackTraces[id];
            StackTraceCapture.ToUnsafeTextStackTrace(data, ref result);
        }

        /// <summary>
        /// Captures a stack trace and returns its id
        /// Can be called from burst (if <see cref="Initialize"/> was called before) or not burst
        /// </summary>
        /// <returns>Id of the captured stacktrace</returns>
        [HideInStackTrace(hideEverythingInside:true)]
        public static long Capture()
        {
            ThrowIfNotInitialized();

            var ptr = Burst2ManagedCall<CaptureStackTraceDelegate, ManagedStackTraceWrapperKey>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                return ((delegate * unmanaged[Cdecl] <long>)ptr.Value)();
            }
#else
            return ptr.Invoke();
#endif
        }

        /// <summary>
        /// Releases captured stack trace that was captured with <see cref="Capture"/>
        /// Can be called from burst (if <see cref="Initialize"/> was called before) or not burst
        /// </summary>
        /// <param name="id">Id of the captured stacktrace to release</param>
        public static void Free(long id)
        {
            ThrowIfNotInitialized();
            if (id != 0)
            {
                var ptr = Burst2ManagedCall<FreeStackTraceDelegate, ManagedStackTraceWrapperKey>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
                unsafe
                {
                    ((delegate * unmanaged[Cdecl] <long, void>)ptr.Value)(id);
                }
#else
                ptr.Invoke(id);
#endif
            }
        }

        /// <summary>
        /// Appends text representation of the stack trace that was captured with <see cref="Capture"/> to <see cref="UnsafeText"/>
        /// </summary>
        /// <param name="id">Id of the captured stacktrace</param>
        /// <param name="result"><see cref="UnsafeText"/> where to append the stacktrace's text representation</param>
        public static void AppendToUnsafeText(long id, ref UnsafeText result)
        {
            ThrowIfNotInitialized();
            if (id != 0)
            {
                var ptr = Burst2ManagedCall<ToUnsafeTextTraceDelegate, ManagedStackTraceWrapperKey>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
                unsafe
                {
                    ((delegate * unmanaged[Cdecl] <long, ref UnsafeText, void>)ptr.Value)(id, ref result);
                }
#else
                ptr.Invoke(id, ref result);
#endif
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
        private static void ThrowIfNotInitialized()
        {
            if (Burst2ManagedCall<ToUnsafeTextTraceDelegate, ManagedStackTraceWrapperKey>.IsCreated == false)
                throw new Exception("ManagedStackTraceWrapper.Initialize() was not called!");
        }

        /// <summary>
        /// Debug function. Asserts is there are any Captured and not Freed stack traces.
        /// <seealso cref="Capture"/>
        /// <seealso cref="Free"/>
        /// </summary>
        public static void AssertNoAllocatedResources()
        {
            Assert.AreEqual(0, s_StackTraces.Count, "AssertNoAllocatedResources failed");
        }

        /// <summary>
        /// Debug function. Force clears all Captured stacktraces if any. Invalidates all ids captured before, so it is not safe to call <see cref="AppendToUnsafeText"/> on any ids captured before.
        /// <seealso cref="Capture"/>
        /// <seealso cref="Free"/>
        /// </summary>
        public static void ForceClearAll()
        {
            s_StackTraces.Clear();
        }
    }
}
