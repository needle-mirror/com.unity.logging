using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Logging.Internal;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Structure that controls rolling logic for log files
    /// Max size and max time duration are supported
    /// </summary>
    internal struct RollStruct
    {
        private long m_OpenDateTime;
        private int m_Roll;
        private int m_MaxRoll;
        private long m_MaxBytes;
        private TimeSpan m_MaxTimeSpan;

        public bool ShouldRollOnSize => m_MaxBytes > 0;
        public bool ShouldRollOnTime => m_MaxTimeSpan != TimeSpan.Zero;

        public static RollStruct Create(ref FileSinkSystem.RollingFileConfiguration rollingConfig)
        {
            return new RollStruct
            {
                m_MaxRoll = rollingConfig.MaxRoll,
                m_MaxBytes = rollingConfig.MaxFileSizeBytes,
                m_MaxTimeSpan = rollingConfig.MaxTimeSpan,
                m_OpenDateTime = TimeStampWrapper.GetTimeStamp(),
                m_Roll = 0
            };
        }

        public FixedString4096Bytes RollFileAbsPath(ref FileSinkSystem.CurrentFileConfiguration fileConfig)
        {
            return RollFileAbsPath(ref fileConfig.AbsFileName, ref fileConfig.FileExt);
        }

        public FixedString4096Bytes RollFileAbsPath(ref FixedString4096Bytes filename, ref FixedString32Bytes filenameExt)
        {
            FixedString64Bytes openDateTime = "";

            if (ShouldRollOnTime)
            {
                openDateTime = TimeStampWrapper.GetFormattedTimeStampStringForFileName(m_OpenDateTime);
            }

            var result = filename;

            if (openDateTime.IsEmpty)
            {
                if (m_MaxRoll > 0 && m_Roll > 0)
                {
                    result.Append('_');
                    result.Append(m_Roll);
                }
            }
            else
            {
                result.Append('_');
                result.Append(openDateTime);
            }

            result.Append(filenameExt);

            return result;
        }

        public bool ShouldRoll(long fileStreamLength)
        {
            // size check
            if (ShouldRollOnSize && fileStreamLength > m_MaxBytes)
                return true;

            if (ShouldRollOnTime)
            {
                var msecSinceFileOpen = TimeStampWrapper.TotalMillisecondsSince(m_OpenDateTime);

                // time check
                if (msecSinceFileOpen > m_MaxTimeSpan.TotalMilliseconds)
                    return true;
            }

            return false;
        }

        public void Roll()
        {
            m_OpenDateTime = TimeStampWrapper.GetTimeStamp();
            ++m_Roll;
            if (m_MaxRoll > 0 && m_Roll >= m_MaxRoll)
                m_Roll = 0;
        }
    }
}
