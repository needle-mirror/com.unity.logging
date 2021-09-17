namespace Unity.Logging
{
    /// <summary>
    /// Use to set the minimum log level that you want to use.
    /// </summary>
    public class LoggerMinimumLevelConfig
    {
        private LogLevel m_CurrentMinLevel = LogLevel.Info;
        private readonly LoggerConfig m_LoggerConfig;

        internal LoggerMinimumLevelConfig(LoggerConfig loggerConfig)
        {
            m_LoggerConfig = loggerConfig;
        }

        /// <summary>
        /// Returns currently set minimal level
        /// </summary>
        public LogLevel Get => m_CurrentMinLevel;

        /// <summary>
        /// Sets minimal level of logs
        /// </summary>
        /// <param name="minLevel"></param>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig Set(LogLevel minLevel)
        {
            m_CurrentMinLevel = minLevel;
            return m_LoggerConfig;
        }

        /// <summary>
        /// Sets minimal level to <see cref="LogLevel.Verbose"/>
        /// </summary>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig Verbose() => Set(LogLevel.Verbose);

        /// <summary>
        /// Sets minimal level to <see cref="LogLevel.Debug"/>
        /// </summary>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig Debug() => Set(LogLevel.Debug);

        /// <summary>
        /// Sets minimal level to <see cref="LogLevel.Info"/>
        /// </summary>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig Info() => Set(LogLevel.Info);

        /// <summary>
        /// Sets minimal level to <see cref="LogLevel.Warning"/>
        /// </summary>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig Warning() => Set(LogLevel.Warning);

        /// <summary>
        /// Sets minimal level to <see cref="LogLevel.Error"/>
        /// </summary>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig Error() => Set(LogLevel.Error);

        /// <summary>
        /// Sets minimal level to <see cref="LogLevel.Fatal"/>
        /// </summary>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig Fatal() => Set(LogLevel.Fatal);
    }
}
