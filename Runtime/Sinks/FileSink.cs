#if PLATFORM_SWITCH
#define FILESINK_IN_MEMORY
#endif

using Unity.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

#if FILESINK_IN_MEMORY
using FileOperations = Unity.Logging.Sinks.FileRollingLogic<Unity.Logging.Sinks.FileOperationsInMemory>;
#else
using FileOperations = Unity.Logging.Sinks.FileRollingLogic<Unity.Logging.Sinks.FileOperationsBaselib>;
#endif

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Extension class for LoggerWriterConfig .File
    /// </summary>
    public static class FileSinkSystemExt
    {
        /// <summary>
        /// Write logs to the file in a text form
        /// </summary>
        /// <param name="writeTo">Logger config</param>
        /// <param name="absFileName">Absolute file path to the log file</param>
        /// <param name="formatter">Formatter that should be used by this sink. Text is default</param>
        /// <param name="maxFileSizeBytes">Threshold of file size in bytes after which new file should be created (rolling). Default of 5 MB. Set to 0 MB if no rolling by file size is needed</param>
        /// <param name="maxRoll">Max amount of rolls after which old files will be rewritten</param>
        /// <param name="maxTimeSpan">Threshold of time after which new file should be created (rolling). 'default' if no rolling by time is needed</param>
        /// <param name="captureStackTrace">True if stack traces should be captured</param>
        /// <param name="minLevel">Minimal level of logs for this particular sink. Null if common level should be used</param>
        /// <param name="outputTemplate">Output message template for this particular sink. Null if common template should be used</param>
        /// <returns>Logger config</returns>
        public static LoggerConfig File(this LoggerWriterConfig writeTo, string absFileName,
                                        FormatterStruct formatter = default,
                                        long maxFileSizeBytes = 5 * 1024 * 1024, int maxRoll = 15, TimeSpan maxTimeSpan = default,
                                        bool? captureStackTrace = null,
                                        LogLevel? minLevel = null,
                                        FixedString512Bytes? outputTemplate = null)
        {
            return writeTo.AddSinkConfig(new FileSinkSystem.Configuration(writeTo, absFileName, formatter, maxFileSizeBytes, maxRoll, maxTimeSpan, captureStackTrace, minLevel, outputTemplate));
        }
    }

    /// <summary>
    /// File sink class
    /// </summary>
    [BurstCompile]
    public class FileSinkSystem : SinkSystemBase
    {
        /// <summary>
        /// General configuration
        /// </summary>
        public struct GeneralSinkConfiguration
        {
            /// <summary>
            /// String that should be added to any file at the beginning
            /// </summary>
            public FixedString4096Bytes Prefix;

            /// <summary>
            /// String that should be added between each logging message
            /// </summary>
            public FixedString64Bytes Separator;

            /// <summary>
            /// String that should be added to any file at the end
            /// </summary>
            public FixedString4096Bytes Postfix;

            /// <summary>
            /// Whole file is a json array, elements separated with comma
            /// </summary>
            /// <returns>GeneralSinkConfiguration that describes JSON array setup</returns>>
            public static GeneralSinkConfiguration JsonArray()
            {
                return new GeneralSinkConfiguration
                {
                    Prefix = "[",
                    Separator = ",",
                    Postfix = "]"
                };
            }

            /// <summary>
            /// Each line is a JSON object
            /// </summary>
            /// <returns>GeneralSinkConfiguration that describes JSON lines setup</returns>>
            public static GeneralSinkConfiguration JsonLines()
            {
                return new GeneralSinkConfiguration
                {
                    Prefix = "",
                    Separator = "",
                    Postfix = ""
                };
            }
        }

        /// <summary>
        /// Current file state
        /// </summary>
        public struct CurrentFileConfiguration
        {
            /// <summary>
            /// Absolute file path, without extension
            /// </summary>
            public FixedString4096Bytes AbsFileName;
            /// <summary>
            /// File extension
            /// </summary>
            public FixedString32Bytes FileExt;
        }

        /// <summary>
        /// Current rolling file state
        /// </summary>
        public struct RollingFileConfiguration
        {
            /// <summary>
            /// Max file size in bytes that is allowed. 0 if no rolling should occur on the size of file
            /// </summary>
            public long MaxFileSizeBytes;
            /// <summary>
            /// Max time span for a file to be opened. Default if no rolling should occur on the time
            /// </summary>
            public TimeSpan MaxTimeSpan;
            /// <summary>
            /// Max number of rolls
            /// </summary>
            public int MaxRoll;
        }

        /// <summary>
        /// Configuration for file sink
        /// </summary>
        public class Configuration : SinkConfiguration
        {
            /// <summary>
            /// Instance of <see cref="GeneralSinkConfiguration"/>
            /// </summary>
            public GeneralSinkConfiguration GeneralConfig;

            /// <summary>
            /// Instance of <see cref="CurrentFileConfiguration"/>
            /// </summary>
            public CurrentFileConfiguration CurrentFileConfig;

            /// <summary>
            /// Instance of <see cref="RollingFileConfiguration"/>
            /// </summary>
            public RollingFileConfiguration RollingFileConfig;

            /// <summary>
            /// Creates the FileSink
            /// </summary>
            /// <param name="logger">Logger that owns sink</param>
            /// <returns>SinkSystemBase</returns>
            public override SinkSystemBase CreateSinkInstance(Logger logger) => CreateAndInitializeSinkInstance<FileSinkSystem>(logger, this);

            /// <summary>
            /// Write logs to the file in a text form
            /// </summary>
            /// <param name="writeTo">Logger config</param>
            /// <param name="absFileName">Absolute file path to the log file</param>
            /// <param name="formatter">Formatter that should be used by this sink. Text is default</param>
            /// <param name="maxFileSizeBytes">Threshold of file size in bytes after which new file should be created (rolling). Default of 5 MB. Set to 0 MB if no rolling by file size is needed</param>
            /// <param name="maxRoll">Max amount of rolls after which old files will be rewritten</param>
            /// <param name="maxTimeSpan">Threshold of time after which new file should be created (rolling). 'default' if no rolling by time is needed</param>
            /// <param name="captureStackTrace">True if stack traces should be captured</param>
            /// <param name="minLevel">Minimal level of logs for this particular sink. Null if common level should be used</param>
            /// <param name="outputTemplate">Output message template for this particular sink. Null if common template should be used</param>
            public Configuration(LoggerWriterConfig writeTo,
                                 string absFileName,
                                 FormatterStruct formatter = default,
                                 long maxFileSizeBytes = 5 * 1024 * 1024, int maxRoll = 15, TimeSpan maxTimeSpan = default,
                                 bool? captureStackTrace = null,
                                 LogLevel? minLevel = null,
                                 FixedString512Bytes? outputTemplate = null) : base(writeTo, formatter, captureStackTrace, minLevel, outputTemplate)
            {
                if (formatter.IsCreated == false)
                    formatter = LogFormatterText.Formatter;
                LogFormatter = formatter;

                FileUtils.MakeSureDirectoryExistsForFile(absFileName);

                var ext = FileUtils.GetExtension(absFileName);
                if (string.IsNullOrEmpty(ext) == false)
                {
                    absFileName = absFileName.Substring(0, absFileName.Length - ext.Length);
                }

                GeneralConfig = default;
                CurrentFileConfig = new FileSinkSystem.CurrentFileConfiguration
                {
                    AbsFileName = absFileName,
                    FileExt = ext
                };
                RollingFileConfig = new FileSinkSystem.RollingFileConfiguration
                {
                    MaxFileSizeBytes = maxFileSizeBytes,
                    MaxRoll = maxRoll,
                    MaxTimeSpan = maxTimeSpan
                };
            }
        }

        /// <summary>
        /// True if can access real file system, false if the virtual one
        /// </summary>
        public static bool HasFileAccess
        {
            get
            {
#if FILESINK_IN_MEMORY
                return false;
#else
                return true;
#endif
            }
        }

        internal static FileOperations CreateFileOperations() => new FileOperations();
        internal static FileOperations CreateFileOperations(IntPtr userData) => new FileOperations(userData);

        /// <summary>
        /// Creates <see cref="LogController.SinkStruct"/>
        /// </summary>
        /// <returns>SinkStruct</returns>
        public override LogController.SinkStruct ToSinkStruct()
        {
            var userData = CreateFileOperations();
            var config = (Configuration)SystemConfig;

            IsInitialized = userData.OpenFileForLogging(ref config);

            if (IsInitialized == false)
            {
                OnSinkFatalError($"Cannot open file '{config.CurrentFileConfig.AbsFileName}{config.CurrentFileConfig.FileExt}' for write");
                return default;
            }

            var s = base.ToSinkStruct();
            s.OnLogMessageEmit = new OnLogMessageEmitDelegate(OnLogMessageEmitFunc);
            //s.OnAfterSink = new OnAfterSinkDelegate(OnAfterFunc);
            s.OnDispose = new OnDisposeDelegate(OnDisposeFunc);
            s.UserData = userData.GetPointer();
            return s;
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(OnLogMessageEmitDelegate.Delegate))]
        internal static void OnLogMessageEmitFunc(in LogMessage logEvent, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator)
        {
            WriteToFile(userData, ref messageBuffer);
        }

        internal static unsafe void WriteToFile(IntPtr userData, ref UnsafeText messageBuffer)
        {
            try
            {
                var fileOps = CreateFileOperations(userData);

                var data = messageBuffer.GetUnsafePtr();
                var newLine = true;
                fileOps.Append(data, (ulong)messageBuffer.Length, newLine);
            }
            finally
            {
                messageBuffer.Length = 0;
            }
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(OnAfterSinkDelegate.Delegate))]
        internal static void OnAfterFunc(IntPtr userData)
        {
            var fileOps = CreateFileOperations(userData);
            fileOps.Flush();
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(OnDisposeDelegate.Delegate))]
        private static void OnDisposeFunc(IntPtr userData)
        {
            var fileOps = CreateFileOperations(userData);
            fileOps.Dispose();
        }
    }

    [BurstCompile]
    [GenerateTestsForBurstCompatibility]
    internal static class FileUtils
    {
        public static void MakeSureDirectoryExistsForFile(string fileName)
        {
#if !NET_DOTS && !PLATFORM_SWITCH
            try
            {
                var dirPath = Path.GetDirectoryName(fileName);
                if (string.IsNullOrEmpty(dirPath) == false && Directory.Exists(dirPath) == false)
                    Directory.CreateDirectory(dirPath);
            }
            catch
            {
                // ignored
            }
#endif
        }

        public static string GetExtension(string fileName)
        {
            var n = fileName.Length;
            for (var i = n - 1; i >= 0; i--)
            {
                var c = fileName[i];
                if (c == '.')
                {
                    if (i != n - 1)
                        return fileName.Substring(i, n - i);

                    return "";
                }

                if (c == '/' || c == '\\' || c == ':')
                    return "";
            }

            return "";
        }
    }
}
