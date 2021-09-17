#if UNITY_DOTSRUNTIME
#define USE_BASELIB
#define USE_BASELIB_FILEIO
#endif

using Unity.Logging;
using Unity.Logging.Sinks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Logging.Internal;

#if USE_BASELIB_FILEIO
using Unity.Collections.LowLevel.Unsafe;
using static Unity.Baselib.LowLevel.Binding;

[assembly: RegisterGenericJobType(typeof(SinkJob<FileSinkLogger<FileOperationsBaselib>>))]
#else
using System.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

[assembly: RegisterGenericJobType(typeof(SinkJob<FileSinkLogger<FileOperationsFileStream>>))]
#endif


namespace Unity.Logging.Sinks
{
    [BurstCompile]
    [BurstCompatible]
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

#if USE_BASELIB_FILEIO
    [BurstCompatible(GenericTypeArguments = new [] {typeof(FileOperationsBaselib)})]
#else
    [BurstCompatible(GenericTypeArguments = new [] {typeof(FileOperationsFileStream)})]
#endif
    [BurstCompile]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FileSinkLogger<TWriter> : ILogger where TWriter : struct, IFileOperations
    {
        internal TWriter fileOps;

        public void OnLogMessage(in LogMessage logEvent, in FixedString512Bytes outTemplate, ref LogMemoryManager memoryManager)
        {
            var message = TextLoggerParser.ParseMessageTemplate(logEvent, outTemplate, ref memoryManager);
            if (message.IsCreated)
            {
                try
                {
                    unsafe
                    {
                        var data = message.GetUnsafePtr();
                        var length = message.Length;
                        var newLine = true;
                        fileOps.Append(data, (ulong)length, newLine);
                    }
                }
                finally
                {
                    message.Dispose();
                }
            }
        }
    }


    internal class FileSinkSystemConfig : SinkConfiguration<FileSinkSystem>
    {
        public FixedString512Bytes FileName;
        public FixedString32Bytes FileExt;
        public long MaxFileSizeBytes;
        public TimeSpan MaxTimeSpan;
        public int MaxRoll;
    }

    public static class FileSinkSystemExt
    {
        /// <summary>
        /// Write logs to the file in a text form
        /// </summary>
        /// <param name="writeTo">Logger config</param>
        /// <param name="fileName">Absolute file path to the log file</param>
        /// <param name="maxFileSizeBytes">Threshold of file size in bytes after which new file should be created (rolling). 0 if no rolling by file size is needed</param>
        /// <param name="maxRoll">Max amount of rolls after which old files will be rewritten</param>
        /// <param name="maxTimeSpan">Threshold of time after which new file should be created (rolling). 'default' if no rolling by time is needed</param>
        /// <param name="captureStackTrace">True if stack traces should be captured</param>
        /// <param name="minLevel">Minimal level of logs for this particular sink. Null if common level should be used</param>
        /// <param name="outputTemplate">Output message template for this particular sink. Null if common template should be used</param>
        /// <returns>Logger config</returns>
        public static LoggerConfig File(this LoggerWriterConfig writeTo, string fileName, long maxFileSizeBytes = 0, int maxRoll = 15, TimeSpan maxTimeSpan = default, bool captureStackTrace = false, LogLevel? minLevel = null, FixedString512Bytes? outputTemplate = null)
        {
            FileUtils.MakeSureDirectoryExistsForFile(fileName);

            var ext = FileUtils.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext) == false)
            {
                fileName = fileName.Substring(0, fileName.Length - ext.Length);
            }

            return writeTo.AddSinkConfig(new FileSinkSystemConfig
            {
                CaptureStackTraces = captureStackTrace,
                FileName = fileName,
                FileExt = ext,
                MaxFileSizeBytes = maxFileSizeBytes,
                MinLevelOverride = minLevel,
                OutputTemplateOverride = outputTemplate,
                MaxRoll = maxRoll,
                MaxTimeSpan = maxTimeSpan
            });
        }
    }

#if USE_BASELIB_FILEIO
    public class FileSinkSystem : FileSinkSystemBase<FileOperationsBaselib> {}
