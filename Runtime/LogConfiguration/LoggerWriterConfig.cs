using System;
using Unity.Collections;
using Unity.Logging.Sinks;

namespace Unity.Logging
{
    /// <summary>
    /// Config for the sink creation
    /// </summary>
    public class LoggerWriterConfig
    {
        private readonly LoggerConfig m_Config;

        internal LoggerWriterConfig(LoggerConfig loggerConfig)
        {
            m_Config = loggerConfig;
        }

        /// <summary>
        /// Add new sink configuration to the <see cref="LoggerConfig"/>
        /// </summary>
        /// <param name="sinkConfig">Configuration for sink to create</param>
        /// <returns>Main logger config</returns>
        public LoggerConfig AddSinkConfig(SinkConfiguration sinkConfig)
        {
            m_Config.SinkConfigs.Add(sinkConfig);
            return m_Config;
        }

        public bool ResolveCaptureStackTrace(bool? captureStackTrace)
        {
            if (captureStackTrace == null)
                return m_Config.GetCaptureStacktrace();
            return captureStackTrace.Value;
        }

        public LogLevel ResolveMinLevel(LogLevel? minLevel)
        {
            if (minLevel == null)
                return m_Config.MinimumLevel.Get;
            return minLevel.Value;
        }

        public FixedString512Bytes ResolveOutputTemplate(FixedString512Bytes? outputTemplate)
        {
            if (outputTemplate == null)
                return m_Config.GetOutputTemplate();
            return outputTemplate.Value;
        }
    }
}
