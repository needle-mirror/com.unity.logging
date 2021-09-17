using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;

namespace Unity.Logging.Internal
{
    public static class TimeStampManagerManaged
    {
        private static byte s_Initialized;
        private static CaptureTimestampDelegate s_CaptureDelegate;
        private static Stopwatch s_Stopwatch;
        private static DateTime s_StopwatchStartTime;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long CaptureTimestampDelegate();
        private struct CaptureTimestampDelegateKey {}
        private static readonly SharedStatic<FunctionPointer<CaptureTimestampDelegate>> s_CaptureMethod = SharedStatic<FunctionPointer<CaptureTimestampDelegate>>.GetOrCreate<FunctionPointer<CaptureTimestampDelegate>, CaptureTimestampDelegateKey>(16);

        [BurstDiscard]
        internal static void Initialize()
        {
            if (s_Initialized != 0)
                return;
            s_Initialized = 1;

            s_CaptureDelegate = CaptureDateTimeUTCNanoseconds;
            s_StopwatchStartTime = DateTime.UtcNow;
            s_Stopwatch = Stopwatch.StartNew();

            s_CaptureMethod.Data = new FunctionPointer<CaptureTimestampDelegate>(Marshal.GetFunctionPointerForDelegate(s_CaptureDelegate));

        }

        [AOT.MonoPInvokeCallback(typeof(CaptureTimestampDelegate))]
        private static long CaptureDateTimeUTCNanoseconds()
        {
            return TimeStampWrapper.DateTimeTicksToNanosec( s_StopwatchStartTime.Add(s_Stopwatch.Elapsed).Ticks );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTimeStamp()
        {
#if UNITY_2021_2_OR_NEWER // C# 9 support, unmanaged delegates - gc alloc free way to call FunctionPointer
                unsafe
                {
                    return ((delegate * unmanaged[Cdecl] <long>)s_CaptureMethod.Data.Value)();
                }
#else
            return s_CaptureMethod.Data.Invoke();
#endif
        }
    }
}
