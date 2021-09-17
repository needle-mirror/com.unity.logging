using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging;
using Unity.Logging.Internal.Debug;
using Unity.Logging.Sinks;
using UnityEngine.Assertions;

[assembly: RegisterGenericJobType(typeof(SinkJob<StringSinkLogger>))]

namespace Unity.Logging.Sinks
{
    public struct StringSinkLogger : ILogger
    {
        internal int id;

        public void OnLogMessage(in LogMessage logEvent, in FixedString512Bytes outTemplate, ref LogMemoryManager memoryManager)
        {
            var message = default(UnsafeText);
            var errorMessage = default(FixedString512Bytes);

            if (TextLoggerParser.ParseMessage(outTemplate, logEvent, ref message, ref errorMessage, ref memoryManager))
            {
                if (message.IsCreated)
                {
                    try
                    {
                        unsafe
                        {
                            var data = message.GetUnsafePtr();
                            var length = message.Length;
                            var newLine = true;
                            ManagedMemoryLogHandler.Write(id, data, length, newLine);
                        }
                    }
                    finally
                    {
                        message.Dispose();
                    }
                }
                else
                {
                    throw new Exception(); // this is a test sink
                }
            }
            else
            {
                SelfLog.OnFailedToParseMessage();

                throw new Exception(errorMessage.ToString()); // this is a test sink
            }
        }
    }

    public class StringSink : SinkSystemBase<StringSinkLogger>
    {
        int id;

        public override void Initialize(in Logger logger, in SinkConfiguration systemConfig)
        {
            id = ManagedMemoryLogHandler.Create();
            base.Initialize(logger, systemConfig);
            LoggerImpl.id = id;
        }

        public override void Dispose()
        {
            ManagedMemoryLogHandler.Dispose(id);
            base.Dispose();
        }

        public string GetString()
        {
            return ManagedMemoryLogHandler.FlushOutput(id);
        }

        internal void WriteText(string contents)
        {
            ManagedMemoryLogHandler.WriteText(id, contents);
        }

        public static void AssertNoSinks()
        {
            ManagedMemoryLogHandler.AssertNoSinks();
        }
    }


    public static class StringLoggerSinkExt
    {
        public static LoggerConfig StringLogger(this LoggerWriterConfig writeTo,
                                                bool captureStackTrace = false,
                                                LogLevel? minLevel = null,
                                                FixedString512Bytes? outputTemplate = null)
        {
            return writeTo.AddSinkConfig(new SinkConfiguration<StringSink>
            {
                CaptureStackTraces = captureStackTrace,
                MinLevelOverride = minLevel,
                OutputTemplateOverride = outputTemplate,
            });
        }
    }

    class StringBuilderReplacement : IDisposable
    {
        private unsafe byte* ptr;
        private int size;
        private int cap;

        public StringBuilderReplacement(int n)
        {
            unsafe
            {
                cap = n;
                ptr = (byte*)UnsafeUtility.Malloc(n, 0, Allocator.Persistent);
                size = 0;
            }
        }

        public void Dispose()
        {
            unsafe
            {
                UnsafeUtility.Free(ptr, Allocator.Persistent);
                ptr = null;
            }
        }

        public unsafe void AppendUTF8(byte* data, int length)
        {
            var prevCap = cap;

            ReallocIfNeeded(size + length);

            Assert.IsTrue(size + length < cap, $"{size} + {length} < {cap} . oldCap = {prevCap}");
            UnsafeUtility.MemCpy(ptr + size, data, length);
            size += length;
        }

        public void AppendNewLine()
        {
            unsafe
            {
                ReallocIfNeeded(size + 2);
#if UNITY_WINDOWS || UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                ptr[size++] = 0xD;
#endif
                ptr[size++] = 0xA;
            }
        }

