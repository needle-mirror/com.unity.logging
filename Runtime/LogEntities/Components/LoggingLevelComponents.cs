using System;
using Unity.Collections;

namespace Unity.Logging
{
    /// <summary>
    /// Log Level
    /// </summary>
    public enum LogLevel : byte
    {
        /// <summary>
        /// Log.Verbose
        /// </summary>
        Verbose,
        /// <summary>
        /// Log.Debug
        /// </summary>
        Debug,
        /// <summary>
        /// Log.Info
        /// </summary>
        Info,
        /// <summary>
        /// Log.Warning
        /// </summary>
        Warning,
        /// <summary>
        /// Log.Error
        /// </summary>
        Error,
        /// <summary>
        /// Log.Fatal
        /// </summary>
        Fatal
    }

    /// <summary>
    /// How Logger should synchronise logging messages.<br/>
    /// Async is usually faster than Sync, but can lead to lost messages.<br/>
    /// FatalIsSync works like async for all types of messages but Fatal. It will flush all the message queue when Fatal message appears.
    /// </summary>
    public enum SyncMode : byte
    {
        /// <summary>
        /// All messages are processed asynchronous, after they were logged. But if Fatal message is logged - log is flushed.
        /// Use this option if you want speed, but also want to make sure logs are not lost in case of Fatal error
        /// </summary>
        FatalIsSync,
        /// <summary>
        /// All messages are processed asynchronous, after they were logged.
        /// This is the fastest way, but can lead to lost messages in case of crashes
        /// </summary>
        FullAsync,
        /// <summary>
        /// All messages are processed immediately. Slowest mode, but all messages are guaranteed to be logged
        /// </summary>
        FullSync
    }

    /// <summary>
    /// FixedString representation of <see cref="LogLevel"/>
    /// </summary>
    public static class Consts
    {
        /// <summary>
        /// Log.Verbose
        /// </summary>
        public static readonly FixedString32Bytes VerboseString = "VERBOSE";
        /// <summary>
        /// Log.Debug
        /// </summary>
        public static readonly FixedString32Bytes DebugString = "DEBUG";
        /// <summary>
        /// Log.Info
        /// </summary>
        public static readonly FixedString32Bytes InfoString = "INFO";
        /// <summary>
        /// Log.Warning
        /// </summary>
        public static readonly FixedString32Bytes WarningString = "WARNING";
        /// <summary>
        /// Log.Error
        /// </summary>
        public static readonly FixedString32Bytes ErrorString = "ERROR";
        /// <summary>
        /// Log.Fatal
        /// </summary>
        public static readonly FixedString32Bytes FatalString = "FATAL";
    }

    /// <summary>
    /// Static class with utility burst compatible functions for <see cref="LogLevel"/> - <see cref="FixedString32Bytes"/> convertion
    /// </summary>
    public static class LogLevelUtilsBurstCompatible
    {
        /// <summary>
        /// Converts <see cref="LogLevel"/> to <see cref="FixedString32Bytes"/>
        /// </summary>
        /// <param name="level">Log Level to convert</param>
        /// <returns><see cref="FixedString32Bytes"/> for <see cref="LogLevel"/></returns>
        /// <exception cref="ArgumentOutOfRangeException">If unknown level specified.</exception>
        public static FixedString32Bytes ToFixedString(in LogLevel level)
        {
            // *begin-nonstandard-formatting*
            return level switch
            {
                LogLevel.Verbose => Consts.VerboseString,
                LogLevel.Debug => Consts.DebugString,
                LogLevel.Info => Consts.InfoString,
                LogLevel.Warning => Consts.WarningString,
                LogLevel.Error => Consts.ErrorString,
                LogLevel.Fatal => Consts.FatalString,
                _ => throw new ArgumentOutOfRangeException()
            };
            // *end-nonstandard-formatting*
        }

        /// <summary>
        /// Converts <see cref="FixedString32Bytes"/> to <see cref="LogLevel"/>
        /// </summary>
        /// <param name="str">FixedString32Bytes representation of <see cref="LogLevel"/></param>
        /// <returns><see cref="LogLevel"/> that was in <see cref="FixedString32Bytes"/></returns>
        /// <exception cref="ArgumentOutOfRangeException">If cannot match the <see cref="LogLevel"/></exception>
        public static LogLevel Parse(in FixedString32Bytes str)
        {
            // *begin-nonstandard-formatting*
            if (str == Consts.VerboseString) return LogLevel.Verbose;
            if (str == Consts.DebugString) return LogLevel.Debug;
            if (str == Consts.InfoString) return LogLevel.Info;
            if (str == Consts.WarningString) return LogLevel.Warning;
            if (str == Consts.ErrorString) return LogLevel.Error;
            if (str == Consts.FatalString) return LogLevel.Fatal;
            throw new ArgumentOutOfRangeException();
            // *end-nonstandard-formatting*
        }
    }
}
