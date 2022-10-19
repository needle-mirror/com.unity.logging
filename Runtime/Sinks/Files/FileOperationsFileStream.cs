//#define LOGGING_FILE_OPS_DEBUG

#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging.Sinks
{
    internal struct FileOperationsFileStream : IFileOperationsImplementation
    {
        static FileOperationsFileStream()
        {
            ManagedFunctionsFileStream.Initialize();
        }

        public IntPtr OpenFile(ref FixedString4096Bytes absFilePath)
        {
            var ptr = Burst2ManagedCall<ManagedFunctionsFileStream.OpenDelegate, ManagedFunctionsFileStream>.Ptr();
            long id = 0;

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                id = ((delegate * unmanaged[Cdecl] <ref FixedString4096Bytes, long>)ptr.Value)(ref absFilePath);
            }
#else
            id = ptr.Invoke(ref absFilePath);
#endif

            return new IntPtr(id);
        }

        public unsafe bool Write(IntPtr fileHandle, byte* data, ulong length, ulong* offsetPtr)
        {
            var ptr = Burst2ManagedCall<ManagedFunctionsFileStream.WriteDelegate, ManagedFunctionsFileStream>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            return ((delegate * unmanaged[Cdecl] <long, byte*, ulong, ulong*, bool>)ptr.Value)(fileHandle.ToInt64(), data, length, offsetPtr);
#else
            return ptr.Invoke(fileHandle.ToInt64(), data, length, offsetPtr);
#endif
        }

        public void FlushFile(IntPtr fileHandle)
        {
            var ptr = Burst2ManagedCall<ManagedFunctionsFileStream.FlushDelegate, ManagedFunctionsFileStream>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                ((delegate * unmanaged[Cdecl] <long, void>)ptr.Value)(fileHandle.ToInt64());
            }
#else
            ptr.Invoke(fileHandle.ToInt64());
#endif
        }

        public void CloseFile(IntPtr fileHandle)
        {
            var ptr = Burst2ManagedCall<ManagedFunctionsFileStream.CloseDelegate, ManagedFunctionsFileStream>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                ((delegate * unmanaged[Cdecl] <long, void>)ptr.Value)(fileHandle.ToInt64());
            }
#else
            ptr.Invoke(fileHandle.ToInt64());
#endif
        }

        class ManagedFunctionsFileStream
        {
            private static bool s_IsInitialized;
            [BurstDiscard]
            public static void Initialize()
            {
                if (s_IsInitialized) return;
                s_IsInitialized = true;

                Burst2ManagedCall<OpenDelegate, ManagedFunctionsFileStream>.Init(Open);
                Burst2ManagedCall<CloseDelegate, ManagedFunctionsFileStream>.Init(Close);
                Burst2ManagedCall<FlushDelegate, ManagedFunctionsFileStream>.Init(Flush);

                unsafe
                {
                    Burst2ManagedCall<WriteDelegate, ManagedFunctionsFileStream>.Init(Write);
                }
            }

            [AOT.MonoPInvokeCallback(typeof(OpenDelegate))]
            static long Open(ref FixedString4096Bytes absFilePath)
            {
                var fileStream = new FileStream(absFilePath.ToString(), FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

                var handle = GCHandle.Alloc(fileStream);
                var res = GCHandle.ToIntPtr(handle).ToInt64();

#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log($"Opened {absFilePath} {fileStream} id = {res}");
#endif
                return res;
            }

            [AOT.MonoPInvokeCallback(typeof(WriteDelegate))]
            static unsafe bool Write(long id, byte* data, ulong length, ulong* offsetPtr)
            {
                var handle = GCHandle.FromIntPtr(new IntPtr(id));
                var fileStream = handle.Target as FileStream;

#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log($"Write id = {id} {fileStream}. Length = {length}");
#endif

#if UNITY_DOTSRUNTIME || !UNITY_2021_2_OR_NEWER // ReadOnlySpan is .net standard 2.1, added in 2021.2
                var buffer = new byte[length];
                fixed (byte* ptr = &buffer[0])
                {
                    UnsafeUtility.MemCpy(ptr, data, (long)length);
                }
                fileStream.Write(buffer, 0, (int)length);
#else
                var readonlySpan = new ReadOnlySpan<byte>(data, (int)length);
                lock (fileStream)
                {
                    fileStream.Write(readonlySpan);
                }
#endif
                *offsetPtr = (ulong)fileStream.Position;
                return true;
            }

            [AOT.MonoPInvokeCallback(typeof(FlushDelegate))]
            static void Flush(long id)
            {
                var handle = GCHandle.FromIntPtr(new IntPtr(id));
                var fileStream = handle.Target as FileStream;

#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log($"Flush id = {id} {fileStream}");
#endif

                lock (fileStream)
                {
                    fileStream.Flush();
                }
            }

            [AOT.MonoPInvokeCallback(typeof(CloseDelegate))]
            static void Close(long id)
            {
                var handle = GCHandle.FromIntPtr(new IntPtr(id));
                var fileStream = handle.Target as FileStream;

#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log($"Close id = {id} {fileStream.Name}. pos = {fileStream.Position}");
#endif

                lock (fileStream)
                {
                    fileStream.Close();
                }

                handle.Free();
            }

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate long OpenDelegate(ref FixedString4096Bytes absFilePath);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal unsafe delegate bool WriteDelegate(long id, byte* data, ulong length, ulong* offsetPtr);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate void FlushDelegate(long id);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate void CloseDelegate(long id);
        }
    }
}
