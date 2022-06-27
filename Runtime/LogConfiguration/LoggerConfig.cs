using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Logging.Sinks;

namespace Unity.Logging.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct HasSinkStruct
    {
        private int m_MinimalLogLevelAcrossAllSinks; // = LogLevel + 1, so 0 is not initialized struct, means no level is supported

        public static HasSinkStruct FromLogger(Logger logger)
        {
            var minLvl = 0;
            if (logger != null)
                minLvl = (int)logger.MinimalLogLevelAcrossAllSystems + 1;

            return new HasSinkStruct { m_MinimalLogLevelAcrossAllSinks = minLvl };
        }

        public static HasSinkStruct FromMinLogLevel(LogLevel m_MinimalLevel)
        {
            return new HasSinkStruct { m_MinimalLogLevelAcrossAllSinks = (int)m_MinimalLevel + 1 };
        }

        public bool Has(LogLevel level)
        {
            return m_MinimalLogLevelAcrossAllSinks != 0 && (int)level >= m_MinimalLogLevelAcrossAllSinks - 1;
        }
    }
}

namespace Unity.Logging
{
    /// <summary>
    /// Configuration for Logger. Can be used to setup Logger and create it.
    /// </summary>
    public class LoggerConfig
    {
        private FixedString512Bytes m_CurrentTemplate = "{Timestamp} | {Level} | {Message}";

        /// <summary>
        /// Set minimal <see cref="LogLevel"/> that will be processed by the <see cref="Logger"/>
        /// </summary>
        public readonly LoggerMinimumLevelConfig MinimumLevel;

        internal LogMemoryManagerParameters MemoryManagerParameters;
        internal readonly List<SinkConfiguration> SinkConfigs;

        /// <summary>
        /// Create <see cref="LoggerConfig"/> to setup a new <see cref="Logger"/>
        /// </summary>
        public LoggerConfig()
        {
            LogMemoryManagerParameters.GetDefaultParameters(out MemoryManagerParameters);
            MinimumLevel = new LoggerMinimumLevelConfig(this);
            SinkConfigs = new List<SinkConfiguration>(32);
        }

        /// <summary>
        /// Use this method to add new Sink
        /// </summary>
        public LoggerWriterConfig WriteTo => new LoggerWriterConfig(this);

        /// <summary>
        /// Template that should be used by sinks by default. All sinks without explicit OutputTemplate will use this one after this call.
        /// </summary>
        /// <param name="newTemplate">Template for the messages. Can use any strings and {Level}, {Timestamp}, {Message} as special holes.</param>
        /// <returns>Config to continue methods chain</returns>
        public LoggerConfig OutputTemplate(FixedString512Bytes newTemplate)
        {
            m_CurrentTemplate = newTemplate;
            return this;
        }

        /// <summary>
        /// Returns current template that is used. See also <see cref="OutputTemplate"/>
        /// </summary>
        /// <returns>Current template that is used</returns>
        public FixedString512Bytes GetOutputTemplate() => m_CurrentTemplate;

        /// <summary>
        /// Call that creates <see cref="Logger"/>
        /// </summary>
        /// <param name="logMemoryManagerParameters"><see cref="LogMemoryManagerParameters"/> to initialize <see cref="LogMemoryManager"/> with.</param>
        /// <returns>New <see cref="Logger"/> that is created using this configuration.</returns>
        public Logger CreateLogger(LogMemoryManagerParameters logMemoryManagerParameters)
        {
            MemoryManagerParameters = logMemoryManagerParameters;
            return new Logger(this);
        }

        /// <summary>
        /// Call that creates <see cref="Logger"/> using default <see cref="LogMemoryManagerParameters"/>
        /// </summary>
        /// <returns>New <see cref="Logger"/> that is created using this configuration.</returns>
        public Logger CreateLogger()
        {
            return new Logger(this);
        }
    }
}