        private void ReallocIfNeeded(int requiredSize)
        {
            unsafe
            {
                if (cap > requiredSize) return;

                while (cap <= requiredSize)
                    cap *= 2;

                var newPtr = (byte*)UnsafeUtility.Malloc(cap, 0, Allocator.Persistent);
                Assert.IsTrue(cap >= size);
                Assert.IsTrue(cap > requiredSize);
                UnsafeUtility.MemCpy(newPtr, ptr, size);
                UnsafeUtility.Free(ptr, Allocator.Persistent);
                ptr = newPtr;
            }
        }

        public void Clear()
        {
            size = 0;
        }

        public override string ToString()
        {
            unsafe
            {
                if (size == 0)
                    return "";
                return Encoding.UTF8.GetString(ptr, size);
            }
        }
    }

    internal static class ManagedMemoryLogHandler
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal unsafe delegate void WriteDelegate(int id, byte* data, int length, bool newLine);

        // make sure delegates are not collected by GC
        private static WriteDelegate s_WriteDelegate;

        private struct ManagedFileOperationsKey {}
        internal static readonly SharedStatic<FunctionPointer<WriteDelegate>> s_WriteMethod = SharedStatic<FunctionPointer<WriteDelegate>>.GetOrCreate<FunctionPointer<WriteDelegate>, ManagedFileOperationsKey>(16);

        private static Dictionary<int, StringBuilderReplacement> sb;
        private static bool IsInitialized;

        internal static void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            sb = new Dictionary<int, StringBuilderReplacement>();

            unsafe
            {
                s_WriteDelegate = WriteFunc;
                s_WriteMethod.Data = new FunctionPointer<WriteDelegate>(Marshal.GetFunctionPointerForDelegate(s_WriteDelegate));
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIsValid(int id)
        {
            if (id <= 0)
                throw new InvalidOperationException("To call this operation id must be valid");
        }

        [AOT.MonoPInvokeCallback(typeof(WriteDelegate))]
        private static unsafe void WriteFunc(int id, byte* data, int length, bool newLine)
        {
            CheckIsValid(id);

            lock (sb)
            {
                if (sb.ContainsKey(id))
                {
                    sb[id].AppendUTF8(data, length);
                    if (newLine)
                        sb[id].AppendNewLine();
                }
                else
                    throw new Exception(id + " was not found in WriteFunc");
            }
        }

        // not burst
        public static int Create()
        {
            if (s_WriteMethod.Data.IsCreated == false)
                Initialize();

            lock (sb)
            {
                for (var i = sb.Count + 1; i < sb.Count + 999; ++i)
                    if (sb.ContainsKey(i) == false)
                    {
                        sb[i] = new StringBuilderReplacement(4096);
                        return i;
                    }
            }
            return 0;
        }

        // not burst
        public static string FlushOutput(int id)
        {
            CheckIsValid(id);
            lock (sb)
            {
                if (sb.ContainsKey(id))
                {
                    var res = sb[id].ToString();
                    sb[id].Clear();
                    return res;
                }
                else
                {
                    throw new Exception(id + " is not found! FlushOutput");
                }
            }
        }

        // called from burst or not burst
        public static unsafe void Write(int id, byte* data, int length, bool newLine)
        {
            CheckIsValid(id);
            s_WriteMethod.Data.Invoke(id, data, length, newLine);
        }

        public static void WriteText(int id, string contents)
        {
            CheckIsValid(id);

            var bytes= Encoding.UTF8.GetBytes(contents);
            lock (sb)
            {
                unsafe
                {
                    fixed (byte* p = &bytes[0])
                    {
                        sb[id].AppendUTF8(p, bytes.Length);
                    }
                }
            }
        }

        public static void Dispose(int id)
        {
            CheckIsValid(id);
            lock (sb)
            {
                sb[id].Dispose();
                sb.Remove(id);
            }
        }

        public static void AssertNoSinks()
        {
            if (sb != null)
            {
                lock (sb)
                {
                    var s = "";
                    foreach (var p in sb)
                    {
                        s += p.Key + " ";
                    }
                    Assert.AreEqual(0, sb.Count, s);
                }
            }
        }
    }

}
