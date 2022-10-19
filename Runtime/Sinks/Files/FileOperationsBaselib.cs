
//#define LOGGING_FILE_OPS_DEBUG

using System;
using Unity.Baselib.LowLevel;
using Unity.Collections;

namespace Unity.Logging.Sinks
{
    internal struct FileOperationsBaselib : IFileOperationsImplementation
    {
        public unsafe IntPtr OpenFile(ref FixedString4096Bytes absFilePath)
        {
            var error = new Binding.Baselib_ErrorState();
            var result = Binding.Baselib_FileIO_SyncOpen(absFilePath.GetUnsafePtr(), Binding.Baselib_FileIO_OpenFlags.CreateAlways | Binding.Baselib_FileIO_OpenFlags.Write, &error).handle;

            if (error.code != Binding.Baselib_ErrorCode.Success)
            {
                FixedString4096Bytes errorMsg = "Failed to open log file '";
                errorMsg.Append(absFilePath);
                errorMsg.Append((FixedString32Bytes)"' with error: ");
                errorMsg.Append((int)error.code);
                errorMsg.Append((FixedString32Bytes)" - ");
                errorMsg.Append(error.nativeErrorCode);
#if UNITY_DOTSRUNTIME
                    Unity.Logging.DotsRuntimePrintWrapper.ConsoleWrite(errorMsg.GetUnsafePtr(), errorMsg.Length, (byte)1);
#else
                UnityEngine.Debug.LogError(errorMsg);
#endif
                result = IntPtr.Zero;
            }
            else
            {
#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log(string.Format("Opened {0} {1}", absFilePath, result.ToInt64()));
#endif
            }

            return result;
        }

        public unsafe bool Write(IntPtr fileHandle, byte* data, ulong length, ulong* offsetPtr)
        {
            var handle = new Binding.Baselib_FileIO_SyncFile { handle = fileHandle };

            var error = new Binding.Baselib_ErrorState();
            *offsetPtr += Binding.Baselib_FileIO_SyncWrite(handle, *offsetPtr, (IntPtr)data, length, &error);
            var success = error.code == Binding.Baselib_ErrorCode.Success;

#if LOGGING_FILE_OPS_DEBUG
            if (success == false)
                UnityEngine.Debug.Log(string.Format("Write {0} failed. error = {1}", fileHandle.ToInt64(), error.code));
#endif
            return success;
        }

        public void FlushFile(IntPtr fileHandle)
        {
            var handle = new Binding.Baselib_FileIO_SyncFile { handle = fileHandle };

            var error = new Binding.Baselib_ErrorState();
            unsafe
            {
                Binding.Baselib_FileIO_SyncFlush(handle, &error);
            }
#if LOGGING_FILE_OPS_DEBUG
            var e1 = error.code;
            UnityEngine.Debug.Log(string.Format("Flush. Error = {0}", e1));
#endif
        }

        public void CloseFile(IntPtr fileHandle)
        {
            var error = new Binding.Baselib_ErrorState();
            var handle = new Binding.Baselib_FileIO_SyncFile { handle = fileHandle };
            unsafe
            {
                Binding.Baselib_FileIO_SyncFlush(handle, &error);
                var e2 = error.code;
                Binding.Baselib_FileIO_SyncClose(handle, &error);
                var e3 = error.code;
#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log(string.Format("Closing {0}. errors = {1},{2}", fileHandle.ToInt64(), e2, e3));
#endif
            }
        }
    }
}
