namespace Unity.Logging.Tests
{
    public readonly struct TemplateParsedMessage
    {
        public readonly long timestamp;
        public readonly LogLevel level;
        public readonly string message;

        public TemplateParsedMessage(long t, LogLevel l, string m)
        {
            timestamp = t;
            level = l;
            message = m;
        }
    }
}
