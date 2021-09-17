using System;
using System.Runtime.InteropServices;

namespace Unity.Logging
{
    /// <summary>
    /// The struct for dispatching and processing log messages.
    /// </summary>
    /// <remarks>
    /// Log message data is allocated and stored using <see cref="LogMemoryManager"/>, which is referenced by this struct via a <see cref="PayloadHandle"/>.
    /// Log messages can be dispatched through <see cref="LogController.DispatchMessage"/>.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct LogMessage : IComparable<LogMessage>
    {
        /// <summary>
        /// References the log message data allocated through <see cref="LogMemoryManager"/>.
        /// </summary>
        public readonly PayloadHandle Payload;

        /// <summary>
        /// Timestamp to sort messages
        /// </summary>
        public readonly long Timestamp;

        /// <summary>
        /// StackTraceId
        /// </summary>
        public readonly long StackTraceId;

        /// <summary>
        /// Log Level: Verbose, Debug, Info, Warning, Error, Fatal
        /// </summary>
        public readonly LogLevel Level;

        /// <summary>
        /// Create a new LogMessage
        /// </summary>
        /// <param name="payload">PayloadHandle of the binary data</param>
        /// <param name="timestamp">Timestamp of the message</param>
        /// <param name="stacktraceId">Id of stacktrace connected to this message, or 0</param>
        /// <param name="level">LogLevel of this message</param>
        public LogMessage(PayloadHandle payload, long timestamp, long stacktraceId, LogLevel level)
        {
            Payload = payload;
            Timestamp = timestamp;
            Level = level;
            StackTraceId = stacktraceId;
        }

        /// <summary>
        /// Compares this LogMessage to another. Compares only timestamps. Needed for timestamp sorting
        /// </summary>
        /// <param name="other">Another LogMessage</param>
        /// <returns>True if timestamps are the same</returns>
        public int CompareTo(LogMessage other)
        {
            return Timestamp.CompareTo(other.Timestamp);
        }
    }
}
