using System;
using Unity.Collections;

namespace Unity.Logging
{
    /// <summary>
    /// Log Level
    /// </summary>
    public enum LogLevel : byte
    {
        // Log.Verbose
        Verbose,
        // Log.Debug
        Debug,
        // Log.Info
        Info,
        // Log.Warning
        Warning,
        // Log.Error
        Error,
        // Log.Fatal
        Fatal
    }

    /// <summary>
    /// FixedString representation of <see cref="LogLevel"/>
    /// </summary>
    public static class Consts
    {
        // Log.Verbose
        public static readonly FixedString32Bytes VerboseString = "VERBOSE";
        // Log.Debug
        public static readonly FixedString32Bytes DebugString = "DEBUG";
        // Log.Info
        public static readonly FixedString32Bytes InfoString = "INFO";
        // Log.Warning
        public static readonly FixedString32Bytes WarningString = "WARNING";
        // Log.Error
        public static readonly FixedString32Bytes ErrorString = "ERROR";
        // Log.Fatal
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
