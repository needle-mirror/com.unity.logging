namespace Unity.Logging
{
    /// <summary>
    /// Use to set the synchronization mode of the logger
    /// </summary>
    public class LoggerSyncModeConfig
    {
        private SyncMode m_CurrentMode = SyncMode.FatalIsSync;
        private readonly LoggerConfig m_LoggerConfig;

        internal LoggerSyncModeConfig(LoggerConfig loggerConfig)
        {
            m_LoggerConfig = loggerConfig;
        }

        /// <summary>
        /// Returns currently set minimal level
        /// </summary>
        public SyncMode Get => m_CurrentMode;

        /// <summary>
        /// Sets minimal level of logs
        /// </summary>
        /// <param name="minLevel"></param>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig Set(SyncMode minLevel)
        {
            m_CurrentMode = minLevel;
            return m_LoggerConfig;
        }

        /// <summary>
        /// Sets SyncMode to <see cref="SyncMode.FatalIsSync"/><br/>
        /// All messages are processed asynchronous, after they were logged. But if Fatal message is logged - log is flushed.
        /// Use this option if you want speed, but also want to make sure logs are not lost in case of Fatal error
        /// </summary>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig FatalIsSync() => Set(SyncMode.FatalIsSync);

        /// <summary>
        /// Sets SyncMode to <see cref="SyncMode.FullAsync"/><br/>
        /// All messages are processed asynchronous, after they were logged.
        /// This is the fastest way, but can lead to lost messages in case of crashes
        /// </summary>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig FullAsync() => Set(SyncMode.FullAsync);

        /// <summary>
        /// Sets SyncMode to <see cref="SyncMode.FullSync"/><br/>
        /// All messages are processed immediately. Slowest mode, but all messages are guaranteed to be logged
        /// </summary>
        /// <returns>LoggerConfig for other settings to set</returns>
        public LoggerConfig FullSync() => Set(SyncMode.FullSync);
    }
}
