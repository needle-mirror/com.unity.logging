#define LOGGING_FILE_OPS_DEBUG

#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Logging.Sinks
{
    internal struct FileOperationsInMemory : IFileOperationsImplementation
    {
        static FileOperationsInMemory()
        {
            ManagedFunctionsFileInMemory.Initialize();
        }

        public IntPtr OpenFile(ref FixedString4096Bytes absFilePath)
        {
            var ptr = Burst2ManagedCall<ManagedFunctionsFileInMemory.OpenDelegate, ManagedFunctionsFileInMemory>.Ptr();
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
            var ptr = Burst2ManagedCall<ManagedFunctionsFileInMemory.WriteDelegate, ManagedFunctionsFileInMemory>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            return ((delegate * unmanaged[Cdecl] <long, byte*, ulong, ulong*, bool>)ptr.Value)(fileHandle.ToInt64(), data, length, offsetPtr);
#else
            return ptr.Invoke(fileHandle.ToInt64(), data, length, offsetPtr);
#endif
        }

        public void FlushFile(IntPtr fileHandle)
        {
        }

        public void CloseFile(IntPtr fileHandle)
        {
            var ptr = Burst2ManagedCall<ManagedFunctionsFileInMemory.CloseDelegate, ManagedFunctionsFileInMemory>.Ptr();

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                ((delegate * unmanaged[Cdecl] <long, void>)ptr.Value)(fileHandle.ToInt64());
            }
#else
            ptr.Invoke(fileHandle.ToInt64());
#endif
        }

        internal class ManagedFunctionsFileInMemory
        {
            private static bool s_IsInitialized;
            private static Dictionary<long, string> s_Id2Filename;
            private static Dictionary<string, string> s_Result;

            [BurstDiscard]
            public static void Initialize()
            {
                if (s_IsInitialized) return;
                s_IsInitialized = true;

                s_Id2Filename = new Dictionary<long, string>(8);
                s_Result = new Dictionary<string, string>(8);

                Burst2ManagedCall<OpenDelegate, ManagedFunctionsFileInMemory>.Init(Open);
                Burst2ManagedCall<CloseDelegate, ManagedFunctionsFileInMemory>.Init(Close);

                unsafe
                {
                    Burst2ManagedCall<WriteDelegate, ManagedFunctionsFileInMemory>.Init(Write);
                }
            }

            public static void AssertIsEmpty()
            {
                if (s_IsInitialized)
                {
                    var message1 = "";
                    var message2 = "";
                    lock (s_Result)
                    {
                        foreach (var kv in s_Result)
                        {
                            message1 += $"{kv.Key} wasn't used in the test!\n";
                        }
                        s_Result.Clear();
                    }

                    lock (s_Id2Filename)
                    {
                        foreach (var kv in s_Id2Filename)
                        {
                            message2 += $"{kv.Key}-{kv.Value} wasn't closed in the test!\n";
                        }
                        s_Id2Filename.Clear();
                    }
                    if (string.IsNullOrEmpty(message1) == false)
                        UnityEngine.Debug.LogError(message1);
                    if (string.IsNullOrEmpty(message2) == false)
                        UnityEngine.Debug.LogError(message2);
                }
            }

            [AOT.MonoPInvokeCallback(typeof(OpenDelegate))]
            static long Open(ref FixedString4096Bytes absFilePath)
            {
                var fileStream = new StringBuilderSlim(1024);

                lock (s_Result)
                {
                    UnityEngine.Debug.Log($"Opening <{absFilePath}>...");
                    s_Result[absFilePath.ToString()] = "";
                }

                var handle = GCHandle.Alloc(fileStream);
                var res = GCHandle.ToIntPtr(handle).ToInt64();

#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log($"Opened {absFilePath} id = {res}");
#endif
                lock (s_Id2Filename)
                {
                    s_Id2Filename[res] = absFilePath.ToString();
                }

                return res;
            }

            [AOT.MonoPInvokeCallback(typeof(WriteDelegate))]
            static unsafe bool Write(long id, byte* data, ulong length, ulong* offsetPtr)
            {
                var handle = GCHandle.FromIntPtr(new IntPtr(id));
                var fileStream = handle.Target as StringBuilderSlim;

#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log($"Write id = {id}. Length = {length}");
#endif

                lock (fileStream)
                {
                    *offsetPtr = (ulong)fileStream.AppendUTF8(data, (int)length);
                }

                return true;
            }

            [AOT.MonoPInvokeCallback(typeof(CloseDelegate))]
            static void Close(long id)
            {
                var handle = GCHandle.FromIntPtr(new IntPtr(id));
                var fileStream = handle.Target as StringBuilderSlim;

#if LOGGING_FILE_OPS_DEBUG
                UnityEngine.Debug.Log($"Close id = {id}");
#endif

                var str = "";
                lock (fileStream)
                {
                    str = fileStream.ToString();
                    fileStream.Dispose();
                }

                var filename = "";
                lock (s_Id2Filename)
                {
                    filename = s_Id2Filename[id];
                    s_Id2Filename.Remove(id);
                }

                lock (s_Result)
                {
                    s_Result[filename] = str;
                }

                handle.Free();
            }

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate long OpenDelegate(ref FixedString4096Bytes absFilePath);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal unsafe delegate bool WriteDelegate(long id, byte* data, ulong length, ulong* offsetPtr);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate void CloseDelegate(long id);

            public static string ReadAllTextAndRemove(string filename)
            {
                var res = "";
                lock (s_Result)
                {
                    if (s_Result.TryGetValue(filename, out res))
                    {
                        s_Result.Remove(filename);
                        UnityEngine.Debug.Log($"Found <{filename}> and removed");
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"Not found <{filename}>!");
                        throw new Exception();
                    }
                }
                return res;
            }
        }

        public static string ReadAllText(string filename)
        {
            return ManagedFunctionsFileInMemory.ReadAllTextAndRemove(filename);
        }
    }
}
