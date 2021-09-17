//#define USE_BASELIB
//#define USE_NATIVE_TIME
// if nothing is defined - time is just an atomic counter

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

#if USE_BASELIB
using Unity.Baselib.LowLevel;
#endif

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Structure that stores time stamp for the <see cref="LogMessage"/>
    /// </summary>
    public struct TimeStampWrapper
    {
        /// <summary>
        /// Defines a delegate to capture the timestamp
        /// </summary>
        /// <returns>long as a timestamp in nanoseconds</returns>>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long CustomGetTimestampHandler();
        internal struct CustomGetTimestampHandlerKey {}
        internal static readonly SharedStatic<FunctionPointer<CustomGetTimestampHandler>> s_GetTimestampHandler = SharedStatic<FunctionPointer<CustomGetTimestampHandler>>.GetOrCreate<FunctionPointer<CustomGetTimestampHandler>, CustomGetTimestampHandlerKey>(16);

        /// <summary>
        /// Set a custom function-handler to get a timestamp in nanoseconds
        /// </summary>
        /// <param name="handler">Function-handler of <see cref="CustomGetTimestampHandler"/> type</param>
        /// <param name="isBurstable">True if the function-handler is burst compatible</param>
        public static void SetHandlerForTimestamp(CustomGetTimestampHandler handler, bool isBurstable = false)
        {
            if (handler != null)
            {
                // Check if list already contains this delegate (Contains won't work with FunctionPointer type)
                FunctionPointer<CustomGetTimestampHandler> func;
                if (isBurstable)
                {
                    func = BurstCompiler.CompileFunctionPointer(handler);
                }
                else
                {
                    // Pin the delegate so it can be used for the lifetime of the app. Never de-alloc
                    GCHandle.Alloc(handler);
                    func = new FunctionPointer<CustomGetTimestampHandler>(Marshal.GetFunctionPointerForDelegate(handler));
                }

                func.Invoke(); // warmup

                s_GetTimestampHandler.Data = func;
            }
            else
            {
                s_GetTimestampHandler.Data = default;
                Assert.IsFalse(s_GetTimestampHandler.Data.IsCreated);
            }
        }

        /// <summary>
        /// Function that calculates difference in milliseconds between <see cref="sinceTimestamp"/> and 'now' (to get 'now' it calls <see cref="GetTimeStamp"/>).
        /// Timestamps are in nanoseconds
        /// </summary>
        /// <param name="sinceTimestamp">Timestamp to calculate the difference with</param>
        /// <returns>Milliseconds between <see cref="sinceTimestamp"/> and 'now'</returns>
        public static long TotalMillisecondsSince(long sinceTimestamp)
        {
            var now = GetTimeStamp();

            var deltaNanoseconds = now - sinceTimestamp;

            // nanoseconds to msec
            return deltaNanoseconds / 1000000;
        }

#if USE_NATIVE_TIME
        [DllImport("lib_unity_logging", EntryPoint = "GetTimeStamp")]
        static extern unsafe long GetTimeStampNative();

        [DllImport("lib_unity_logging", EntryPoint = "GetFormattedTimeStampString")]
        static extern unsafe int GetFormattedTimeStampStringNative(long timestamp, byte* buffer, int bufferSize);

        [DllImport("lib_unity_logging", EntryPoint = "GetFormattedTimeStampStringForFileName")]
        static extern unsafe int GetFormattedTimeStampStringForFileNameNative(long timestamp, byte* buffer, int bufferSize);

        /// <summary>
        /// Returns current UTC timestamp
        /// </summary>
        /// <returns>UTC timestamp</returns>
        public static long GetTimeStamp()
        {
            if (s_GetTimestampHandler.Data.IsCreated)
            {
                return s_GetTimestampHandler.Data.Invoke();
            }
            else
            {
                // default
                return GetTimeStampNative();
            }
        }

        /// <summary>
        /// Writes human-readable timestamp representation into buffer
        /// </summary>
        /// <param name="timestamp">Timestamp to write</param>
        /// <param name="buffer">Buffer to write to</param>
        /// <param name="bufferSize">Size of the buffer</param>
        /// <returns>Length written to the buffer</returns>
        public static unsafe int GetFormattedTimeStampString(long timestamp, ref UnsafeText messageOutput)
        {
            FixedString64Bytes res = "";
            var buffer = messageOutput.GetUnsafePtr();
            var bufferSize = FixedString64Bytes.UTF8MaxLengthInBytes;
            res.Length = bufferSize;

            var written = GetFormattedTimeStampStringNative(timestamp, buffer, bufferSize);
            if (written > 0)
            {
                res.Length = written;
                messageOutput.Append(res);
            }

            return written;
        }

        public static FixedString64Bytes GetFormattedTimeStampStringForFileName(long timestamp)
        {
            FixedString64Bytes res = "";
            var buffer = messageOutput.GetUnsafePtr();
            var bufferSize = FixedString64Bytes.UTF8MaxLengthInBytes;
            res.Length = bufferSize;

            var written = GetFormattedTimeStampStringForFileNameNative(timestamp, buffer, bufferSize);
            if (written > 0)
            {
                res.Length = written;
                return res;
            }

            return "";
        }

#else

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate long CaptureTimestampDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int ToUnsafeTextTimestampDelegate(long timestamp, ref UnsafeText result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ToFixedStringFileNameDelegate(long timestamp, ref FixedString64Bytes result);

        // make sure delegates are not collected by GC
        private static CaptureTimestampDelegate s_CaptureDelegate;
        private static ToUnsafeTextTimestampDelegate s_ToUnsafeTextDelegate;
        private static ToFixedStringFileNameDelegate s_ToFixedStringFileNameDelegate;

        private struct CaptureTimestampDelegateKey {}
        internal static readonly SharedStatic<FunctionPointer<CaptureTimestampDelegate>> s_CaptureMethod = SharedStatic<FunctionPointer<CaptureTimestampDelegate>>.GetOrCreate<FunctionPointer<CaptureTimestampDelegate>, CaptureTimestampDelegateKey>(16);

        private struct ToUnsafeTextTimestampDelegateKey {}
        internal static readonly SharedStatic<FunctionPointer<ToUnsafeTextTimestampDelegate>> s_ToUnsafeTextMethod = SharedStatic<FunctionPointer<ToUnsafeTextTimestampDelegate>>.GetOrCreate<FunctionPointer<ToUnsafeTextTimestampDelegate>, ToUnsafeTextTimestampDelegateKey>(16);

        private struct ToFixedStringFileNameDelegateKey {}
        internal static readonly SharedStatic<FunctionPointer<ToFixedStringFileNameDelegate>> s_ToFixedStringFileNameMethod = SharedStatic<FunctionPointer<ToFixedStringFileNameDelegate>>.GetOrCreate<FunctionPointer<ToFixedStringFileNameDelegate>, ToFixedStringFileNameDelegateKey>(16);

        private static byte s_Initialized = 0;

        private static Stopwatch s_Stopwatch;
        private static DateTime s_StopwatchStartTime;

        /// <summary>
        /// Default Timestamp string format
        /// </summary>
        public const string TimestampFormatDefault = "yyyy/MM/dd HH:mm:ss.fff";

        /// <summary>
        /// Current Timestamp string format
        /// </summary>
        public static string TimestampFormat = TimestampFormatDefault;

        // [long.minValue, long.maxValue] maps to [9/23/1907 12:12:43 AM, 4/10/2492 11:47:16 PM] with nanosecond precision
        private const long DateTimeBase = 693937152000000000; // new DateTime(2200, 1, 1).Ticks;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long NanosecToDateTimeTicks(long nanosec)
        {
            // https://docs.microsoft.com/en-us/dotnet/api/system.datetime.ticks
            // A single tick represents one hundred nanoseconds

            return (nanosec / 100) + DateTimeBase;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long DateTimeTicksToNanosec(long datetimeTicks)
        {
            // https://docs.microsoft.com/en-us/dotnet/api/system.datetime.ticks
            // A single tick represents one hundred nanoseconds

            return (datetimeTicks - DateTimeBase) * 100;
        }

        static TimeStampWrapper()
        {
            Initialize();
        }

        /// <summary>
        /// Initializes everything needed for Timestamps to be captured. Should be called from managed (non-burst) environment
        /// </summary>
        [BurstDiscard]
        public static void Initialize()
        {
            if (s_Initialized != 0)
                return;

            s_Initialized = 1;

            s_CaptureDelegate = CaptureDateTimeUTCNanoseconds;
            s_ToUnsafeTextDelegate = ToUnsafeTextTimeUTC;
            s_ToFixedStringFileNameDelegate = ToFixedStringFileName;

            s_Stopwatch = new Stopwatch();
            s_StopwatchStartTime = DateTime.UtcNow;
            s_Stopwatch.Restart();

            s_CaptureMethod.Data = new FunctionPointer<CaptureTimestampDelegate>(Marshal.GetFunctionPointerForDelegate(s_CaptureDelegate));
            s_ToUnsafeTextMethod.Data = new FunctionPointer<ToUnsafeTextTimestampDelegate>(Marshal.GetFunctionPointerForDelegate(s_ToUnsafeTextDelegate));
            s_ToFixedStringFileNameMethod.Data = new FunctionPointer<ToFixedStringFileNameDelegate>(Marshal.GetFunctionPointerForDelegate(s_ToFixedStringFileNameDelegate));
        }

        [AOT.MonoPInvokeCallback(typeof(CaptureTimestampDelegate))]
        private static long CaptureDateTimeUTCNanoseconds()
        {
            return DateTimeTicksToNanosec( s_StopwatchStartTime.Add(s_Stopwatch.Elapsed).Ticks );
        }

        [AOT.MonoPInvokeCallback(typeof(ToUnsafeTextTimestampDelegateKey))]
        private static int ToUnsafeTextTimeUTC(long timestampUTCNanoseconds, ref UnsafeText result)
        {
            var dateTime = new DateTime(NanosecToDateTimeTicks(timestampUTCNanoseconds));

            var n = result.Length;
            result.Append(dateTime.ToString(TimestampFormat));
            return result.Length - n;
        }

        [AOT.MonoPInvokeCallback(typeof(ToFixedStringFileNameDelegate))]
        private static void ToFixedStringFileName(long timestampUTCNanoseconds, ref FixedString64Bytes result)
        {
            var dateTime = new DateTime(NanosecToDateTimeTicks(timestampUTCNanoseconds));

            result.Clear();
            result.Append(dateTime.ToString("yyyyMMddHHmm"));
        }

        /// <summary>
        /// Returns current UTC timestamp in nanoseconds
        /// </summary>
        /// <returns>UTC timestamp in nanoseconds</returns>
        public static long GetTimeStamp()
        {
            if (s_GetTimestampHandler.Data.IsCreated)
            {
                return s_GetTimestampHandler.Data.Invoke();
            }

#if USE_BASELIB
            var ratio = Binding.Baselib_Timer_GetTicksToNanosecondsConversionRatio();
            var rate = (double)ratio.ticksToNanosecondsNumerator / ratio.ticksToNanosecondsDenominator;
            
            var tick = Binding.Baselib_Timer_GetHighPrecisionTimerTicks();
            return (long)(tick * rate);
#else
            return s_CaptureMethod.Data.Invoke();
#endif
        }

        /// <summary>
        /// Writes human-readable timestamp representation into buffer
        /// </summary>
        /// <param name="timestamp">UTC timestamp in nanoseconds to write</param>
        /// <param name="messageOutput">UnsafeText to write to</param>
        /// <returns>Length written to the buffer</returns>
        public static int GetFormattedTimeStampString(long timestamp, ref UnsafeText messageOutput)
        {
            return s_ToUnsafeTextMethod.Data.Invoke(timestamp, ref messageOutput);
        }

        /// <summary>
        /// Timestamp to filename <see cref="FixedString64Bytes"/>
        /// </summary>
        /// <param name="timestamp">UTC timestamp in nanoseconds</param>
        /// <returns><see cref="FixedString64Bytes"/> to be used for the filename</returns>
        public static FixedString64Bytes GetFormattedTimeStampStringForFileName(long timestamp)
        {
            FixedString64Bytes res = "";
            s_ToFixedStringFileNameMethod.Data.Invoke(timestamp, ref res);
            return res;
        }
#endif
    }
}
