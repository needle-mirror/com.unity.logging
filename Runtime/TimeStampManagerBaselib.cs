#if USE_BASELIB

using System;
using System.Runtime.CompilerServices;
using Unity.Baselib.LowLevel;
using Unity.Burst;

namespace Unity.Logging.Internal
{
    [HideInStackTrace]
    public static class TimeStampManagerBaselib
    {
        private static byte s_Initialized;

        private struct TimestampOffset {}
        private static readonly SharedStatic<long> s_TimestampStartTimeNanosec = SharedStatic<long>.GetOrCreate<long, TimestampOffset>(16);

        [BurstDiscard]
        internal static void Initialize()
        {
            if (s_Initialized != 0)
                return;
            s_Initialized = 1;

            s_TimestampStartTimeNanosec.Data = TimeStampWrapper.DateTimeTicksToNanosec( DateTime.UtcNow.Ticks ) - (long)(Binding.Baselib_Timer_GetTimeSinceStartupInSeconds() * Binding.Baselib_NanosecondsPerSecond);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTimeStamp()
        {
            return (long)(Binding.Baselib_Timer_GetTimeSinceStartupInSeconds() * Binding.Baselib_NanosecondsPerSecond) + s_TimestampStartTimeNanosec.Data;
        }
    }
}

#endif
