using Unity.Collections;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// SinkConfiguration stores all the data needed for Sink to work and what is most important - knows how to create and initialize the sink.
    /// </summary>
    public abstract class SinkConfiguration
    {
        /// <summary>
        /// Minimal level that sink will log.
        /// </summary>
        public LogLevel MinLevel;

        /// <summary>
        /// Output template for the message. Ignored in structured logging case.
        /// </summary>
        public FixedString512Bytes OutputTemplate;

        /// <summary>
        /// True if this logger needs stack traces.
        /// </summary>
        public bool CaptureStackTraces;

        /// <summary>
        /// Formatter that controls how to represent the log message: plain text, json, etc..
        /// </summary>
        public FormatterStruct LogFormatter;

        /// <summary>
        /// Base sink configuration constructor.
        /// </summary>
        /// <param name="writeTo">Logger config</param>
        /// <param name="formatter">Formatter that should be used by this sink. Text is default</param>
        /// <param name="captureStackTraceOverride">True if stack traces should be captured. Null if common setting should be used</param>
        /// <param name="minLevelOverride">Minimal level of logs for this particular sink. Null if common level should be used</param>
        /// <param name="outputTemplateOverride">Output message template for this particular sink. Null if common template should be used</param>
        protected SinkConfiguration(LoggerWriterConfig writeTo, FormatterStruct formatter, bool? captureStackTraceOverride = null, LogLevel? minLevelOverride = null,
                                    FixedString512Bytes? outputTemplateOverride = null)
        {
            CaptureStackTraces = writeTo.ResolveCaptureStackTrace(captureStackTraceOverride);
            MinLevel = writeTo.ResolveMinLevel(minLevelOverride);
            OutputTemplate = writeTo.ResolveOutputTemplate(outputTemplateOverride);
            LogFormatter = formatter;
        }

        /// <summary>
        /// Function that creates specific SinkSystem using this configuration.
        /// </summary>
        /// <param name="logger">Parent Logger</param>
        /// <returns>Specific SinkSystem that is going to be added to the Logger</returns>
        public abstract SinkSystemBase CreateSinkInstance(Logger logger);

        /// <summary>
        /// Standard way of creation and init the sink
        /// </summary>
        /// <param name="logger">Parent Logger</param>
        /// <param name="configuration">Configuration that stores all the data needed for the init</param>
        /// <typeparam name="T">SinkSystemBase</typeparam>
        /// <returns>Specific SinkSystemBase sink instance, already Initialized</returns>
        public static T CreateAndInitializeSinkInstance<T>(Logger logger, SinkConfiguration configuration) where T : SinkSystemBase, new()
        {
            var sink = new T();
            sink.Initialize(logger, configuration);
            return sink;
        }
    }
}
