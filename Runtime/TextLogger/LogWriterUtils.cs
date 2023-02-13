#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.IL2CPP.CompilerServices;
using Unity.Logging.Internal;
using Unity.Logging.Internal.Debug;

namespace Unity.Logging
{
    /// <summary>
    /// Parser logic
    /// </summary>
    [BurstCompile]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public unsafe struct LogWriterUtils
    {
        /// <summary>
        /// Result values returned by an OutputWriteHandler implementer.
        /// </summary>
        public enum ContextWriteResult
        {
            /// <summary>
            /// Context ID value is recognized/supported by the current <see cref="OutputWriterHandler"/>.
            /// </summary>
            /// <remarks>
            /// There can be multiple <see cref="OutputWriterHandler"/> delegates, each supporting a different set of context data structs,
            /// which are called in turn from a list until an approbate delegate is found. Delegates must return this value if the context
            /// data type isn't recognized to instruct <see cref="LogWriterUtils"/> to try the next delegate.
            /// </remarks>
            UnknownType,

            /// <summary>
            /// Indicates an <see cref="OutputWriterHandler"/> successfully wrote the data for a Context struct to the output stream.
            /// </summary>
            Success,

            /// <summary>
            /// Indicates an <see cref="OutputWriterHandler"/> failed to write the data for a Context struct to the output stream.
            /// </summary>
            /// <remarks>
            /// This should be returned if there's any issue writing data.
            /// </remarks>
            Failed,
        };

        /// <summary>
        /// Defines a delegate to handle writing the field data for context data structs.
        /// </summary>
        /// <remarks>
        /// The <see cref="LogWriterUtils"/> calls into these delegates in order to write the struct data to the output string.
        /// Delegates can be added or removed from the active list of handlers by calling <see cref="AddOutputHandler"/>
        /// and <see cref="RemoveOutputHandler"/> respectively.
        /// By default, source generation will produce the output handlers and add the delegates to the handle list.
        ///
        /// NOTE: Burst FunctionPointers only support delegates with primitive parameter types; structs cannot be passed, even by reference.
        /// </remarks>
        /// <param name="formatter">Formatter that should be used to populate output text</param>
        /// <param name="outputUnsafeText">Reference to a UnsafeListString object to which the delegate writes the struct's data.</param>
        /// <param name="mem">Pointer + size of data to parse</param>
        /// <param name="memAllocator">Memory manager that owns the binary data</param>
        /// <param name="holeInfo">Hole setup, like format specifiers</param>
        /// <returns>Value indicating delegate successfully handled the data or not.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate ContextWriteResult OutputWriterHandler(ref FormatterStruct formatter, ref UnsafeText outputUnsafeText, ref BinaryParser mem, IntPtr memAllocator, ref ArgumentInfo holeInfo);

        /// <summary>
        /// Defines a delegate to handle writing the timestamp
        /// </summary>
        /// <param name="outputUnsafeText">Reference to a UnsafeListString object to which the delegate writes the struct's data.</param>
        /// <param name="timestamp">Timestamps to write to the UnsafeText</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate ContextWriteResult OutputWriterTimestampHandler(ref UnsafeText outputUnsafeText, long timestamp);

        /// <summary>
        /// Defines a delegate to handle writing the <see cref="LogLevel"/>
        /// </summary>
        /// <param name="outputUnsafeText">Reference to a UnsafeListString object to which the delegate writes the struct's data.</param>
        /// <param name="level">LogLevel to write to the UnsafeText</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate ContextWriteResult OutputWriterLevelHandler(ref UnsafeText outputUnsafeText, LogLevel level);

        private struct TextLoggerParserKey {}

        private struct TextLoggerTimestampKey {}

        private struct TextLoggerLevelKey {}

        private static readonly SharedStatic<ThreadSafeFuncList<OutputWriterHandler>> s_OutputWriterHandlers =
            SharedStatic<ThreadSafeFuncList<OutputWriterHandler>>.GetOrCreate<ThreadSafeFuncList<OutputWriterHandler>, TextLoggerParserKey>(16);

        private static readonly SharedStatic<FunctionPointer<OutputWriterTimestampHandler>> s_OutputWriterTimestampHandler =
            SharedStatic<FunctionPointer<OutputWriterTimestampHandler>>.GetOrCreate<FunctionPointer<OutputWriterTimestampHandler>, TextLoggerTimestampKey>(16);

        private static readonly SharedStatic<FunctionPointer<OutputWriterLevelHandler>> s_OutputWriterLevelHandler =
            SharedStatic<FunctionPointer<OutputWriterLevelHandler>>.GetOrCreate<FunctionPointer<OutputWriterLevelHandler>, TextLoggerLevelKey>(16);

        /// <summary>
        /// Sets a delegate to process and generate logging output strings for LogLevel.
        /// </summary>
        /// <remarks>
        /// This method is invoked by source generated code and generally shouldn't be used directly.
        ///
        /// Since this method takes a delegate parameter, it's not Burst compatible itself. However, the method
        /// referenced by the delegate can be Burst compatible or not, depending on how it's implemented.
        /// If the handler method is Burst compatible, then pass true for isBurstable to Burst compile the
        /// handler, otherwise the handler always runs as managed code.
        /// </remarks>
        /// <param name="handler"><see cref="OutputWriterLevelHandler"/> delegate to output logging for LogLevel. </param>
        /// <param name="isBurstable">True to Burst compile the handler and false if it's not Burst compatible.</param>
        public static void SetOutputHandlerForLevel(OutputWriterLevelHandler handler, bool isBurstable = false)
        {
            if (handler != null)
            {
                // Check if list already contains this delegate (Contains won't work with FunctionPointer type)
                FunctionPointer<OutputWriterLevelHandler> func;
                if (isBurstable)
                {
                    func = BurstCompiler.CompileFunctionPointer(handler);
                }
                else
                {
                    // Pin the delegate so it can be used for the lifetime of the app. Never de-alloc
                    GCHandle.Alloc(handler);
                    func = new FunctionPointer<OutputWriterLevelHandler>(Marshal.GetFunctionPointerForDelegate(handler));
                }

                WarmUpFunctionInMainThread(func);

                s_OutputWriterLevelHandler.Data = func;
            }
            else
            {
                s_OutputWriterLevelHandler.Data = default;
                Assert.IsFalse(s_OutputWriterLevelHandler.Data.IsCreated);
            }
        }

        /// <summary>
        /// Sets a delegate to process and generate logging output strings for timestamps.
        /// </summary>
        /// <remarks>
        /// This method is invoked by source generated code and generally shouldn't be used directly.
        ///
        /// Since this method takes a delegate parameter, it's not Burst compatible itself. However, the method
        /// referenced by the delegate can be Burst compatible or not, depending on how it's implemented.
        /// If the handler method is Burst compatible, then pass true for isBurstable to Burst compile the
        /// handler, otherwise the handler always runs as managed code.
        /// </remarks>
        /// <param name="handler"><see cref="OutputWriterTimestampHandler"/> delegate to output logging for timestamps. </param>
        /// <param name="isBurstable">True to Burst compile the handler and false if it's not Burst compatible.</param>
        public static void SetOutputHandlerForTimestamp(OutputWriterTimestampHandler handler, bool isBurstable = false)
        {
            if (handler != null)
            {
                FunctionPointer<OutputWriterTimestampHandler> func;
                if (isBurstable)
                {
                    func = BurstCompiler.CompileFunctionPointer(handler);
                }
                else
                {
                    // Pin the delegate so it can be used for the lifetime of the app. Never de-alloc
                    GCHandle.Alloc(handler);
                    func = new FunctionPointer<OutputWriterTimestampHandler>(Marshal.GetFunctionPointerForDelegate(handler));
                }

                WarmUpFunctionInMainThread(func);

                s_OutputWriterTimestampHandler.Data = func;
            }
            else
            {
                s_OutputWriterTimestampHandler.Data = default;
                Assert.IsFalse(s_OutputWriterTimestampHandler.Data.IsCreated);
            }
        }

        /// <summary>
        /// Adds a delegate to process and generate logging output strings for a set of context structs.
        /// </summary>
        /// <remarks>
        /// This method is invoked by source generated code and generally shouldn't be used directly.
        ///
        /// Since this method takes a delegate parameter, it's not Burst compatible itself. However, the method
        /// referenced by the delegate can be Burst compatible or not, depending on how it's implemented.
        /// If the handler method is Burst compatible, then pass true for isBurstable to Burst compile the
        /// handler, otherwise the handler always runs as managed code.
        /// </remarks>
        /// <param name="handler"><see cref="OutputWriterHandler"/> delegate to output logging context struct data. </param>
        /// <param name="isBurstable">True to Burst compile the handler and false if it's not Burst compatible.</param>
        /// <returns>A token referencing this handler, used for removing the handler later.</returns>
        public static IntPtr AddOutputHandler(OutputWriterHandler handler, bool isBurstable = false)
        {
            FunctionPointer<OutputWriterHandler> func;
            if (isBurstable)
            {
                func = BurstCompiler.CompileFunctionPointer(handler);
            }
            else
            {
                // Pin the delegate so it can be used for the lifetime of the app. Never de-alloc
                GCHandle.Alloc(handler);
                func = new FunctionPointer<OutputWriterHandler>(Marshal.GetFunctionPointerForDelegate(handler));
            }

            WarmUpFunctionInMainThread(func);

            s_OutputWriterHandlers.Data.Add(func);

            return func.Value;
        }

        /// <summary>
        /// Used by Log.Decorate calls to add a method that's called for every log message, and add decorations to it globally.
        /// </summary>
        /// <param name="handler">Decorate call delegate</param>
        /// <param name="isBurstable">If true - <c>BurstCompiler.CompileFunctionPointer</c> will be called. Will fallback to false if compilation failed</param>
        /// <returns>LogDecorateHandlerScope scoped struct</returns>
        public static LogDecorateHandlerScope AddDecorateHandler(LoggerManager.OutputWriterDecorateHandler handler, bool isBurstable = false)
        {
            var func = new FunctionPointer<LoggerManager.OutputWriterDecorateHandler>();
            if (isBurstable)
            {
                try
                {
                    func = BurstCompiler.CompileFunctionPointer(handler);
                }
                catch
                {
                    isBurstable = false;
                }
            }

            if (isBurstable == false)
            {
                // Pin the delegate so it can be used for the lifetime of the app. Never de-alloc
                GCHandle.Alloc(handler);
                func = new FunctionPointer<LoggerManager.OutputWriterDecorateHandler>(Marshal.GetFunctionPointerForDelegate(handler));
            }

            WarmUpFunctionInMainThread(func);

            return new LogDecorateHandlerScope(func);
        }

        /// <summary>
        /// Used by Log.To().Decorate calls to add a method that's called for every log message, and adds decorations to it for a particular logger.
        /// </summary>
        /// <param name="lock">LogControllerScopedLock of logger that adds the decorator</param>
        /// <param name="handler">Decorate call delegate</param>
        /// <param name="isBurstable">If true - <c>BurstCompiler.CompileFunctionPointer</c> will be called. Will fallback to false if compilation failed</param>
        /// <returns>LogDecorateHandlerScope scoped struct</returns>
        public static LogDecorateHandlerScope AddDecorateHandler(LogControllerScopedLock @lock, LoggerManager.OutputWriterDecorateHandler handler, bool isBurstable = false)
        {
            FunctionPointer<LoggerManager.OutputWriterDecorateHandler> func;
            if (isBurstable)
            {
                func = BurstCompiler.CompileFunctionPointer(handler);
            }
            else
            {
                // Pin the delegate so it can be used for the lifetime of the app. Never de-alloc
                GCHandle.Alloc(handler);
                func = new FunctionPointer<LoggerManager.OutputWriterDecorateHandler>(Marshal.GetFunctionPointerForDelegate(handler));
            }

            WarmUpFunctionInMainThread(func);

            return new LogDecorateHandlerScope(func, @lock);
        }

        // WarmUp functions is used to call Burst compilation in the main thread, otherwise it could run in another one and that will crash player
        private static void WarmUpFunctionInMainThread(FunctionPointer<OutputWriterTimestampHandler> func)
        {
            var burstWarmUpMessageOutput = new UnsafeText(7, Allocator.Temp);
            func.Invoke(ref burstWarmUpMessageOutput, 42);
            burstWarmUpMessageOutput.Dispose();
        }

        // WarmUp functions is used to call Burst compilation in the main thread, otherwise it could run in another one and that will crash player
        private static void WarmUpFunctionInMainThread(FunctionPointer<OutputWriterLevelHandler> func)
        {
            var burstWarmUpMessageOutput = new UnsafeText(7, Allocator.Temp);
            func.Invoke(ref burstWarmUpMessageOutput, LogLevel.Debug);
            burstWarmUpMessageOutput.Dispose();
        }

        // WarmUp functions is used to call Burst compilation in the main thread, otherwise it could run in another one and that will crash player
        private static void WarmUpFunctionInMainThread(FunctionPointer<OutputWriterHandler> func)
        {
            var burstWarmUpMessageOutput = new UnsafeText(7, Allocator.Temp);
            FixedString64Bytes buff = "Test";
            var mem = new BinaryParser(buff.GetUnsafePtr(), 8);
            LogMemoryManager memAllocator = default;
            var ptr = new IntPtr(&memAllocator);
            var arg = default(ArgumentInfo);
            FormatterStruct fs = default;
            func.Invoke(ref fs, ref burstWarmUpMessageOutput, ref mem, ptr, ref arg);
            burstWarmUpMessageOutput.Dispose();
        }

        // WarmUp functions is used to call Burst compilation in the main thread, otherwise it could run in another one and that will crash player
        private static void WarmUpFunctionInMainThread(FunctionPointer<LoggerManager.OutputWriterDecorateHandler> func)
        {
            var handles = new FixedList512Bytes<PayloadHandle>();
            var ctx = new LogContextWithDecorator(&handles);
            func.Invoke(ref ctx);
        }

        /// <summary>
        /// Removes an <see cref="OutputWriterHandler"/> delegate from the parser.
        /// </summary>
        /// <remarks>
        /// This method is invoked by source generated code and generally shouldn't be used directly.
        ///
        /// Once the handler is removed, context structs this handler serviced will no longer generated logging output.
        /// Log message that reference these context structs will still be outputted, but the context data will be missing.
        /// </remarks>
        /// <param name="token">Value returned by a previous call to <see cref="AddOutputHandler"/> for the handler to remove.</param>
        public static void RemoveOutputHandler(IntPtr token)
        {
            s_OutputWriterHandlers.Data.Remove(token);
        }

        /// <summary>
        /// Writes Timestamp to the UnsafeText as UTC
        /// </summary>
        /// <param name="timestamp">Timestamp to write</param>
        /// <param name="messageOutput">Where to write to</param>
        /// <returns>True on success</returns>
        public static bool WriteFormattedTimestamp(in long timestamp, ref UnsafeText messageOutput)
        {
            if (s_OutputWriterTimestampHandler.Data.IsCreated)
            {
#if LOGGING_USE_UNMANAGED_DELEGATES
                ((delegate * unmanaged[Cdecl] <ref UnsafeText, long, ContextWriteResult>)s_OutputWriterTimestampHandler.Data.Value)(ref messageOutput, timestamp);
#else
                s_OutputWriterTimestampHandler.Data.Invoke(ref messageOutput, timestamp);
#endif
            }
            else
            {
                var length = TimeStampWrapper.GetFormattedTimeStampString(timestamp, ref messageOutput);
                // Timestamp not retrieved
                if (length <= 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Writes Timestamp to the UnsafeText in Local time zone
        /// </summary>
        /// <param name="timestamp">Timestamp to write</param>
        /// <param name="messageOutput">Where to write to</param>
        /// <returns>True on success</returns>
        public static bool WriteFormattedTimestampLocalTimeZone(in long timestamp, ref UnsafeText messageOutput)
        {
            var length = TimeStampWrapper.GetFormattedTimeStampStringLocalTime(timestamp, ref messageOutput);
            // Timestamp not retrieved
            if (length <= 0)
                return false;

            return true;
        }

        /// <summary>
        /// Writes Timestamp to the UnsafeText in Local time zone to show in the Console Window in the Editor (HH:MM:SS)
        /// </summary>
        /// <param name="timestamp">Timestamp to write</param>
        /// <param name="messageOutput">Where to write to</param>
        /// <returns>True on success</returns>
        public static bool WriteFormattedTimestampLocalTimeZoneForConsole(in long timestamp, ref UnsafeText messageOutput)
        {
            var length = TimeStampWrapper.GetFormattedTimeStampStringLocalTimeForConsole(timestamp, ref messageOutput);
            // Timestamp not retrieved
            if (length <= 0)
                return false;

            return true;
        }

        /// <summary>
        /// Appends string representation of <see cref="LogLevel"/> into <see cref="UnsafeText"/>
        /// </summary>
        /// <param name="level">Level to append</param>
        /// <param name="messageOutput">Where to append</param>
        /// <returns>True on success</returns>
        public static bool WriteFormattedLevel(LogLevel level, ref UnsafeText messageOutput)
        {
            if (s_OutputWriterLevelHandler.Data.IsCreated)
            {
#if LOGGING_USE_UNMANAGED_DELEGATES
                ((delegate * unmanaged[Cdecl] <ref UnsafeText, LogLevel, ContextWriteResult>)s_OutputWriterLevelHandler.Data.Value)(ref messageOutput, level);
#else
                s_OutputWriterLevelHandler.Data.Invoke(ref messageOutput, level);
#endif
            }
            else
            {
                // default
                messageOutput.Append(LogLevelUtilsBurstCompatible.ToFixedString(level));
            }

            return true;
        }

        /// <summary>
        /// Appends string representation of a binary data in the payload
        /// </summary>
        /// <param name="formatter">Current formatter</param>
        /// <param name="payload">PayloadHandle that points to the binary data</param>
        /// <param name="messageOutput">UnsafeText where to append</param>
        /// <param name="errorMessage">Returns an error message string, should a problem parsing the message occur.</param>
        /// <param name="memAllocator">Memory manager that holds binary representation of the mirror struct</param>
        /// <param name="currArgSlot">Hole that was used to describe the struct in the log message, for instance <c>{0}</c> or <c>{Number}</c> or <c>{Number:##.0;-##.0}</c></param>
        /// <returns>True on success</returns>
        public static bool WriteFormattedContextData(ref FormatterStruct formatter, in PayloadHandle payload, ref UnsafeText messageOutput, ref FixedString512Bytes errorMessage, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot)
        {
            if (memAllocator.RetrievePayloadBuffer(payload, out var contextBuffer) == false)
            {
                errorMessage = Errors.UnableToRetrieveContextDataFromLogMessageBuffer;
                SelfLog.Error(errorMessage);

                return false;
            }

            // Burst FunctionPointer only supports primitive data types so must get raw pointers to the data
            var ptrContextData = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks<byte>(contextBuffer);

            // Invoke each handler until a success/failure result is returned
            var result = ContextWriteResult.UnknownType;

            var mem = new BinaryParser(ptrContextData, contextBuffer.Length);

            var emptyHandlers = false;
            fixed (LogMemoryManager* ptrMem = &memAllocator)
            {
                var ptrMemAllocator = new IntPtr(ptrMem);

                ref var handlers = ref s_OutputWriterHandlers.Data;
                try
                {
                    var n = handlers.BeginRead();
                    emptyHandlers = n <= 0;
                    for (var i = n - 1; i >= 0; --i)
                    {
                        ref var func = ref handlers.ElementAt(i);

#if LOGGING_USE_UNMANAGED_DELEGATES
                        result = ((delegate * unmanaged[Cdecl] <ref FormatterStruct, ref UnsafeText, ref BinaryParser, IntPtr, ref ArgumentInfo, ContextWriteResult>)func.Value)(ref formatter, ref messageOutput, ref mem, ptrMemAllocator, ref currArgSlot);
#else
                        result = func.Invoke(ref formatter, ref messageOutput, ref mem, ptrMemAllocator, ref currArgSlot);
#endif
                        if (result != ContextWriteResult.UnknownType)
                            break;
                    }
                }
                finally
                {
                    handlers.EndRead();
                }
            }

            if (result == ContextWriteResult.UnknownType)
            {
                if (emptyHandlers)
                    SelfLog.OnUnknownTypeIdBecauseOfEmptyHandlers(*(ulong*)ptrContextData);
                else
                    SelfLog.OnUnknownTypeId(*(ulong*)ptrContextData);
            }

            // NOTE: Only return false if hit a "hard" error, as this will abort processing of any remaining context data.
            // For a "soft" error, e.g. UnknownType, we can still continue processing remaining arguments.

            return result != ContextWriteResult.Failed;
        }

        /// <summary>
        /// Appends a new line into the <see cref="UnsafeText"/>
        /// </summary>
        /// <param name="messageOutput">UnsafeText to append the new line</param>
        /// <returns>True on success</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteNewLine(ref UnsafeText messageOutput)
        {
            return messageOutput.Append(Builder.EnvNewLine.Data) == FormatError.None;
        }

        /// <summary>
        /// Appends all properties into the <see cref="UnsafeText"/>. For now not implemented
        /// </summary>
        /// <param name="messageOutput">UnsafeText to append the new line</param>
        /// <returns>True on success</returns>
        public static bool WriteProperties(ref UnsafeText messageOutput)
        {
            // TODO https://jira.unity3d.com/browse/MTT-1807

            return true;
        }
    }
}
