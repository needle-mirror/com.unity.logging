using Unity.Collections;

namespace Unity.Logging.Sinks
{
    public abstract class SinkConfiguration
    {
        public LogLevel? MinLevelOverride;
        public FixedString512Bytes? OutputTemplateOverride;
        public bool CaptureStackTraces;
        public abstract ISinkSystemInterface CreateSinkInstance(in Logger logger);
    }

    public class SinkConfiguration<T> : SinkConfiguration where T : ISinkSystemInterface, new()
    {
        public override ISinkSystemInterface CreateSinkInstance(in Logger logger)
        {
            var sink = new T();
            sink.Initialize(logger, this);

            return sink;
        }
    }
}
