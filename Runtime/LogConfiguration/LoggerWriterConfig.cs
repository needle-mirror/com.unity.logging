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

        /// <summary>
        /// Resolves default setting for stack trace capture
        /// </summary>
        /// <param name="captureStackTrace">If this is null - default config's setting is used</param>
        /// <returns>Returns capture stack trace setting for this config</returns>
        public bool ResolveCaptureStackTrace(bool? captureStackTrace)
        {
            if (captureStackTrace == null)
                return m_Config.GetCaptureStacktrace();
            return captureStackTrace.Value;
        }

        /// <summary>
        /// Resolves default setting for minimal log level
        /// </summary>
        /// <param name="minLevel">If this is null - default config's setting is used</param>
        /// <returns>Returns minimal logging message setting for this config</returns>
        public LogLevel ResolveMinLevel(LogLevel? minLevel)
        {
            if (minLevel == null)
                return m_Config.MinimumLevel.Get;
            return minLevel.Value;
        }

        /// <summary>
        /// Resolves default setting for output template
        /// </summary>
        /// <param name="outputTemplate">If this is null - default config's setting is used</param>
        /// <returns>Returns output template setting for this config</returns>
        public FixedString512Bytes ResolveOutputTemplate(FixedString512Bytes? outputTemplate)
        {
            if (outputTemplate == null)
                return m_Config.GetOutputTemplate();
            return outputTemplate.Value;
        }
    }
}
