using System;
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
            if (sinkConfig.MinLevelOverride == null)
                sinkConfig.MinLevelOverride = m_Config.MinimumLevel.Get;
            if (sinkConfig.OutputTemplateOverride == null)
                sinkConfig.OutputTemplateOverride = m_Config.GetOutputTemplate();

            m_Config.SinkConfigs.Add(sinkConfig);
            return m_Config;
        }
    }
}