#else
    public class FileSinkSystem : FileSinkSystemBase<FileOperationsFileStream>
    {
        public override void Initialize(in Logger logger, in SinkConfiguration systemConfig)
        {
            ManagedFileOperationsFunctions.Initialize();
            base.Initialize(logger, systemConfig);
        }
    }
#endif

    public class FileSinkSystemBase<T> : SinkSystemBase<FileSinkLogger<T>> where T : struct, IFileOperations
    {
        public override void Initialize(in Logger logger, in SinkConfiguration systemConfig)
        {
            base.Initialize(logger, systemConfig);
            IsInitialized = false;

            LoggerImpl.fileOps = new T();

            FixedString4096Bytes prefix = "";
            FixedString64Bytes separator = "";
            FixedString4096Bytes postfix = "";

            LoggerImpl.fileOps.Initialize(ref prefix, ref separator, ref postfix);

            var config = (FileSinkSystemConfig)SystemConfig;

            if (LoggerImpl.fileOps.OpenFileForLogging(ref config.FileName, ref config.FileExt, config.MaxFileSizeBytes, config.MaxTimeSpan, config.MaxRoll))
            {
                IsInitialized = true;
            }
            else
            {
                OnSinkFatalError($"Cannot open file '{config.FileName}{config.FileExt}' for write");
            }
        }

        public override void Dispose()
        {
            if (IsInitialized)
                LoggerImpl.fileOps.Dispose();
        }

        public void Flush()
        {
            if (IsInitialized)
                LoggerImpl.fileOps.Flush();
        }
    }

    public interface IFileOperations : IDisposable
    {
        void Initialize(ref FixedString4096Bytes prefix, ref FixedString64Bytes separator, ref FixedString4096Bytes postfix);

        bool OpenFileForLogging(ref FixedString512Bytes fileName, ref FixedString32Bytes fileExt, long maxBytes, TimeSpan maxTimeSpan, int maxRoll);
        void Flush();
        unsafe void Append(byte* data, ulong length, bool newLine);
    }

    [BurstCompile]
    [BurstCompatible]
    struct RollStruct
    {
        private long m_OpenDateTime;
        private int m_Roll;
        private int m_MaxRoll;
        private long m_MaxBytes;
        private TimeSpan m_MaxTimeSpan;

        public bool ShouldRollOnSize => m_MaxBytes > 0;
        public bool ShouldRollOnTime => m_MaxTimeSpan != TimeSpan.Zero;

        public static RollStruct Create(long maxBytes, TimeSpan maxTimeSpan, int maxRoll)
        {
            return new RollStruct
            {
                m_MaxRoll = maxRoll,
                m_MaxBytes = maxBytes,
                m_MaxTimeSpan = maxTimeSpan,
                m_OpenDateTime = TimeStampWrapper.GetTimeStamp(),
                m_Roll = 0
            };
        }


        public FixedString512Bytes RollFilePath(ref FixedString512Bytes filename, ref FixedString32Bytes filenameExt)
        {
            FixedString64Bytes openDateTime = "";

            if (m_MaxTimeSpan != TimeSpan.Zero)
            {
                openDateTime = TimeStampWrapper.GetFormattedTimeStampStringForFileName(m_OpenDateTime);
            }

            var result = filename;

            if (openDateTime.IsEmpty)
            {
                if (m_MaxRoll > 0 && m_Roll > 0)
                {
                    result.Append('_');
                    result.Append(m_Roll);
                }
            }
            else
            {
                result.Append('_');
                result.Append(openDateTime);
            }

            result.Append(filenameExt);

            return result;
        }

        public bool ShouldRoll(long fileStreamLength)
        {
            var shouldRoll = m_MaxBytes > 0 && fileStreamLength > m_MaxBytes;

            if (shouldRoll)
                return true;

            if (m_MaxTimeSpan != TimeSpan.Zero)
            {
                var off = TimeStampWrapper.TotalMillisecondsSince(m_OpenDateTime);
                if (off > m_MaxTimeSpan.TotalMilliseconds)
                {
                    return true;
                }
            }

            return false;
        }

        public void Roll()
        {
            m_OpenDateTime = TimeStampWrapper.GetTimeStamp();
            ++m_Roll;
            if (m_MaxRoll > 0 && m_Roll >= m_MaxRoll)
                m_Roll = 0;
        }
    }

