#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging;
using Unity.Logging.Internal.Debug;
using Unity.Logging.Sinks;
using UnityEngine.Assertions;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// Extension class for LoggerWriterConfig .StringLogger
    /// </summary>
    public static class StringLoggerSinkExt
    {
        /// <summary>
        /// Write logs to the string in a text form
        /// </summary>
        /// <param name="writeTo">Logger config</param>
        /// <param name="formatter">Formatter that should be used by this sink. Text is default</param>
        /// <param name="captureStackTrace">True if stack traces should be captured</param>
        /// <param name="minLevel">Minimal level of logs for this particular sink. Null if common level should be used</param>
        /// <param name="outputTemplate">Output message template for this particular sink. Null if common template should be used</param>
        /// <returns>Logger config</returns>
        public static LoggerConfig StringLogger(this LoggerWriterConfig writeTo,
                                                FormatterStruct formatter = default,
                                                bool? captureStackTrace = null,
                                                LogLevel? minLevel = null,
                                                FixedString512Bytes? outputTemplate = null)
        {
            if (formatter.IsCreated == false)
                formatter = LogFormatterText.Formatter;

            return writeTo.AddSinkConfig(new StringSink.Configuration(writeTo, formatter, captureStackTrace, minLevel, outputTemplate));
        }
    }

    /// <summary>
    /// String sink class
    /// </summary>
    [BurstCompile]
    public class StringSink : SinkSystemBase
    {
        /// <summary>
        /// Configuration for string sink
        /// </summary>
        public class Configuration : SinkConfiguration
        {
            /// <summary>
            /// Creates the StringSink
            /// </summary>
            /// <param name="logger">Logger that owns sink</param>
            /// <returns>SinkSystemBase</returns>
            public override SinkSystemBase CreateSinkInstance(Logger logger) => CreateAndInitializeSinkInstance<StringSink>(logger, this);

            /// <summary>
            /// Constructor for the configuration
            /// </summary>
            /// <param name="writeTo">Logger config</param>
            /// <param name="formatter">Formatter that should be used by this sink. Text is default</param>
            /// <param name="captureStackTraceOverride">True if stack traces should be captured. Null if default</param>
            /// <param name="minLevelOverride">Minimal level of logs for this particular sink. Null if common level should be used</param>
            /// <param name="outputTemplateOverride">Output message template for this particular sink. Null if common template should be used</param>
            public Configuration(LoggerWriterConfig writeTo, FormatterStruct formatter,
                                 bool? captureStackTraceOverride = null, LogLevel? minLevelOverride = null, FixedString512Bytes? outputTemplateOverride = null)
                : base(writeTo, formatter, captureStackTraceOverride, minLevelOverride, outputTemplateOverride)
            {}
        }

        private int m_StringBuilderId;

        /// <summary>
        /// Creates <see cref="LogController.SinkStruct"/>
        /// </summary>
        /// <returns>SinkStruct</returns>
        public override LogController.SinkStruct ToSinkStruct()
        {
            var s = base.ToSinkStruct();
            s.OnLogMessageEmit = new OnLogMessageEmitDelegate(OnLogMessageEmitFunc);
            s.UserData = new IntPtr(m_StringBuilderId);
            return s;
        }

        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(OnLogMessageEmitDelegate.Delegate))]
        internal static void OnLogMessageEmitFunc(in LogMessage logEvent, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator)
        {
            unsafe
            {
                try
                {
                    var data = messageBuffer.GetUnsafePtr();
                    var newLine = true;

                    var id = userData.ToInt32();

                    UnManagedMemoryLogHandler.Write(id, data, messageBuffer.Length, newLine);
                }
                finally
                {
                    messageBuffer.Length = 0;
                }
            }
        }

        /// <summary>
        /// Initialization of the sink using <see cref="Logger"/> and <see cref="SinkConfiguration"/> of this Sink
        /// </summary>
        /// <param name="logger">Logger that owns the sink</param>
        /// <param name="systemConfig">Configuration</param>
        public override void Initialize(Logger logger, SinkConfiguration systemConfig)
        {
            m_StringBuilderId = UnManagedMemoryLogHandler.Create();
            base.Initialize(logger, systemConfig);
        }

        /// <summary>
        /// Dispose the sink
        /// </summary>
        public override void Dispose()
        {
            UnManagedMemoryLogHandler.Dispose(m_StringBuilderId);
            base.Dispose();
        }

        /// <summary>
        /// Get everything that was written as a string
        /// </summary>
        /// <returns>String with all output in the sink</returns>
        public string GetString()
        {
            return UnManagedMemoryLogHandler.FlushOutput(m_StringBuilderId);
        }

        internal void WriteText(string contents)
        {
            UnManagedMemoryLogHandler.WriteText(m_StringBuilderId, contents);
        }

        /// <summary>
        /// Debug assertion that makes sure there is no StringSinks
        /// </summary>
        public static void AssertNoSinks()
        {
            UnManagedMemoryLogHandler.AssertNoSinks();
        }
    }

    internal static class UnManagedMemoryLogHandler
    {
        private static bool IsInitialized;

        // TODO: change to UnsafeHashMap when collections bug is fixed

        struct UniqIdKey{}
        static readonly SharedStatic<int> sbUniq = SharedStatic<int>.GetOrCreate<int, UniqIdKey>(16);
        static readonly SharedStatic<UnsafeParallelHashMap<int, ParallelText>> sb = SharedStatic<UnsafeParallelHashMap<int, ParallelText>>.GetOrCreate<UnsafeParallelHashMap<int, ParallelText>>(16);

        struct SbLockKey{}
        static readonly SharedStatic<long> sbLock = SharedStatic<long>.GetOrCreate<long, SbLockKey>(16);
        struct SbReaderKey{}
        static readonly SharedStatic<long> sbReader = SharedStatic<long>.GetOrCreate<long, SbReaderKey>(16);


        struct ParallelText : IDisposable
        {
            private SpinLockExclusive m_Lock;
            private UnsafeText m_Text;

            public ParallelText(int capacity, Allocator allocator)
            {
                m_Lock = new SpinLockExclusive(allocator);
                m_Text = new UnsafeText(capacity, allocator);
            }

            private int ExactLengthOfUtf16(ref UnsafeText text)
            {
                unsafe
                {
                    var utf8Buffer = text.GetUnsafePtr();
                    var utf8Length = text.Length;
                    var utf16Length = 0;
                    for (var utf8Offset = 0; utf8Offset < utf8Length;)
                    {
                        Unity.Collections.Unicode.Utf8ToUcs(out var ucs, utf8Buffer, ref utf8Offset, utf8Length);

                        int tempUtf16Symbol;
                        int utf16SymbolSize = 0;
                        char* ptr = (char*)&tempUtf16Symbol;

                        Unity.Collections.Unicode.UcsToUtf16(ptr, ref utf16SymbolSize, 2, ucs);
                        utf16Length += utf16SymbolSize;
                    }

                    return utf16Length;
                }
            }

            public string ToStringAndClear()
            {
                using (var l = new SpinLockExclusive.ScopedLock(m_Lock))
                {
                    string res = "";

#if !UNITY_DOTSRUNTIME
                    var utf16Size = ExactLengthOfUtf16(ref m_Text);
                    res = string.Create(utf16Size, m_Text, (chars, text) =>
                    {
                        unsafe
                        {
                            fixed (char* c = chars) {
                                Unicode.Utf8ToUtf16(text.GetUnsafePtr(), text.Length, c, out var length, chars.Length);
                            }
                        }
                    });
#else
                    // remove this block when dots runtime moves to .net 6 / 2.1 standard
                    try
                    {

                        unsafe
                        {
                            static string ConvertToUtf16String<T>(char* dst, ref T fs, int utf16Capacity) where T : IUTF8Bytes, INativeList<byte>
                            {
                                Unicode.Utf8ToUtf16(fs.GetUnsafePtr(), fs.Length, dst, out var length, utf16Capacity);

                                return new string(dst, 0, length);
                            }

                            var utf16WorstSize = m_Text.Length * 2;
                            var shouldStackalloc = utf16WorstSize <= 4096;

                            if (shouldStackalloc)
                            {
                                var c = stackalloc char[utf16WorstSize];
                                res = ConvertToUtf16String(c, ref m_Text, utf16WorstSize);
                            }
                            else
                            {
                                var arr = new char[utf16WorstSize];
                                fixed (char* c = arr)
                                {
                                    res = ConvertToUtf16String(c, ref m_Text, utf16WorstSize);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogException(e);
                    }
#endif
                    m_Text.Clear();
                    return res;
                }
            }

            public unsafe void Append(byte* data, int length, bool newLine)
            {
                using (var l = new SpinLockExclusive.ScopedLock(m_Lock))
                {
                    m_Text.Append(data, length);
                    if (newLine)
                    {
                        ref var newLineChar = ref Builder.EnvNewLine.Data;
                        m_Text.Append(newLineChar);
                    }
                }
            }

            public void Dispose()
            {
                m_Lock.Dispose();
                m_Text.Dispose();
            }
        }

        internal static void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            sbLock.Data = 0;
            sbReader.Data = 0;
            sbUniq.Data = 0;
            sb.Data = new UnsafeParallelHashMap<int, ParallelText>(64, Allocator.Persistent);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
        private static void CheckIsValid(int id)
        {
            if (id <= 0)
                throw new InvalidOperationException("To call this operation id must be valid");
        }

        public static int Create()
        {
            Initialize();

            try
            {
                BurstSpinLockReadWriteFunctions.EnterExclusive(ref sbLock.Data, ref sbReader.Data);
                for (var guard = 0; guard < 999; guard++)
                {
                    var id = ++sbUniq.Data;

                    if (sb.Data.ContainsKey(id))
                        continue;

                    sb.Data[id] = new ParallelText(4096, Allocator.Persistent);

                    return id;
                }
            }
            finally
            {
                BurstSpinLockReadWriteFunctions.ExitExclusive(ref sbLock.Data);
            }

            return 0;
        }

        // not burst
        public static string FlushOutput(int id)
        {
            CheckIsValid(id);

            try
            {
                BurstSpinLockReadWriteFunctions.EnterRead(ref sbLock.Data, ref sbReader.Data);
                if (sb.Data.TryGetValue(id, out var unsafeText))
                {
                    var res = unsafeText.ToStringAndClear();
                    sb.Data[id] = unsafeText;
                    return res;
                }
            }
            finally
            {
                BurstSpinLockReadWriteFunctions.ExitRead(ref sbReader.Data);
            }


            throw new Exception(id + " is not found! FlushOutput");
        }

        // called from burst or not burst
        public static unsafe void Write(int id, byte* data, int length, bool newLine)
        {
            CheckIsValid(id);

            try
            {
                BurstSpinLockReadWriteFunctions.EnterRead(ref sbLock.Data, ref sbReader.Data);
                if (sb.Data.TryGetValue(id, out var unsafeText))
                {
                    unsafeText.Append(data, length, newLine);

                    sb.Data[id] = unsafeText;
                    return;
                }
            }
            finally
            {
                BurstSpinLockReadWriteFunctions.ExitRead(ref sbReader.Data);
            }

            SelfLog.Error(FixedString.Format("{0} was not found in WriteFunc", id));
        }

        public static void WriteText(int id, string contents)
        {
            var bytes= Encoding.UTF8.GetBytes(contents);

            unsafe
            {
                fixed (byte* p = &bytes[0])
                {
                    Write(id, p, bytes.Length, false);
                }
            }
        }

        public static void Dispose(int id)
        {
            CheckIsValid(id);

            try
            {
                BurstSpinLockReadWriteFunctions.EnterExclusive(ref sbLock.Data, ref sbReader.Data);
                if (sb.Data.TryGetValue(id, out var unsafeText))
                {
                    unsafeText.Dispose();
                    sb.Data.Remove(id);
                }
            }
            finally
            {
                BurstSpinLockReadWriteFunctions.ExitExclusive(ref sbLock.Data);
            }
        }

        public static void AssertNoSinks()
        {
            if (sb.Data.IsCreated)
            {
                try
                {
                    BurstSpinLockReadWriteFunctions.EnterRead(ref sbLock.Data, ref sbReader.Data);

                    var s = "";
                    foreach (var ids in sb.Data.GetKeyArray(Allocator.Temp))
                    {
                        s += ids + " ";
                    }
                    Assert.AreEqual(0, sb.Data.Count(), s);
                }
                finally
                {
                    BurstSpinLockReadWriteFunctions.ExitRead(ref sbReader.Data);
                }
            }
        }
    }
}
