#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Timestamp logic controlled by DateTime + Stopwatch
    /// </summary>
    [HideInStackTrace]
    public static class TimeStampManagerManaged
    {
        private static byte s_Initialized;

        private static Stopwatch s_Stopwatch;
        private static DateTime s_StopwatchStartTime;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long CaptureTimestampDelegate();
        private struct CaptureTimestampDelegateKey {}

        [BurstDiscard]
        internal static void Initialize()
        {
            if (s_Initialized != 0)
                return;
            s_Initialized = 1;

            s_StopwatchStartTime = DateTime.UtcNow;
            s_Stopwatch = Stopwatch.StartNew();

            Burst2ManagedCall<CaptureTimestampDelegate, CaptureTimestampDelegateKey>.Init(CaptureDateTimeUTCNanoseconds);
        }

        [AOT.MonoPInvokeCallback(typeof(CaptureTimestampDelegate))]
        private static long CaptureDateTimeUTCNanoseconds()
        {
            return TimeStampWrapper.DateTimeTicksToNanosec( s_StopwatchStartTime.Add(s_Stopwatch.Elapsed).Ticks );
        }

        /// <summary>
        /// Returns current timestamp
        /// </summary>
        /// <returns>UTC timestamp in nanoseconds</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTimeStamp()
        {
            var ptr = Burst2ManagedCall<CaptureTimestampDelegate, CaptureTimestampDelegateKey>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                return ((delegate * unmanaged[Cdecl] <long>)ptr.Value)();
            }
#else
            return ptr.Invoke();
#endif
        }
    }
}
