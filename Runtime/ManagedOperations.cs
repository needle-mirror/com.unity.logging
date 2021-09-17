using System;
using System.Runtime.InteropServices;

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Logging
{
#if !UNITY_DOTSRUNTIME && !NET_DOTS

    /// <summary>
    /// Collection of functionality that (currently) requires calling into managed code, which is exposed through Burst FunctionPointers
    /// allowing Burst compiled functions to call into managed code.
    /// </summary>
    internal struct ManagedOperations
    {
        /// <summary>
        /// Delegate to retrieve the managed stack trace and copy it into the provided buffer.
        /// </summary>
        /// <remarks>
        /// The destBuffer parameter must point to a valid fixed or native memory buffer large enough to hold the stack trace;
        /// the string will be truncated if the buffer is too small. To determine the required size of the buffer, pass in 'null'
        /// for the buffer parameter.
        ///
        /// NOTE: All length parameters and return values are in 'bytes' (NOT chars)
        /// The returned stack trace string is converted from standard C# chars (UTF-16) to UTF-8 encoding.
        /// </remarks>
        /// <param name="destBuffer">Pointer to a valid memory buffer that'll receive the stack trace string; pass in 'null' to query the required size.</param>
        /// <param name="bufferLength">Total length (in bytes) of the output buffer</param>
        /// <param name="destIndex">Byte index within the buffer to begin copying; if not 0 make sure there's enough space from the starting point to the end of the buffer</param>
        /// <returns>Number of bytes copied into destination buffer or the required buffer size of the stack trace if null was passed in.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate int SerializeStackTraceDelegate(byte* destBuffer, int bufferLength, int destIndex);

        /// <summary>
        /// Delegate to write a text buffer to the system Console (stdout).
        /// </summary>
        /// <remarks>
        /// The stringBuffer parameter must point to a valid fixed or native memory buffer containing UTF-8 encoded text.
        /// If newLine is true Console.WriteLine is invoked otherwise Console.Write is used.
        ///
        /// NOTE: All length parameters and return values are in 'bytes' (NOT chars)
        /// </remarks>
        /// <param name="stringBuffer">Pointer to a valid memory buffer that contains the UTF-8 encoded text to output.</param>
        /// <param name="bufferLength">Number of bytes to output from the stringBuffer.</param>
        /// <param name="newLine">True to include a newline after the text and false to not include a newline</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void SystemWriteLineDelegate(byte* stringBuffer, int bufferLength, byte newLine);

        public static unsafe void Initialize()
        {
            s_SerializeStackTraceDelegate = SerializeStackTraceImpl;
            s_ConsoleWriteDelegate = ConsoleWriteImpl;

            s_SerializeStackTraceFunctor.Data = new FunctionPointer<SerializeStackTraceDelegate>(Marshal.GetFunctionPointerForDelegate(s_SerializeStackTraceDelegate));
            s_ConsoleWriteFunctor.Data = new FunctionPointer<SystemWriteLineDelegate>(Marshal.GetFunctionPointerForDelegate(s_ConsoleWriteDelegate));
        }

        public static FunctionPointer<SerializeStackTraceDelegate> SerializeStackTrace => s_SerializeStackTraceFunctor.Data;

        public static FunctionPointer<SystemWriteLineDelegate> SystemWriteLine => s_ConsoleWriteFunctor.Data;

        // FunctionPointers cannot be initialized as regular static variables (triggers Burst compile error) so must use SharedStatic instead
        private struct ManagedOperationsDataContext {}
        private static SerializeStackTraceDelegate s_SerializeStackTraceDelegate;
        private static SystemWriteLineDelegate s_ConsoleWriteDelegate;
        private static readonly SharedStatic<FunctionPointer<SerializeStackTraceDelegate>> s_SerializeStackTraceFunctor = SharedStatic<FunctionPointer<SerializeStackTraceDelegate>>.GetOrCreate<FunctionPointer<SerializeStackTraceDelegate>, ManagedOperationsDataContext>(16);
        private static readonly SharedStatic<FunctionPointer<SystemWriteLineDelegate>> s_ConsoleWriteFunctor = SharedStatic<FunctionPointer<SystemWriteLineDelegate>>.GetOrCreate<FunctionPointer<SystemWriteLineDelegate>, ManagedOperationsDataContext>(16);

        [NotBurstCompatible]
        [AOT.MonoPInvokeCallback(typeof(SerializeStackTraceDelegate))]
        private static unsafe int SerializeStackTraceImpl(byte* destBuffer, int bufferLength, int destIndex)
        {
            string stackString;
            int numBytes = 0;

            if (bufferLength < 0 || destIndex < 0)
                return 0;

            try
            {
                int length = bufferLength - destIndex;

                stackString = System.Environment.StackTrace;
                numBytes = System.Text.Encoding.UTF8.GetByteCount(stackString);

                // Pass in a null pointer to just get the required length of the stack trace
                if (destBuffer != null && length > 0)
                {
                    // If the buffer is too small, need to truncate the string, but since it's encoding into UTF-8 the amount to chop off isn't obvious.
                    // We'll assume most chars can be encoded with 1 byte (ASCII values) with some content needed 2 bytes, e.g. non-English file paths,
                    // which may average out to 1.25 bytes per char.
                    while (numBytes > length)
                    {
                        int byteDiff = numBytes - length;
                        int charsToTrim = (int)(byteDiff * 1.25f);

                        if (charsToTrim >= stackString.Length)
                        {
                            stackString = String.Empty;
                            numBytes = 0;
                            break;
                        }

                        // Check the required bytes of the new string and if still too long then try again
                        stackString = stackString.Substring(0, stackString.Length - charsToTrim);
                        numBytes = System.Text.Encoding.UTF8.GetByteCount(stackString);
                    }

                    fixed(char* pString = stackString)
                    {
                        numBytes = System.Text.Encoding.UTF8.GetBytes(pString, stackString.Length, &destBuffer[destIndex], numBytes);
                    }
                }
                else if (destBuffer != null)
                {
                    // Valid buffer was specified but actual length is too small
                    numBytes = 0;
                }
            }
            catch { numBytes = 0; }

            return numBytes;
        }

        [NotBurstCompatible]
        [AOT.MonoPInvokeCallback(typeof(SystemWriteLineDelegate))]
        private static unsafe void ConsoleWriteImpl(byte* stringBuffer, int bufferLength, byte newLine)
        {
            try
            {
                if (newLine != 0)
                {
                    System.Console.WriteLine(System.Text.Encoding.UTF8.GetString(stringBuffer, bufferLength));
                }
                else
                {
                    System.Console.Write(System.Text.Encoding.UTF8.GetString(stringBuffer, bufferLength));
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.Message);
            }
        }
    }
#endif

#if UNITY_DOTSRUNTIME

    [StructLayout(LayoutKind.Sequential)]
    public struct DotsRuntimePrintWrapper
    {
        [DllImport("lib_unity_logging", EntryPoint = "BeginBatchConsoleWrite")]
        public static extern unsafe void BeginBatchConsoleWrite();
        [DllImport("lib_unity_logging", EntryPoint = "EndBatchConsoleWrite")]
        public static extern unsafe void EndBatchConsoleWrite();
        [DllImport("lib_unity_logging", EntryPoint = "Flush")]
        public static extern unsafe void Flush();

        [DllImport("lib_unity_logging", EntryPoint = "ConsoleWrite")]
        public static extern unsafe void ConsoleWrite(byte* buffer, int numBytes, byte newLine);

        public static void ConsoleWrite(FixedString512Bytes message)
        {
            unsafe
            {
                var data = message.GetUnsafePtr();
                var length = message.Length;
                ConsoleWrite(data, length, 1);
            }
        }
    }

#endif

    /// <summary>
    /// Static class for Console related operations.
    /// Provides <see cref="Write(byte*,int,byte)"/>, <see cref="Write(ref FixedString512Bytes)"/> and <see cref="Write(ref FixedString4096Bytes)"/> for low-level writing to the Console.
    /// Use <see cref="BeginBatch"/> and <see cref="EndBatch"/> when you need to write a lot of data in a batch.
    /// </summary>
    public static class Console
    {
        /// <summary>
        /// Call this before writing a lot of data to the console. Disables flush behaviour on every end-of-line
        /// </summary>
        public static void BeginBatch()
        {
#if UNITY_DOTSRUNTIME
            Unity.Logging.DotsRuntimePrintWrapper.BeginBatchConsoleWrite();
#endif
        }

        /// <summary>
        /// Call this after <see cref="BeginBatch"/> Restores flush behaviour on every end-of-line
        /// </summary>
        public static void EndBatch()
        {
#if UNITY_DOTSRUNTIME
            Unity.Logging.DotsRuntimePrintWrapper.EndBatchConsoleWrite();
#endif
        }

        /// <summary>
        /// Flush internal buffer to the console
        /// </summary>
        public static void Flush()
        {
#if UNITY_DOTSRUNTIME
            Unity.Logging.DotsRuntimePrintWrapper.Flush();
#endif
        }

        /// <summary>
        /// Unsafe method for writing data to the console
        /// </summary>
        /// <param name="data">Pointer to the string</param>
        /// <param name="length">Length of the string</param>
        /// <param name="newLine">if true - add a new line at the end</param>
        public static unsafe void Write(byte* data, int length, byte newLine)
        {
#if UNITY_DOTSRUNTIME
            Unity.Logging.DotsRuntimePrintWrapper.ConsoleWrite(data, length, newLine);
#else
            Unity.Logging.ManagedOperations.SystemWriteLine.Invoke(data, length, newLine);
#endif
        }

        /// <summary>
        /// Writes FixedString to the console
        /// </summary>
        /// <param name="message">String to write to the console</param>
        public static void Write(ref FixedString512Bytes message)
        {
            unsafe
            {
                var data = message.GetUnsafePtr();
                var length = message.Length;
                byte newLine = 1;
#if UNITY_DOTSRUNTIME
                Unity.Logging.DotsRuntimePrintWrapper.ConsoleWrite(data, length, newLine);
#else
                ManagedOperations.SystemWriteLine.Invoke(data, length, newLine);
#endif
            }
        }

        /// <summary>
        /// Writes FixedString to the console
        /// </summary>
        /// <param name="message">String to write to the console</param>
        public static void Write(ref FixedString4096Bytes message)
        {
            unsafe
            {
                var data = message.GetUnsafePtr();
                var length = message.Length;
                byte newLine = 1;
#if UNITY_DOTSRUNTIME
                Unity.Logging.DotsRuntimePrintWrapper.ConsoleWrite(data, length, newLine);
#else
                ManagedOperations.SystemWriteLine.Invoke(data, length, newLine);
#endif
            }
        }

        /// <summary>
        /// Schedule <see cref="BeginBatch"/> call in a job
        /// </summary>
        /// <param name="inputDeps">Dependency that should complete before this job</param>
        /// <returns>JobHandle for just scheduled job</returns>
        public static JobHandle ScheduleBeginBatch(JobHandle inputDeps)
        {
            return new BeginBatchJob().Schedule(inputDeps);
        }

        [BurstCompile]
        private struct BeginBatchJob : IJob { public void Execute() { BeginBatch(); } }


        /// <summary>
        /// Schedule <see cref="EndBatch"/> call in a job
        /// </summary>
        /// <param name="inputDeps">Dependency that should complete before this job</param>
        /// <returns>JobHandle for just scheduled job</returns>
        public static JobHandle ScheduleEndBatch(JobHandle inputDeps)
        {
            return new EndBatchJob().Schedule(inputDeps);
        }

        [BurstCompile]
        private struct EndBatchJob : IJob { public void Execute() { EndBatch(); } }


        /// <summary>
        /// Schedule <see cref="Flush"/> call in a job
        /// </summary>
        /// <param name="inputDeps">Dependency that should complete before this job</param>
        /// <returns>JobHandle for just scheduled job</returns>
        public static JobHandle ScheduleFlush(JobHandle inputDeps)
        {
            return new FlushJob().Schedule(inputDeps);
        }

        [BurstCompile]
        private struct FlushJob : IJob { public void Execute() { Flush(); } }
    }
}
