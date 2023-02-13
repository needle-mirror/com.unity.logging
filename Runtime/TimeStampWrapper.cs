#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

using TimeStampManager = Unity.Logging.Internal.TimeStampManagerBaselib;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// Structure that stores time stamp for the <see cref="LogMessage"/>
    /// </summary>
    [HideInStackTrace]
    public struct TimeStampWrapper
    {
        struct LocalKey { }
        struct LocalConsoleKey { }

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
        /// Calculates the difference in milliseconds between a given timestamp in nanoseconds and <see cref="GetTimeStamp"/>.
        /// </summary>
        /// <param name="sinceTimestamp">Timestamp in nanoseconds to calculate the difference with</param>
        /// <returns>Milliseconds between given timestamp and <see cref="GetTimeStamp"/>.</returns>
        public static long TotalMillisecondsSince(long sinceTimestamp)
        {
            var now = GetTimeStamp();

            var deltaNanoseconds = now - sinceTimestamp;

            // nanoseconds to msec
            return deltaNanoseconds / 1000000;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int ToUnsafeTextTimestampDelegate(long timestamp, ref UnsafeText result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ToFixedStringFileNameDelegate(long timestamp, ref FixedString64Bytes result);

        private static byte s_Initialized = 0;


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

            TimeStampManager.Initialize();

            Burst2ManagedCall<ToUnsafeTextTimestampDelegate, TimeStampWrapper>.Init(ToUnsafeTextTimeUTC);
            Burst2ManagedCall<ToUnsafeTextTimestampDelegate, LocalKey>.Init(ToUnsafeTextTimeLocalTimeZone);
            Burst2ManagedCall<ToUnsafeTextTimestampDelegate, LocalConsoleKey>.Init(ToUnsafeTextTimeLocalTimeZoneConsole);
            Burst2ManagedCall<ToFixedStringFileNameDelegate, TimeStampWrapper>.Init(ToFixedStringFileName);
        }


        [AOT.MonoPInvokeCallback(typeof(ToUnsafeTextTimestampDelegate))]
        private static int ToUnsafeTextTimeUTC(long timestampUTCNanoseconds, ref UnsafeText result)
        {
            var dateTime = new DateTime(NanosecToDateTimeTicks(timestampUTCNanoseconds), DateTimeKind.Utc);

            var n = result.Length;
            result.Append(dateTime.ToString(TimestampFormat));
            return result.Length - n;
        }

        [AOT.MonoPInvokeCallback(typeof(ToUnsafeTextTimestampDelegate))]
        private static int ToUnsafeTextTimeLocalTimeZone(long timestampUTCNanoseconds, ref UnsafeText result)
        {
            var dateTime = new DateTime(NanosecToDateTimeTicks(timestampUTCNanoseconds), DateTimeKind.Utc);

            var n = result.Length;
            result.Append(dateTime.ToLocalTime().ToString(TimestampFormat));
            return result.Length - n;
        }

        [AOT.MonoPInvokeCallback(typeof(ToUnsafeTextTimestampDelegate))]
        private static int ToUnsafeTextTimeLocalTimeZoneConsole(long timestampUTCNanoseconds, ref UnsafeText result)
        {
            var dateTime = new DateTime(NanosecToDateTimeTicks(timestampUTCNanoseconds), DateTimeKind.Utc);

            var n = result.Length;
            result.Append(dateTime.ToLocalTime().ToString("HH:mm:ss.fff"));
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
#if LOGGING_USE_UNMANAGED_DELEGATES
                unsafe
                {
                    return ((delegate * unmanaged[Cdecl] <long>)s_GetTimestampHandler.Data.Value)();
                }
#else
                return s_GetTimestampHandler.Data.Invoke();
#endif
            }

            return TimeStampManager.GetTimeStamp();
        }

        /// <summary>
        /// Writes human-readable timestamp representation into buffer as UTC using TimestampFormat
        /// </summary>
        /// <param name="timestamp">UTC timestamp in nanoseconds to write</param>
        /// <param name="messageOutput">UnsafeText to write to</param>
        /// <returns>Length written to the buffer</returns>
        public static int GetFormattedTimeStampString(long timestamp, ref UnsafeText messageOutput)
        {
            var ptr = Burst2ManagedCall<ToUnsafeTextTimestampDelegate, TimeStampWrapper>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                return ((delegate * unmanaged[Cdecl] <long, ref UnsafeText, int>)ptr.Value)(timestamp, ref messageOutput);
            }
#else
            return ptr.Invoke(timestamp, ref messageOutput);
#endif
        }

        /// <summary>
        /// Writes human-readable timestamp representation into buffer as Local time using TimestampFormat
        /// </summary>
        /// <param name="timestamp">UTC timestamp in nanoseconds to write</param>
        /// <param name="messageOutput">UnsafeText to write to</param>
        /// <returns>Length written to the buffer</returns>
        public static int GetFormattedTimeStampStringLocalTime(long timestamp, ref UnsafeText messageOutput)
        {
            var ptr = Burst2ManagedCall<ToUnsafeTextTimestampDelegate, LocalKey>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                return ((delegate * unmanaged[Cdecl] <long, ref UnsafeText, int>)ptr.Value)(timestamp, ref messageOutput);
            }
#else
            return ptr.Invoke(timestamp, ref messageOutput);
#endif
        }

        /// <summary>
        /// Writes human-readable timestamp representation into buffer as Local time, HH:MM:SS
        /// </summary>
        /// <param name="timestamp">UTC timestamp in nanoseconds to write</param>
        /// <param name="messageOutput">UnsafeText to write to</param>
        /// <returns>Length written to the buffer</returns>
        public static int GetFormattedTimeStampStringLocalTimeForConsole(long timestamp, ref UnsafeText messageOutput)
        {
            var ptr = Burst2ManagedCall<ToUnsafeTextTimestampDelegate, LocalConsoleKey>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                return ((delegate * unmanaged[Cdecl] <long, ref UnsafeText, int>)ptr.Value)(timestamp, ref messageOutput);
            }
#else
            return ptr.Invoke(timestamp, ref messageOutput);
#endif
        }

        /// <summary>
        /// Timestamp to filename <see cref="FixedString64Bytes"/>
        /// </summary>
        /// <param name="timestamp">UTC timestamp in nanoseconds</param>
        /// <returns><see cref="FixedString64Bytes"/> to be used for the filename</returns>
        public static FixedString64Bytes GetFormattedTimeStampStringForFileName(long timestamp)
        {
            FixedString64Bytes res = "";

            var ptr = Burst2ManagedCall<ToFixedStringFileNameDelegate, TimeStampWrapper>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                ((delegate * unmanaged[Cdecl] <long, ref FixedString64Bytes, void>)ptr.Value)(timestamp, ref res);
            }
#else
            ptr.Invoke(timestamp, ref res);
#endif
            return res;
        }
    }
}
