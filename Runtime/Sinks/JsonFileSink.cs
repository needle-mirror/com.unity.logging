using System;
using Unity.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Extension class for LoggerWriterConfig .JsonFile
    /// </summary>
    public static class JsonFileSinkSystemExt
    {
        /// <summary>
        /// Write structured logs to the json file
        /// </summary>
        /// <param name="writeTo">Logger config</param>
        /// <param name="absFileName">Absolute file path to the log file</param>
        /// <param name="maxFileSizeBytes">Threshold of file size in bytes after which new file should be created (rolling). Default of 5 MB. Set to 0 MB if no rolling by file size is needed</param>
        /// <param name="maxRoll">Max amount of rolls after which old files will be rewritten</param>
        /// <param name="maxTimeSpan">Threshold of time after which new file should be created (rolling). 'default' if no rolling by time is needed</param>
        /// <param name="captureStackTrace">True if stack traces should be captured</param>
        /// <param name="minLevel">Minimal level of logs for this particular sink. Null if common level should be used</param>
        /// <param name="outputTemplate">Output message template for this particular sink. Null if common template should be used</param>
        /// <returns>Logger config</returns>
        public static LoggerConfig JsonFile(this LoggerWriterConfig writeTo,
                                            string absFileName,
                                            long maxFileSizeBytes = 5 * 1024 * 1024, int maxRoll = 15, TimeSpan maxTimeSpan = default,
                                            bool? captureStackTrace = null,
                                            LogLevel? minLevel = null,
                                            FixedString512Bytes? outputTemplate = null)
        {
            var config = new FileSinkSystem.Configuration(writeTo, absFileName, LogFormatterJson.Formatter, maxFileSizeBytes, maxRoll, maxTimeSpan, captureStackTrace, minLevel, outputTemplate)
            {
                GeneralConfig = FileSinkSystem.GeneralSinkConfiguration.JsonLines()
            };

            return writeTo.AddSinkConfig(config);
        }
    }
}
