#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

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

        private static bool s_Initialized;

        public static unsafe void Initialize()
        {
            if (s_Initialized) return;
            s_Initialized = true;
            Burst2ManagedCall<SystemWriteLineDelegate, ManagedOperationsDataContext>.Init(ConsoleWriteImpl);
        }

        public static FunctionPointer<SystemWriteLineDelegate> SystemWriteLine => Burst2ManagedCall<SystemWriteLineDelegate, ManagedOperationsDataContext>.Ptr();

        // FunctionPointers cannot be initialized as regular static variables (triggers Burst compile error) so must use SharedStatic instead
        private struct ManagedOperationsDataContext {}

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
            var ptr = Unity.Logging.ManagedOperations.SystemWriteLine;
    #if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                ((delegate * unmanaged[Cdecl] <byte*, int, byte, void>)ptr.Value)(data, length, newLine);
            }
    #else
            ptr.Invoke(data, length, newLine);
    #endif
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
                var ptr = Unity.Logging.ManagedOperations.SystemWriteLine;
    #if LOGGING_USE_UNMANAGED_DELEGATES
                unsafe
                {
                    ((delegate * unmanaged[Cdecl] <byte*, int, byte, void>)ptr.Value)(data, length, newLine);
                }
    #else
                ptr.Invoke(data, length, newLine);
    #endif
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
                var ptr = Unity.Logging.ManagedOperations.SystemWriteLine;
    #if LOGGING_USE_UNMANAGED_DELEGATES
                unsafe
                {
                    ((delegate * unmanaged[Cdecl] <byte*, int, byte, void>)ptr.Value)(data, length, newLine);
                }
    #else
                ptr.Invoke(data, length, newLine);
    #endif
#endif
            }
        }
    }
}
