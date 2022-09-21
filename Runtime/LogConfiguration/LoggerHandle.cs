using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Unity.Logging
{
    /// <summary>
    /// Structure that contains unique identifier of <see cref="Logger"/>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct LoggerHandle
    {
        /// <summary>
        /// Unique id of <see cref="Logger"/>. Valid value is not 0.
        /// </summary>
        public readonly uint Value;

        /// <summary>
        /// Internal call to create a new LoggerHandle
        /// </summary>
        /// <param name="newId">unique Id</param>
        internal LoggerHandle(uint newId)
        {
            Value = newId;
        }

        /// <summary>
        /// Method to create a LoggerHandle from a long.
        /// Use this method only if you cannot use LoggerHandle for some reason and must use the long
        /// </summary>
        /// <param name="knownId">Id that was taken from some LoggerHandle.Value, don't pass other longs</param>
        /// <returns>LoggerHandle that uses the id that was provided.</returns>
        public static LoggerHandle CreateUsingKnownId(uint knownId)
        {
            return new LoggerHandle(knownId);
        }

        /// <summary>
        /// True if Value is not 0.
        /// </summary>
        public readonly bool IsValid => Value != 0;

        /// <summary>
        /// Throws if this LoggerHandle is not valid.
        /// <seealso cref="IsValid"/>
        /// </summary>
        /// <exception cref="Exception">throws if this LoggerHandle is not valid</exception>
        public readonly void MustBeValid()
        {
            if (IsValid == false)
                throw new Exception("LoggerHandle must be valid, but it is not!");
        }
    }
}