#if USE_BASELIB_FILEIO
    [BurstCompile]
    [BurstCompatible]
    public struct FileOperationsBaselib : IFileOperations
    {
        struct State
        {
            public ulong m_Offset;
            public IntPtr m_fileHandle;
            public FixedString512Bytes m_fileHandleName;

            public FixedString4096Bytes prefix;
            public FixedString64Bytes separator;
            public FixedString4096Bytes postfix;

            public FixedString512Bytes m_Filename;
            public FixedString32Bytes m_FilenameExt;

            public byte m_FirstTimeSeparatorByte;

            public RollStruct m_RollStruct;

            public bool HasOpenedFile => m_fileHandle != IntPtr.Zero;
            public bool FirstTimeSeparator => m_FirstTimeSeparatorByte != 0;
        }

        [NativeDisableUnsafePtrRestriction]
        private unsafe State* m_State;
        public bool IsCreated
        {
            get
            {
                unsafe
                {
                    return m_State != null;
                }
            }
        }

        public void Initialize(ref FixedString4096Bytes prefix, ref FixedString64Bytes separator, ref FixedString4096Bytes postfix)
        {
            CheckMustBeUninitialized();
            unsafe
            {
                var alignOf = UnsafeUtility.AlignOf<State>();
                var sizeOf = sizeof(State);
#if UNITY_DOTSRUNTIME
                m_State = (State*)UnsafeUtility.Malloc(sizeOf, alignOf, Allocator.Persistent);
#else
                m_State = (State*)UnsafeUtility.MallocTracked(sizeOf, alignOf, Allocator.Persistent, 0);
#endif
                UnsafeUtility.MemClear(m_State, sizeOf);

                m_State->prefix = prefix;
                m_State->separator = separator;
                m_State->postfix = postfix;
            }
        }

        public bool OpenFileForLogging(ref FixedString512Bytes fileName, ref FixedString32Bytes fileExt, long maxBytes, TimeSpan maxTimeSpan, int maxRoll)
        {
            CheckMustBeInitialized();
            unsafe
            {
                m_State->m_Filename = fileName;
                m_State->m_FilenameExt = fileExt;
                m_State->m_RollStruct = RollStruct.Create(maxBytes, maxTimeSpan, maxRoll);

                return CreateFileStream();
            }
        }

        private bool CreateFileStream()
        {
            CheckMustBeInitialized();
            unsafe
            {
                m_State->m_FirstTimeSeparatorByte = 1;
                if (m_State->HasOpenedFile)
                {
                    CloseFile();
                }

                var error = new Baselib_ErrorState();
                m_State->m_fileHandleName = m_State->m_RollStruct.RollFilePath(ref m_State->m_Filename, ref m_State->m_FilenameExt);
                m_State->m_fileHandle = Baselib_FileIO_SyncOpen(m_State->m_fileHandleName.GetUnsafePtr(), Baselib_FileIO_OpenFlags.CreateAlways | Baselib_FileIO_OpenFlags.Write, &error).handle;

                //UnityEngine.Debug.Log(string.Format("Opened {0} {1}", m_State->m_fileHandleName, m_State->m_fileHandle.ToInt64()));

                var offsetPtr = &m_State->m_Offset;
                *offsetPtr = 0;

                var res = error.code == Baselib_ErrorCode.Success;
                if (res)
                {
                    if (m_State->prefix.IsEmpty == false)
                    {
                        res = Write(m_State->prefix.GetUnsafePtr(), (ulong)m_State->prefix.Length, offsetPtr);
                    }
                }
                else
                {
                    FixedString512Bytes errorMsg = "Failed to open log file '";
                    errorMsg.Append(m_State->m_fileHandleName);
                    errorMsg.Append((FixedString32Bytes)"' with error: ");
                    errorMsg.Append((int)error.code);
                    errorMsg.Append((FixedString32Bytes)" - ");
                    errorMsg.Append(error.nativeErrorCode);
#if UNITY_DOTSRUNTIME
                    Unity.Logging.DotsRuntimePrintWrapper.ConsoleWrite(errorMsg.GetUnsafePtr(), errorMsg.Length, (byte)1);
#else
                    UnityEngine.Debug.LogError(errorMsg);
#endif
                }

                return res;
            }
        }

        private void CloseFile()
        {
            CheckCanAccessFile();

            unsafe
            {
                if (m_State->postfix.IsEmpty == false)
                {
                    Write(m_State->postfix.GetUnsafePtr(), (ulong)m_State->postfix.Length, &m_State->m_Offset);
                }
            }

            CloseFileInternal();
        }

        private void CloseFileInternal()
        {
            CheckCanAccessFile();

            unsafe
            {
                var error = new Baselib_ErrorState();
                var handle = new Baselib_FileIO_SyncFile { handle = m_State->m_fileHandle };
                //var e1 = error.code;
                Baselib_FileIO_SyncFlush(handle, &error);
                //var e2 = error.code;
                Baselib_FileIO_SyncClose(handle, &error);
                //var e3 = error.code;
                //UnityEngine.Debug.Log(string.Format("Closing {1} {0}. errors = {2},{3},{4}", m_State->m_fileHandle.ToInt64(), m_State->m_fileHandleName, e1, e2, e3));

                m_State->m_fileHandleName = "";
                m_State->m_fileHandle = default;
            }
        }

        public void Flush()
        {
            CheckCanAccessFile();

            unsafe
            {
                var error = new Baselib_ErrorState();
                //var e1 = error.code;
                var handle = new Baselib_FileIO_SyncFile { handle = m_State->m_fileHandle };
                //var e2 = error.code;
                Baselib_FileIO_SyncFlush(handle, &error);

                //UnityEngine.Debug.Log(string.Format("Flush. Errors = {0},{1}", e1, e2));
            }
        }

        public void Dispose()
        {
            unsafe
            {
                if (m_State->HasOpenedFile)
                {
                    //UnityEngine.Debug.Log(string.Format("Disposing {1} {0}..", m_State->m_fileHandle.ToInt64(), m_State->m_fileHandleName));
                    CloseFile();
                }

#if UNITY_DOTSRUNTIME
                UnsafeUtility.Free(m_State, Allocator.Persistent);
#else
                UnsafeUtility.FreeTracked(m_State, Allocator.Persistent);
#endif
                m_State = null;
            }

            CheckMustBeUninitialized();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe bool Write(byte* data, ulong length, ulong* offsetPtr)
        {
            CheckCanAccessFile();

            var error = new Baselib_ErrorState();
            var handle = new Baselib_FileIO_SyncFile { handle = m_State->m_fileHandle };
            *offsetPtr += Baselib_FileIO_SyncWrite(handle, *offsetPtr, (IntPtr)data, length, &error);
            return error.code == Baselib_ErrorCode.Success;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckCanAccessFile()
        {
            unsafe
            {
                if (IsCreated == false)
                    throw new Exception("FileOperationsBaselib is not created!");
                if (m_State->HasOpenedFile == false)
                    throw new Exception("FileOperationsBaselib doesn't have file opened");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckMustBeInitialized()
        {
            if (IsCreated == false)
                throw new Exception("FileOperationsBaselib is not created!");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckMustBeUninitialized()
        {
            if (IsCreated)
                throw new Exception("FileOperationsBaselib is Created, but it shouldn't be!");
        }

        public unsafe void Append(byte* data, ulong length, bool newLine)
        {
            CheckCanAccessFile();

            var offsetPtr = &m_State->m_Offset;
            var shouldRoll = m_State->m_RollStruct.ShouldRoll((long)(*offsetPtr + length));

            if (shouldRoll)
            {
                m_State->m_RollStruct.Roll();
                //UnityEngine.Debug.Log(string.Format("Appending.. Rolling now {0} {1}", m_State->m_fileHandle.ToInt64(), m_State->m_fileHandleName));
                CreateFileStream();
            }

            if (m_State->separator.IsEmpty == false)
            {
                if (m_State->FirstTimeSeparator)
                    m_State->m_FirstTimeSeparatorByte = 0;
                else
                    Write(m_State->separator.GetUnsafePtr(), (ulong)m_State->separator.Length, offsetPtr);
            }

            Write(data, length, offsetPtr);

            if (newLine)
            {
                // Cannot use Environment.NewLine so manually add platform specific newline char(s)
#if UNITY_WINDOWS || UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                var newLineChar = new FixedList32Bytes<byte> {0xD, 0xA};
#else
                var newLineChar = new FixedList32Bytes<byte> {0xA};
#endif
                Write((byte*)&newLineChar + 2, (uint)newLineChar.Length, offsetPtr);
            }
        }
    }
    #else

    internal static class ManagedFileOperationsFunctions
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int InitDelegate(ref FixedString512Bytes filename, ref FixedString32Bytes fileext, ref FixedString4096Bytes prefix,
                                                                                              ref FixedString64Bytes separator, ref FixedString4096Bytes postfix, long maxBytes, TimeSpan maxTimeSpan, int maxRoll);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal unsafe delegate void WriteDelegate(int id, byte* data, ulong length, byte newLine);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void IdDelegate(int id);

        // make sure delegates are not collected by GC
        private static InitDelegate s_InitDelegate;
        private static WriteDelegate s_WriteDelegate;
        private static IdDelegate s_FlushDelegate;
        private static IdDelegate s_DisposeDelegate;

        class LogFileInfo
        {
            private FileStream m_FileStream;
            private FixedString512Bytes m_Filename;
            private FixedString32Bytes m_FilenameExt;

            private readonly byte[] m_PrefixByteArray;
            private readonly byte[] m_PostfixByteArray;

            private bool m_FirstTimeSeparator;
            private FixedString64Bytes m_Separator;

            private RollStruct m_RollStruct;

            private static byte[] FixedStringToByteArray(ref FixedString4096Bytes s)
            {
                if (s.IsEmpty)
                    return null;

                var n = s.Length;
                var res = new byte[n];
                unsafe
                {
                    fixed (byte* ptr = &res[0])
                    {
                        UnsafeUtility.MemCpy(ptr, s.GetUnsafePtr(), n);
                    }
                }

                return res;
            }

            public LogFileInfo(ref FixedString512Bytes filename, ref FixedString32Bytes fileext, ref FixedString4096Bytes prefix,
                               ref FixedString64Bytes separator, ref FixedString4096Bytes postfix, long maxBytes, TimeSpan maxTimeSpan, int maxRoll)
            {
                m_Filename = filename;
                m_FilenameExt = fileext;

                m_PrefixByteArray = FixedStringToByteArray(ref prefix);
                m_PostfixByteArray = FixedStringToByteArray(ref postfix);

                m_Separator = separator;

                m_RollStruct = RollStruct.Create(maxBytes, maxTimeSpan, maxRoll);

                CreateFileStream(firstSetup: true);
            }

            private void CreateFileStream(bool firstSetup)
            {
                m_FirstTimeSeparator = true;

                if (firstSetup == false)
                {
                    CloseFile();
                }

                var filename = m_RollStruct.RollFilePath(ref m_Filename, ref m_FilenameExt);

                m_FileStream = new FileStream(filename.ToString(), FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

                if (m_PrefixByteArray != null)
                    m_FileStream.Write(m_PrefixByteArray, 0, m_PrefixByteArray.Length);
            }

            private void CloseFile()
            {
                if (m_PostfixByteArray != null)
                    m_FileStream.Write(m_PostfixByteArray, 0, m_PrefixByteArray.Length);

                m_FileStream.Flush();
                m_FileStream.Close();
            }

            public void Dispose()
            {
                CloseFile();
            }

            public void Flush()
            {
                m_FileStream.Flush();
            }

            public void WriteAsync(byte[] arr, int offset, int length)
            {
                var shouldRoll = m_RollStruct.ShouldRoll(m_FileStream.Length + length);

                if (shouldRoll)
                {
                    m_RollStruct.Roll();
                    CreateFileStream(firstSetup: false);
                }

                m_FileStream.Write(arr, offset, length);
            }

            public int AddSeparator(byte[] payload, int maxOffsetForSeparator, ref int totalLength)
            {
                //      maxOffset
                // arr |---------|----------------------|
                //     |---|SEPAR|----------------------|
                //      offset

                // no separator, return max offset, don't change the length
                if (m_FirstTimeSeparator || m_Separator.IsEmpty)
                {
                    m_FirstTimeSeparator = false;
                    return maxOffsetForSeparator;
                }

                var n = m_Separator.Length;
                Assert.IsTrue(n <= maxOffsetForSeparator);

                var offset = maxOffsetForSeparator - n;
                unsafe
                {
                    fixed (byte* ptr = &payload[offset])
                    {
                        UnsafeUtility.MemCpy(ptr, m_Separator.GetUnsafePtr(), n);
                    }
                }

                totalLength += n;
                return offset;
            }
        }

        private static Dictionary<int, LogFileInfo> s_FileIdToFileInfo;
        private static bool s_IsInitialized;

        private struct ManagedFileOperationsKey {}
        private struct ManagedFileFlushOperationsKey {}
        internal static readonly SharedStatic<FunctionPointer<InitDelegate>> s_InitMethod = SharedStatic<FunctionPointer<InitDelegate>>.GetOrCreate<FunctionPointer<InitDelegate>, ManagedFileOperationsKey>(16);
        internal static readonly SharedStatic<FunctionPointer<WriteDelegate>> s_WriteMethod = SharedStatic<FunctionPointer<WriteDelegate>>.GetOrCreate<FunctionPointer<WriteDelegate>, ManagedFileOperationsKey>(16);
        internal static readonly SharedStatic<FunctionPointer<IdDelegate>> s_FlushMethod = SharedStatic<FunctionPointer<IdDelegate>>.GetOrCreate<FunctionPointer<IdDelegate>, ManagedFileFlushOperationsKey>(16);
        internal static readonly SharedStatic<FunctionPointer<IdDelegate>> s_DisposeMethod = SharedStatic<FunctionPointer<IdDelegate>>.GetOrCreate<FunctionPointer<IdDelegate>, ManagedFileOperationsKey>(16);

        internal static void Initialize()
        {

            if (s_IsInitialized) return;
            s_IsInitialized = true;


            s_FileIdToFileInfo = new Dictionary<int, LogFileInfo>(64);

            unsafe
            {
                s_InitDelegate = Init;
                s_DisposeDelegate = Dispose;
                s_WriteDelegate = Write;
                s_FlushDelegate = Flush;

                s_InitMethod.Data = new FunctionPointer<InitDelegate>(Marshal.GetFunctionPointerForDelegate(s_InitDelegate));
                s_FlushMethod.Data = new FunctionPointer<IdDelegate>(Marshal.GetFunctionPointerForDelegate(s_FlushDelegate));
                s_DisposeMethod.Data = new FunctionPointer<IdDelegate>(Marshal.GetFunctionPointerForDelegate(s_DisposeDelegate));
                s_WriteMethod.Data = new FunctionPointer<WriteDelegate>(Marshal.GetFunctionPointerForDelegate(s_WriteDelegate));
            }
        }

        [AOT.MonoPInvokeCallback(typeof(WriteDelegate))]
        private static unsafe void Write(int id, byte* data, ulong lengthUlong, byte newLine)
        {
            const int maxNewLineLength = 2;

            var lengthPayload = (int)lengthUlong;
            var maxOffsetForSeparator = UnsafeUtility.SizeOf<FixedString64Bytes>();
            var totalPossibleLength = maxOffsetForSeparator + lengthPayload + maxNewLineLength;

            var arr = new byte[totalPossibleLength];
            fixed (byte* arrPtr = &arr[0])
            {
                UnsafeUtility.MemCpy(arrPtr + maxOffsetForSeparator, data, lengthPayload);
            }

            var totalLength = lengthPayload;
            if (newLine != 0)
            {
#if UNITY_WINDOWS || UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                totalLength += 2;
                arr[maxOffsetForSeparator + lengthPayload + 0] = 0xD;
                arr[maxOffsetForSeparator + lengthPayload + 1] = 0xA;
#else
                totalLength += 1;
                arr[maxOffsetForSeparator + lengthPayload + 0] = 0xA;
#endif
            }

            lock (s_FileIdToFileInfo)
            {
                if (s_FileIdToFileInfo.TryGetValue(id, out var fs))
                {
                    var offset = fs.AddSeparator(arr, maxOffsetForSeparator, ref totalLength);
                    fs.WriteAsync(arr, offset, totalLength);
                }
                else
                {
                    throw new Exception($"[Write] {id} is not found");
                }
            }
        }

        [AOT.MonoPInvokeCallback(typeof(InitDelegate))]
        private static int Init(ref FixedString512Bytes filename, ref FixedString32Bytes fileext, ref FixedString4096Bytes prefix,
                                ref FixedString64Bytes separator, ref FixedString4096Bytes postfix, long maxBytes, TimeSpan maxTimeSpan, int maxRoll)
        {
            try
            {
                var fileInfo = new LogFileInfo(ref filename, ref fileext, ref prefix, ref separator, ref postfix, maxBytes, maxTimeSpan, maxRoll);


                lock (s_FileIdToFileInfo)
                {
                    var id = s_FileIdToFileInfo.Count + 1;
                    s_FileIdToFileInfo[id] = fileInfo;
                    return id;
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(IdDelegate))]
        private static void Flush(int id)
        {
            lock (s_FileIdToFileInfo)
            {
                if (s_FileIdToFileInfo.TryGetValue(id, out var fs))
                {
                    fs.Flush();
                }
                else
                {
                    throw new Exception($"[Flush] {id} is not found");
                }
            }
        }

        [AOT.MonoPInvokeCallback(typeof(IdDelegate))]
        private static void Dispose(int id)
        {
            lock (s_FileIdToFileInfo)
            {
                if (s_FileIdToFileInfo.TryGetValue(id, out var fs))
                {
                    fs.Dispose();
                    s_FileIdToFileInfo.Remove(id);
                }
                else
                {
                    throw new Exception($"[Dispose] {id} is not found");
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FileOperationsFileStream : IFileOperations
    {
        private int m_FileStreamId;
        public bool IsValid => m_FileStreamId != 0;

        private FixedString4096Bytes m_Prefix;
        private FixedString4096Bytes m_Postfix;
        private FixedString64Bytes m_Separator;

        public void Initialize(ref FixedString4096Bytes prefix, ref FixedString64Bytes separator, ref FixedString4096Bytes postfix)
        {
            m_Prefix = prefix;
            m_Separator = separator;
            m_Postfix = postfix;
        }

        public bool OpenFileForLogging(ref FixedString512Bytes fileName, ref FixedString32Bytes fileExt, long maxBytes, TimeSpan maxTimeSpan, int maxRoll)
        {
            m_FileStreamId = ManagedFileOperationsFunctions.s_InitMethod.Data.Invoke(ref fileName,
                                                                                     ref fileExt,
                                                                                     ref m_Prefix,
                                                                                     ref m_Separator,
                                                                                     ref m_Postfix,
                                                                                     maxBytes,
                                                                                     maxTimeSpan,
                                                                                     maxRoll);
            return m_FileStreamId != 0;
        }

        public void Flush()
        {
            CheckIsValid();

            ManagedFileOperationsFunctions.s_FlushMethod.Data.Invoke(m_FileStreamId);
        }

        public void Dispose()
        {
            CheckIsValid();

            ManagedFileOperationsFunctions.s_DisposeMethod.Data.Invoke(m_FileStreamId);
        }

        public unsafe void Append(byte* data, ulong length, bool newLine)
        {
            CheckIsValid();

            ManagedFileOperationsFunctions.s_WriteMethod.Data.Invoke(m_FileStreamId, data, length, (byte)(newLine ? 1 : 0));
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIsValid()
        {
            if (IsValid == false)
                throw new InvalidOperationException("To call this operation FileOperation must be valid");
        }
    }
    #endif
}
