using System;
using Unity.Burst;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
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
    public static class ManagedStackTraceWrapper
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate long CaptureStackTraceDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void FreeStackTraceDelegate(long id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ToUnsafeTextTraceDelegate(long id, ref UnsafeText result);

        // make sure delegates are not collected by GC
        private static CaptureStackTraceDelegate s_CaptureDelegate;
        private static FreeStackTraceDelegate s_FreeDelegate;
        private static ToUnsafeTextTraceDelegate s_ToUnsafeTextDelegate;

        private struct CaptureDelegateKey
        {
        }

        internal static readonly SharedStatic<FunctionPointer<CaptureStackTraceDelegate>> s_CaptureMethod =
            SharedStatic<FunctionPointer<CaptureStackTraceDelegate>>.GetOrCreate<FunctionPointer<CaptureStackTraceDelegate>, CaptureDelegateKey>(16);

        private struct FreeDelegateKey
        {
        }

        internal static readonly SharedStatic<FunctionPointer<FreeStackTraceDelegate>> s_FreeMethod = SharedStatic<FunctionPointer<FreeStackTraceDelegate>>.GetOrCreate<FunctionPointer<FreeStackTraceDelegate>, FreeDelegateKey>(16);

        private struct ToUnsafeTextDelegateKey
        {
        }

        internal static readonly SharedStatic<FunctionPointer<ToUnsafeTextTraceDelegate>> s_ToUnsafeTextMethod =
            SharedStatic<FunctionPointer<ToUnsafeTextTraceDelegate>>.GetOrCreate<FunctionPointer<ToUnsafeTextTraceDelegate>, ToUnsafeTextDelegateKey>(16);

        private static ConcurrentDictionary<long, StackTraceCapture.StackTraceData> s_StackTraces;
        private static long s_Counter;

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

            s_StackTraces = new ConcurrentDictionary<long, StackTraceCapture.StackTraceData>();
            s_Counter = 0;

            s_CaptureDelegate = CaptureStackTrace;
            s_FreeDelegate = FreeStackTrace;
            s_ToUnsafeTextDelegate = ToUnsafeTextStackTrace;

            s_CaptureMethod.Data = new FunctionPointer<CaptureStackTraceDelegate>(Marshal.GetFunctionPointerForDelegate(s_CaptureDelegate));
            s_FreeMethod.Data = new FunctionPointer<FreeStackTraceDelegate>(Marshal.GetFunctionPointerForDelegate(s_FreeDelegate));
            s_ToUnsafeTextMethod.Data = new FunctionPointer<ToUnsafeTextTraceDelegate>(Marshal.GetFunctionPointerForDelegate(s_ToUnsafeTextDelegate));
        }

        public static readonly string CaptureStackTraceFuncName1 = $"{nameof(ManagedStackTraceWrapper)}.{nameof(Capture)}";
        public static readonly string CaptureStackTraceFuncName2 = $"{nameof(ManagedStackTraceWrapper)}.{nameof(CaptureStackTrace)}";

        [AOT.MonoPInvokeCallback(typeof(CaptureStackTraceDelegate))]
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
        public static long Capture()
        {
            ThrowIfNotInitialized();

            return s_CaptureMethod.Data.Invoke();
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
                s_FreeMethod.Data.Invoke(id);
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
                s_ToUnsafeTextMethod.Data.Invoke(id, ref result);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void ThrowIfNotInitialized()
        {
            if (s_ToUnsafeTextMethod.Data.IsCreated == false)
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
