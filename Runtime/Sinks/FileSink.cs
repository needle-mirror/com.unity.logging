#if UNITY_DOTSRUNTIME
#define USE_BASELIB
#define USE_BASELIB_FILEIO
#endif

// #if PLATFORM_SWITCH
// #define FILESINK_IN_MEMORY
// #endif

using Unity.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

#if !USE_BASELIB_FILEIO
using System.Threading.Tasks;
using UnityEngine.Assertions;
#endif

#if FILESINK_IN_MEMORY
using FileOperations = Unity.Logging.Sinks.FileRollingLogic<Unity.Logging.Sinks.FileOperationsInMemory>;
#elif USE_BASELIB_FILEIO
using FileOperations = Unity.Logging.Sinks.FileRollingLogic<Unity.Logging.Sinks.FileOperationsBaselib>;
#else
using FileOperations = Unity.Logging.Sinks.FileRollingLogic<Unity.Logging.Sinks.FileOperationsFileStream>;
#endif

namespace Unity.Logging.Sinks
{
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

    [BurstCompile]
    public class FileSinkSystem : SinkSystemBase
    {
        public struct GeneralSinkConfiguration
        {
            public FixedString4096Bytes Prefix;
            public FixedString64Bytes Separator;
            public FixedString4096Bytes Postfix;

            /// <summary>
            /// Whole file is a json array, elements separated with comma
            /// </summary>
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

        public struct CurrentFileConfiguration
        {
            public FixedString4096Bytes AbsFileName;
            public FixedString32Bytes FileExt;
        }

        public struct RollingFileConfiguration
        {
            public long MaxFileSizeBytes;
            public TimeSpan MaxTimeSpan;
            public int MaxRoll;
        }

        public class Configuration : SinkConfiguration
        {
            public GeneralSinkConfiguration GeneralConfig;
            public CurrentFileConfiguration CurrentFileConfig;
            public RollingFileConfiguration RollingFileConfig;

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
            /// <returns>Logger config</returns>
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
#if !NET_DOTS
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
