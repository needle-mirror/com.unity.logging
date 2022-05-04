using System;
using System.Diagnostics;
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
    public unsafe struct TextLoggerParser
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
            /// data type isn't recognized to instruct <see cref="TextLoggerParser"/> to try the next delegate.
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
        /// The <see cref="TextLoggerParser"/> calls into these delegates in order to write the struct data to the output string.
        /// Delegates can be added or removed from the active list of handlers by calling <see cref="AddOutputHandler(OutputWriterHandler)"/>
        /// and <see cref="RemoveOutputHandler(OutputWriterHandler)"/> respectively.
        /// By default, source generation will produce the output handlers and add the delegates to the handle list.
        ///
        /// NOTE: Burst FunctionPointers only support delegates with primitive parameter types; structs cannot be passed, even by reference.
        /// </remarks>
        /// <param name="outputUnsafeText">Reference to a UnsafeListString object to which the delegate writes the struct's data.</param>
        /// <param name="dataBuffer">Pointer to the byte buffer holding the current generated struct data.</param>
        /// <param name="bufferLength">Length of the dataBuffer</param>
        /// <returns>Value indicating delegate successfully handled the data or not.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate ContextWriteResult OutputWriterHandler(ref UnsafeText outputUnsafeText, byte* dataBuffer, int bufferLength);

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


        const int DecorationStartIndex = 2;


        // TODO: We cannot use a NativeList because the DisposeSentinel prevents us from using it in a Burst context.
        // If/When DisposeSentinel is removed from Native collections, use it instead of FixedList
        internal struct TextLoggerParserKey {}
        internal struct TextLoggerTimestampKey {}
        internal struct TextLoggerLevelKey {}
        internal static readonly SharedStatic<ThreadSafeFuncList<OutputWriterHandler>> s_OutputWriterHandlers = SharedStatic<ThreadSafeFuncList<OutputWriterHandler>>.GetOrCreate<ThreadSafeFuncList<OutputWriterHandler>, TextLoggerParserKey>(16);
        internal static readonly SharedStatic<FunctionPointer<OutputWriterTimestampHandler>> s_OutputWriterTimestampHandler = SharedStatic<FunctionPointer<OutputWriterTimestampHandler>>.GetOrCreate<FunctionPointer<OutputWriterTimestampHandler>, TextLoggerTimestampKey>(16);
        internal static readonly SharedStatic<FunctionPointer<OutputWriterLevelHandler>> s_OutputWriterLevelHandler = SharedStatic<FunctionPointer<OutputWriterLevelHandler>>.GetOrCreate<FunctionPointer<OutputWriterLevelHandler>, TextLoggerLevelKey>(16);


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
        [NotBurstCompatible]
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
        [NotBurstCompatible]
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
        [NotBurstCompatible]
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

        [NotBurstCompatible]
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

        [NotBurstCompatible]
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
            func.Invoke(ref burstWarmUpMessageOutput, buff.GetUnsafePtr(), buff.Length);
            burstWarmUpMessageOutput.Dispose();
        }

        // WarmUp functions is used to call Burst compilation in the main thread, otherwise it could run in another one and that will crash player
        private static void WarmUpFunctionInMainThread(FunctionPointer<LoggerManager.OutputWriterDecorateHandler> func)
        {
            var handles = new FixedList512Bytes<PayloadHandle>();
            var ctx = LogContextWithDecorator.From512(&handles);
            func.Invoke(ctx);
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
        /// Processes a TextLogger message and returns a text string containing the message text along with the context data referenced in the message.
        /// </summary>
        /// <remarks>
        /// This method is called by a TextLogger Sinks to extract the data from the message and generate a formatted text string, which can be written
        /// to an output stream. The message output is returned through a <see cref="UnsafeText"/> variable while any error messages are returned through
        /// a separate FixedString variable.
        ///
        /// The returned message string is allocated internally according to the size/length of the message, and therefore the caller should pass in an empty
        /// messageOutput variable. Furthermore, the string memory is allocated from the Temp pool and must not be referenced after the end of the frame. If
        /// the message needs to be held longer, it must be copied to a Persistent buffer. The caller should still Dispose the string once it's finished with it.
        ///
        /// If a problem occurs parsing/writing the output text, a separate errorMessage string is returned; a value of 'false' is returned in this case. Otherwise
        /// errorMessage string will be empty and 'true' is returned. Note that messageOutput may still hold valid text even if a error/failure occurs; it'll be
        /// a "partial" output string and missing some or all of the data.
        ///
        /// The caller must also pass in a reference to the <see cref="LogMemoryManager"/> instance holding the backing memory for the passed in <see cref="LogMessage"/>.
        /// Generally this is the MemoryManager within the <see cref="Logger"/> the Sink resides in.
        /// </remarks>
        /// <param name="template">Template for the message, like '{Timestamp} | {Level} | {Message}'</param>
        /// <param name="messageData">A <see cref="LogMessage"/> generated by the TextLogger to generated formatted output for.</param>
        /// <param name="messageOutput">Returns the text output generated from the log message.</param>
        /// <param name="errorMessage">Returns an error message string, should a problem parsing the message occur.</param>
        /// <param name="memAllocator">Reference to <see cref="LogMemoryManager"/> holding memory buffers for the passed in message.</param>
        /// <returns>True if text message was successfully generated and false if an error occurred.</returns>
        public static bool ParseMessage(in FixedString512Bytes template, in LogMessage messageData, ref UnsafeText messageOutput, ref FixedString512Bytes errorMessage, ref LogMemoryManager memAllocator)
        {
            var lockContext = memAllocator.LockPayloadBuffer(messageData.Payload);
            if (lockContext.IsValid)
            {
                var success = ParseContextMessage(template, messageData, out messageOutput, ref errorMessage, ref memAllocator);

                memAllocator.UnlockPayloadBuffer(messageData.Payload, lockContext);

                return success;
            }

            messageOutput = new UnsafeText();
            errorMessage = Errors.FailedToLockPayloadBuffer;
            SelfLog.Error(errorMessage);

            return false;
        }

        /// <summary>
        /// Parses the LogMessage to UnsafeText
        /// </summary>
        /// <param name="messageData">LogMessage to parse</param>
        /// <param name="template">Template to use for the parsing</param>
        /// <param name="memAllocator">LogMemoryManager to get data from</param>
        /// <returns>UnsafeText with parsed message</returns>
        public static UnsafeText ParseMessageTemplate(in LogMessage messageData, in FixedString512Bytes template, ref LogMemoryManager memAllocator)
        {
            var result = default(UnsafeText);
            var errorMessage = default(FixedString512Bytes);

            if (ParseMessage(in template, messageData, ref result, ref errorMessage, ref memAllocator))
            {
                return result;
            }

            SelfLog.OnFailedToParseMessage();

            return default;
        }

        /// <summary>
        /// Parses the LogMessage to Json UnsafeText
        /// </summary>
        /// <param name="messageData">LogMessage to parse</param>
        /// <param name="_">Ignored in this case</param>
        /// <param name="memAllocator">LogMemoryManager to get data from</param>
        /// <returns>UnsafeText with parsed message</returns>
        public static UnsafeText ParseMessageToJson(in LogMessage messageData, in FixedString512Bytes _, ref LogMemoryManager memAllocator)
        {
            var errorMessage = default(FixedString512Bytes);

            if (ParseContextMessageToJson(messageData, out var result, ref errorMessage, ref memAllocator))
            {
                return result;
            }

            SelfLog.OnFailedToParseMessage();

            return default;
        }

        internal enum ParseContextResult { NoArgs, NormalArg, EscOpenBrace, EscCloseBrace };

        [DebuggerDisplay("Offset = {Offset}, Length = {Length}")]
        internal struct ParseSegment
        {
            public int Offset;
            public int Length;

            public bool IsValid => Offset >= 0 && Length >= 0;
            public int OffsetEnd => Offset + Length;

            public static ParseSegment Reduce(in ParseSegment origin, int BytesFromLeft, int BytesFromRight)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsTrue(origin.IsValid);
#endif
                return new ParseSegment
                {
                    Offset = origin.Offset + BytesFromLeft,
                    Length = origin.Length - BytesFromLeft - BytesFromRight
                };
            }

            public static ParseSegment LeftPart(in ParseSegment origin, int splitPoint)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsTrue(origin.IsValid);
                Assert.IsTrue(splitPoint >= origin.Offset);
                Assert.IsTrue(splitPoint < origin.Offset + origin.Length);
#endif
                return new ParseSegment
                {
                    Offset = origin.Offset,
                    Length = splitPoint - origin.Offset
                };
            }

            public static ParseSegment RightPart(in ParseSegment origin, int splitPoint)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsTrue(origin.IsValid);
                Assert.IsTrue(splitPoint >= origin.Offset);
                Assert.IsTrue(splitPoint < origin.Offset + origin.Length);
#endif
                return new ParseSegment
                {
                    Offset = splitPoint,
                    Length = origin.Length - (splitPoint - origin.Offset)
                };
            }

            public static ParseSegment Local(in ParseSegment parent, in ParseSegment convertThis)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsTrue(parent.IsValid);
                Assert.IsTrue(convertThis.IsValid);
#endif
                return new ParseSegment
                {
                    Offset = convertThis.Offset - parent.Offset,
                    Length = convertThis.Length
                };
            }
        }

        private static bool ParseContextMessageToJson(in LogMessage messageData, out UnsafeText jsonOutput, ref FixedString512Bytes errorMessage, ref LogMemoryManager memAllocator)
        {
            jsonOutput = default;

            // Payload handle is expected to reference a DisjointedBuffer.
            // The "head" buffer's length from a DisjointedBuffer allocation must be a multiple of PayloadHandle size, If not something is very wrong.
            if (!memAllocator.RetrievePayloadBuffer(messageData.Payload, out var headBuffer) || headBuffer.Length % UnsafeUtility.SizeOf<PayloadHandle>() != 0)
            {
                errorMessage = Errors.UnableToRetrieveDisjointedMessageBuffer;
                SelfLog.Error(errorMessage);
                return false;
            }

            var payloads = (PayloadHandle*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(headBuffer);
            var numPayloads = headBuffer.Length / UnsafeUtility.SizeOf<PayloadHandle>();

            if (numPayloads <= 1)
            {
                errorMessage = Errors.UnableToRetrieveDisjointedMessageBuffer;
                SelfLog.Error(errorMessage);
                return false;
            }

            if (!memAllocator.RetrievePayloadBuffer(payloads[0], out var messageBuffer))
            {
                errorMessage = Errors.UnableToRetrieveMessageFromContextBuffer;
                SelfLog.Error(errorMessage);
                return false;
            }

            var stackTraceIdData = messageData.StackTraceId;

            if (!memAllocator.RetrievePayloadBuffer(payloads[1], out var decorationInfo))
            {
                errorMessage = Errors.UnableToRetrieveDecoratorsInfo;
                SelfLog.Error(errorMessage);
                return false;
            }

            var timestamp = messageData.Timestamp;
            var logLevel = messageData.Level;

            // 0 handle is message
            // 1 handle is decoration header (contains info about localConstHandlesCount, globalConstHandlesCount and total decorator count, see BuildDecorators
            // [total decorator count] payloads
            // till the end - context buffers

            // decoration info
            var decorationIsCorrect = ExtractDecorationInfo(decorationInfo, numPayloads, out var localConstPayloadsCount, out var globalConstPayloadsCount, out var totalDecorationCount);
            if (decorationIsCorrect == false)
            {
                errorMessage = Errors.CorruptedDecorationInfo;
                SelfLog.Error(errorMessage);
                return false;
            }

            // Context buffers should always occupy the last set of payloads.
            int contextStartIndex = DecorationStartIndex + totalDecorationCount;

            int numContextBuffers = numPayloads - contextStartIndex;

            // Estimate initial size of the output buffer; UnsafeListString will resize by re-allocating memory (like to avoid), so try to get a ballpark size guess.
            int sizeEstimate = messageBuffer.Length + numPayloads * 256;

            var rawMsgBuffer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(messageBuffer);
            var rawMsgBufferLength = messageBuffer.Length;

            var tempBuffer = new UnsafeText(rawMsgBufferLength * 2, Allocator.Temp);

            jsonOutput = new UnsafeText(sizeEstimate, Allocator.Temp);
            {
                jsonOutput.Append((FixedString32Bytes)"{\"Timestamp\":\"");
                tempBuffer.Clear();
                WriteFormattedTimestamp(timestamp, ref tempBuffer);
                AppendEscapedJsonString(ref jsonOutput, tempBuffer.GetUnsafePtr(), tempBuffer.Length);
            }
            {
                jsonOutput.Append((FixedString32Bytes)"\",\"Level\":\"");
                tempBuffer.Clear();
                WriteFormattedLevel(logLevel, ref tempBuffer);
                AppendEscapedJsonString(ref jsonOutput, tempBuffer.GetUnsafePtr(), tempBuffer.Length);
            }
            jsonOutput.Append((FixedString32Bytes)"\",\"Message\":\"");
            AppendEscapedJsonString(ref jsonOutput, rawMsgBuffer, rawMsgBufferLength);

            if (stackTraceIdData != 0)
            {
                tempBuffer.Clear();
                ManagedStackTraceWrapper.AppendToUnsafeText(stackTraceIdData, ref tempBuffer);
                jsonOutput.Append((FixedString32Bytes)"\",\"Stacktrace\":\"");

                AppendEscapedJsonString(ref jsonOutput, tempBuffer.GetUnsafePtr(), tempBuffer.Length);
            }

            jsonOutput.Append((FixedString32Bytes)"\",\"Properties\":{");

            var currMsgSegment = new ParseSegment();

            var argIndexInString = -1;
            var done = false;
            var success = true;
            var firstArg = true;
            var hashName = new NativeParallelHashSet<FixedString512Bytes>(128, Allocator.Temp);
            do
            {
                var result = FindNextParseStringSegment(in rawMsgBuffer, in rawMsgBufferLength, ref currMsgSegment, out var currArgSlot);

                switch (result)
                {
                    case ParseContextResult.NormalArg:
                    {
                        var rawBuffer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(messageBuffer);
                        var arg = ParseArgument(rawBuffer, currArgSlot.OffsetEnd, currArgSlot);

                        if (arg.IsValid)
                            ++argIndexInString;

                        if (arg.IsBuiltIn == false)
                        {
                            var contextIndex = arg.Index;

                            var jsonName = arg.Name;

                            if (jsonName == (FixedString32Bytes)"")
                            {
                                jsonName.Append((FixedString32Bytes)"arg");
                                jsonName.Append(arg.Index);
                            }
                            else
                            {
                                contextIndex = argIndexInString;
                            }

                            if (contextIndex < 0 || contextIndex >= numContextBuffers)
                            {
                                errorMessage = Errors.UnableToRetrieveValidContextArgumentIndex;
                                SelfLog.Error(errorMessage);

                                success = false;
                            }
                            else
                            {
                                tempBuffer.Clear();
                                success = WriteFormattedContextData(payloads[contextStartIndex + contextIndex], ref tempBuffer, ref errorMessage, ref memAllocator) && success;
                                if (success)
                                    WriteProperty(ref jsonOutput, ref jsonName, ref tempBuffer, ref hashName, ref firstArg);
                            }
                        }

                        break;
                    }
                    case ParseContextResult.NoArgs:
                        done = true;
                        break;
                }

                currMsgSegment.Offset = currArgSlot.Offset + currArgSlot.Length;
                if (currMsgSegment.Offset >= messageBuffer.Length)
                    done = true;
            }
            while (!done);

            var tempNameBuffer = new FixedString512Bytes();
            for (var i = 0; i < totalDecorationCount; i += 2)
            {
                if (!memAllocator.RetrievePayloadBuffer(payloads[DecorationStartIndex + i], out var namePayload))
                {
                    errorMessage = Errors.UnableToRetrieveDecoratorsInfo;
                    SelfLog.Error(errorMessage);
                    return false;
                }

                var namePayloadBuffer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(namePayload);
                var namePayloadBufferLength = namePayload.Length;

                tempNameBuffer.Clear();
                tempNameBuffer.Append(namePayloadBuffer, namePayloadBufferLength);

                tempBuffer.Clear();

                var getValueSuccess = WriteFormattedContextData(payloads[DecorationStartIndex + i + 1], ref tempBuffer, ref errorMessage, ref memAllocator);
                if (getValueSuccess)
                    WriteProperty(ref jsonOutput, ref tempNameBuffer, ref tempBuffer, ref hashName, ref firstArg);
                else
                    success = false;
            }

            static bool WriteProperty(ref UnsafeText jsonOutput, ref FixedString512Bytes jsonName, ref UnsafeText jsonVal, ref NativeParallelHashSet<FixedString512Bytes> hashName, ref bool firstArg)
            {
                if (hashName.Add(jsonName))
                {
                    if (firstArg == false)
                        jsonOutput.Append(',');
                    jsonOutput.Append('"');

                    firstArg = false;

                    AppendEscapedJsonString(ref jsonOutput, jsonName.GetUnsafePtr(), jsonName.Length);
                    jsonOutput.Append((FixedString32Bytes)"\":");

                    jsonOutput.Append('"');
                    AppendEscapedJsonString(ref jsonOutput, jsonVal.GetUnsafePtr(), jsonVal.Length);
                    jsonOutput.Append('"');

                    return true;
                }

                return false;
            }

            hashName.Dispose();

            jsonOutput.Append((FixedString32Bytes)"}}");

            // Regardless of current success value, if an error was raised return "failed"
            // NOTE: Internally we may return  "success" even when some errors are raised so as to continue parsing the message.
            if (!errorMessage.IsEmpty)
                success = false;

            return success;
        }

        private static bool ExtractDecorationInfo(NativeArray<byte> decorationInfo, int totalPayloadCount, out ushort localConstPayloadsCount, out ushort globalConstPayloadsCount, out ushort totalDecorCount)
        {
            totalDecorCount = localConstPayloadsCount = globalConstPayloadsCount = 0;

            var decorationSizeInBytes = decorationInfo.Length;
            if (decorationSizeInBytes != sizeof(ushort) * 3)
                return false;

            var payloadLocalCountPtr = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(decorationInfo);
            localConstPayloadsCount = *payloadLocalCountPtr++;
            globalConstPayloadsCount = *payloadLocalCountPtr++;
            totalDecorCount = *payloadLocalCountPtr;

            if (totalDecorCount == 0 && localConstPayloadsCount == 0 && globalConstPayloadsCount == 0)
                return true; // empty

            if (totalDecorCount % 2 != 0)
                return false;

            var totalConstPayloads = localConstPayloadsCount + globalConstPayloadsCount;

            // totalDecorCount is totalConstPayloads + handles payloads
            if (totalConstPayloads > totalDecorCount)
                return false;

            // decor + message + timestamp_level + this_header cannot be more than total payload count
            if (totalDecorCount + DecorationStartIndex > totalPayloadCount)
                return false;

            return true;
        }

        internal static void AppendEscapedJsonString(ref UnsafeText jsonOutput, byte* rawMsgBuffer, int rawMsgBufferLength)
        {
            for (var offset = 0; offset < rawMsgBufferLength;)
            {
                Unicode.Utf8ToUcs(out var rune, rawMsgBuffer, ref offset, rawMsgBufferLength);

                // https://www.json.org/json-en.html
                // https://www.ietf.org/rfc/rfc4627.txt

                // must be escaped:
                //quotation mark, reverse solidus, and the control characters (U+0000
                //through U+001F).

                var controlCharC0 = rune.value <= 0x1F || rune.value == 0x7F;
                var controlCharC1 = rune.value >= 0x80 && rune.value <= 0x9F;
                var controlUnicode = rune.value == 0x85;

                var controlChar = controlCharC0 || controlCharC1 || controlUnicode;

                if (controlChar == false)
                {
                    var prependSlash = rune.value == '"' || rune.value == '\\';// || rune.value == '/';
                    if (prependSlash)
                    {
                        jsonOutput.Append('\\');
                    }

                    jsonOutput.Append(rune);
                }
                else
                {
                    if (rune.value == 0)
                    {
                        // null terminator
                        break;
                    }

                    if (rune.value == 8)
                    {
                        // \b  Backspace
                        jsonOutput.Append('\\');
                        jsonOutput.Append('b');
                    }
                    else if (rune.value == 12)
                    {
                        // \f  Form feed (0C)
                        jsonOutput.Append('\\');
                        jsonOutput.Append('f');
                    }
                    else if (rune.value == 10)
                    {
                        // \n linefeed (0A)
                        jsonOutput.Append('\\');
                        jsonOutput.Append('n');
                    }
                    else if (rune.value == 13)
                    {
                        // \r carriage return (0D)
                        jsonOutput.Append('\\');
                        jsonOutput.Append('r');
                    }
                    else if (rune.value == 9)
                    {
                        // \t tab
                        jsonOutput.Append('\\');
                        jsonOutput.Append('t');
                    }
                    else
                    {
                        jsonOutput.Append('\\');
                        jsonOutput.Append('u');
                        // 0000
                        var i = 0;
                        var n = rune.value;
                        FixedString32Bytes hexaDeciNum = default;
                        hexaDeciNum.Append('0');
                        hexaDeciNum.Append('0');
                        hexaDeciNum.Append('0');
                        hexaDeciNum.Append('0');
                        while (n != 0 && i <= 3)
                        {
                            var temp = n % 16;

                            if (temp < 10)
                                hexaDeciNum[3-i] = (byte)(temp + '0');
                            else
                                hexaDeciNum[3-i] = (byte)(temp + 'a' - 10);

                            i++;
                            n /= 16;
                        }
                        jsonOutput.Append(hexaDeciNum);
                    }
                }
            }
        }

        private static bool ParseContextMessage(FixedString512Bytes template, in LogMessage messageData, out UnsafeText messageOutput, ref FixedString512Bytes errorMessage, ref LogMemoryManager memAllocator)
        {
            messageOutput = default;

            // Payload handle is expected to reference a DisjointedBuffer.
            // The "head" buffer's length from a DisjointedBuffer allocation must be a multiple of PayloadHandle size, If not something is very wrong.
            if (!memAllocator.RetrievePayloadBuffer(messageData.Payload, out var headBuffer) || headBuffer.Length % UnsafeUtility.SizeOf<PayloadHandle>() != 0)
            {
                errorMessage = Errors.UnableToRetrieveDisjointedMessageBuffer;
                SelfLog.Error(errorMessage);
                return false;
            }

            var payloads = (PayloadHandle*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(headBuffer);
            var numPayloads = headBuffer.Length / UnsafeUtility.SizeOf<PayloadHandle>();

            if (numPayloads <= 1)
            {
                errorMessage = Errors.UnableToRetrieveDisjointedMessageBuffer;
                SelfLog.Error(errorMessage);
                return false;
            }

            if (!memAllocator.RetrievePayloadBuffer(payloads[0], out var messageBuffer))
            {
                errorMessage = Errors.UnableToRetrieveMessageFromContextBuffer;
                SelfLog.Error(errorMessage);
                return false;
            }

            var stackTraceIdData = messageData.StackTraceId;

            if (!memAllocator.RetrievePayloadBuffer(payloads[1], out var decorationInfo))
            {
                errorMessage = Errors.UnableToRetrieveDecoratorsInfo;
                SelfLog.Error(errorMessage);
                return false;
            }

            var timestamp = messageData.Timestamp;
            var logLevel = messageData.Level;

            // decoration info
            var decorationIsCorrect = ExtractDecorationInfo(decorationInfo, numPayloads, out _, out _, out var totalDecorationCount);
            if (decorationIsCorrect == false)
            {
                errorMessage = Errors.CorruptedDecorationInfo;
                SelfLog.Error(errorMessage);
                return false;
            }

            // Context buffers should always occupy the last set of payloads.
            int contextStartIndex = DecorationStartIndex + totalDecorationCount;

            int numContextBuffers = numPayloads - contextStartIndex;

            // Estimate initial size of the output buffer; UnsafeListString will resize by re-allocating memory (like to avoid), so try to get a ballpark size guess.
            int sizeEstimate = messageBuffer.Length + numPayloads * 256;
            messageOutput = new UnsafeText(sizeEstimate, Allocator.Temp);

            byte* rawMsgBuffer = template.GetUnsafePtr();
            var currMsgSegment = new ParseSegment();

            bool success = true;
            bool done = false;
            do
            {
                var result = FindNextParseStringSegment(in rawMsgBuffer, template.Length, ref currMsgSegment, out var currArgSlot);

                success = messageOutput.Append(&rawMsgBuffer[currMsgSegment.Offset], currMsgSegment.Length) == FormatError.None && success;

                switch (result)
                {
                    case ParseContextResult.EscOpenBrace:
                    {
                        success = messageOutput.Append('{') == FormatError.None && success;
                        break;
                    }
                    case ParseContextResult.EscCloseBrace:
                    {
                        success = messageOutput.Append('}') == FormatError.None && success;
                        break;
                    }
                    case ParseContextResult.NormalArg:
                    {
                        int contextIndex = RetrieveContextArgumentIndex(rawMsgBuffer, currArgSlot, isThisTemplate: true);
                        if (contextIndex == BuiltInLevelId)
                        {
                            success = WriteFormattedLevel(logLevel, ref messageOutput) && success;
                        }
                        else if (contextIndex == BuiltInTimestampId)
                        {
                            success = WriteFormattedTimestamp(timestamp, ref messageOutput) && success;
                        }
                        else if (contextIndex == BuiltInMessage)
                        {
                            success = WriteMessage(in messageBuffer, logLevel, timestamp, payloads, contextStartIndex, numContextBuffers, stackTraceIdData, ref messageOutput, ref errorMessage, ref memAllocator) && success;
                        }
                        else if (contextIndex == BuiltInStackTrace)
                        {
                            ManagedStackTraceWrapper.AppendToUnsafeText(stackTraceIdData, ref messageOutput);
                        }
                        break;
                    }
                    case ParseContextResult.NoArgs:
                        done = true;
                        break;
                }

                currMsgSegment.Offset = currArgSlot.Offset + currArgSlot.Length;
                if (currMsgSegment.Offset >= template.Length)
                    done = true;
            }
            while (!done);


            // Regardless of current success value, if an error was raised return "failed"
            // NOTE: Internally we may return  "success" even when some errors are raised so as to continue parsing the message.
            if (!errorMessage.IsEmpty)
                success = false;

            return success;
        }

        private static bool WriteMessage(in NativeArray<byte> messageBuffer, LogLevel logLevel, long timestamp, PayloadHandle* payloads, int contextStartIndex, int numContextBuffers, long stackTraceIdData, ref UnsafeText messageOutput,
                                         ref FixedString512Bytes errorMessage, ref LogMemoryManager memAllocator)
        {
            var rawMsgBuffer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(messageBuffer);
            var rawMsgBufferLength = messageBuffer.Length;

            var currMsgSegment = new ParseSegment();
            var argIndexInString = -1;
            var done = false;
            var success = true;
            do
            {
                var result = FindNextParseStringSegment(in rawMsgBuffer, in rawMsgBufferLength, ref currMsgSegment, out var currArgSlot);

                success = messageOutput.Append(&rawMsgBuffer[currMsgSegment.Offset], currMsgSegment.Length) == FormatError.None && success;

                switch (result)
                {
                    case ParseContextResult.EscOpenBrace:
                    {
                        success = messageOutput.Append('{') == FormatError.None && success;
                        break;
                    }
                    case ParseContextResult.EscCloseBrace:
                    {
                        success = messageOutput.Append('}') == FormatError.None && success;
                        break;
                    }
                    case ParseContextResult.NormalArg:
                    {
                        var rawBuffer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(messageBuffer);
                        var arg = ParseArgument(rawBuffer, currArgSlot.OffsetEnd, currArgSlot);

                        if (arg.IsValid)
                            ++argIndexInString;

                        if (arg.IsBuiltInLevel)
                        {
                            success = WriteFormattedLevel(logLevel, ref messageOutput) && success;
                        }
                        else if (arg.IsBuiltInTimestamp)
                        {
                            success = WriteFormattedTimestamp(timestamp, ref messageOutput) && success;
                        }
                        else if (arg.IsBuiltInStackTrace)
                        {
                            ManagedStackTraceWrapper.AppendToUnsafeText(stackTraceIdData, ref messageOutput);
                        }
                        else if (arg.IsBuiltIn == false)
                        {
                            var contextIndex = arg.Index;
                            if (arg.Name != (FixedString32Bytes)"")
                            {
                                contextIndex = argIndexInString;
                            }

                            if (contextIndex < 0 || contextIndex >= numContextBuffers)
                            {
                                errorMessage = Errors.UnableToRetrieveValidContextArgumentIndex;
                                SelfLog.Error(errorMessage);

                                success = false;
                            }
                            else
                            {
                                success = WriteFormattedContextData(payloads[contextStartIndex + contextIndex], ref messageOutput, ref errorMessage, ref memAllocator) && success;
                            }
                        }
                        else
                        {
                            errorMessage = Errors.UnableToRetrieveValidContextArgumentIndex;
                            SelfLog.Error(errorMessage);
                            success = false;
                        }

                        break;
                    }
                    case ParseContextResult.NoArgs:
                        done = true;
                        break;
                }

                currMsgSegment.Offset = currArgSlot.Offset + currArgSlot.Length;
                if (currMsgSegment.Offset >= messageBuffer.Length)
                    done = true;
            }
            while (!done);

            return success;
        }

        private static bool WriteFormattedTimestamp(in long timestamp, ref UnsafeText messageOutput)
        {
            if (s_OutputWriterTimestampHandler.Data.IsCreated)
            {
                s_OutputWriterTimestampHandler.Data.Invoke(ref messageOutput, timestamp);
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

        private static bool WriteFormattedLevel(in LogLevel level, ref UnsafeText messageOutput)
        {
            if (s_OutputWriterLevelHandler.Data.IsCreated)
            {
                s_OutputWriterLevelHandler.Data.Invoke(ref messageOutput, level);
            }
            else
            {
                // default
                messageOutput.Append(LogLevelUtilsBurstCompatible.ToFixedString(level));
            }

            return true;
        }

        private static bool WriteFormattedContextData(in PayloadHandle payload, ref UnsafeText messageOutput, ref FixedString512Bytes errorMessage, ref LogMemoryManager memAllocator)
        {
            if (!memAllocator.RetrievePayloadBuffer(payload, out var contextBuffer))
            {
                errorMessage = Errors.UnableToRetrieveContextDataFromLogMessageBuffer;
                SelfLog.Error(errorMessage);

                return false;
            }

            // Burst FunctionPointer only supports primitive data types so must get raw pointers to the data
            void* ptrContextData = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks<byte>(contextBuffer);

            // Invoke each handler until a success/failure result is returned
            var result = ContextWriteResult.UnknownType;

            ref var handlers = ref s_OutputWriterHandlers.Data;
            var n = handlers.BeginRead();
            try
            {
                for (var i = 0; i < n; i++)
                {
                    result = handlers.ElementAt(i).Invoke(ref messageOutput, (byte*)ptrContextData, contextBuffer.Length);
                    if (result != ContextWriteResult.UnknownType)
                        break;
                }
            }
            finally
            {
                handlers.EndRead();
            }

            if (result == ContextWriteResult.UnknownType)
            {
                errorMessage = Errors.UnknownTypeId;
                errorMessage.Append(*(ulong*)ptrContextData);

                SelfLog.Error(errorMessage);
            }

            // NOTE: Only return false if hit a "hard" error, as this will abort processing of any remaining context data.
            // For a "soft" error, e.g. UnknownType, we can still continue processing remaining arguments.

            return result != ContextWriteResult.Failed;
        }

        /// <summary>
        /// Parsed Argument/Hole data
        /// </summary>
        public readonly struct ArgumentInfo
        {
            /// <summary>
            /// Enum for the name - with @, with $ or without
            /// </summary>
            public enum DestructingType
            {
                Default = 0,
                Destructure, // @
                Stringify    // $
            }

            /// <summary>
            /// See <see cref="DestructingType"/>
            /// </summary>
            public readonly DestructingType Destructing;

            /// <summary>
            /// Name of the hole
            /// </summary>
            public readonly FixedString512Bytes Name;   //  [0-9A-z_]+

            /// <summary>
            /// Indexed hole (if no <see cref="Name"/> was specified)
            /// </summary>
            public readonly int Index;             // [0-9]+

            /// <summary>
            /// Alignment that is specified after ','
            /// </summary>
            public readonly int Alignment;         // '-'? [0-9]+

            /// <summary>
            /// Format that is specified after ':'
            /// </summary>
            public readonly FixedString512Bytes Format; // [^\{]+

            /// <summary>
            /// True if created
            /// </summary>
            public readonly bool IsValid;

            public readonly bool IsBuiltIn;
            public readonly bool IsBuiltInTimestamp;
            public readonly bool IsBuiltInLevel;
            public readonly bool IsBuiltInMessage;
            public readonly bool IsBuiltInStackTrace;

            /// <summary>
            /// Create a hole from a number (index)
            /// </summary>
            /// <param name="i">index to use</param>
            /// <returns>ArgumentInfo with Index == i</returns>
            public static ArgumentInfo Number(int i)
            {
                return new ArgumentInfo(i);
            }

            public ArgumentInfo(int index)
            {
                Index = index;
                IsValid = true;

                Destructing = DestructingType.Default;
                Name = default;
                Alignment = 0;
                Format = default;

                IsBuiltIn = IsBuiltInTimestamp = IsBuiltInLevel = IsBuiltInMessage = IsBuiltInStackTrace = false;
            }

            public ArgumentInfo(int index, FixedString512Bytes name, DestructingType destructingType, FixedString512Bytes format, int alignment)
            {
                Index = index;

                Destructing = destructingType;
                Name = name;
                Alignment = alignment;
                Format = format;

                IsBuiltInTimestamp = Name == "Timestamp";
                IsBuiltInMessage = Name == "Message";
                IsBuiltInLevel = Name == "Level";
                IsBuiltInStackTrace = Name == "Stacktrace";
                IsBuiltIn = IsBuiltInTimestamp || IsBuiltInMessage || IsBuiltInLevel || IsBuiltInStackTrace;

                IsValid = true;
            }
        }

        internal static ArgumentInfo ParseArgument(byte* rawMsgBuffer, int rawMsgBufferLength, in ParseSegment currArgSlot)
        {
            if (rawMsgBufferLength == 0 || currArgSlot.Length <= 2 || currArgSlot.OffsetEnd > rawMsgBufferLength)
                return default;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.AreEqual('{', rawMsgBuffer[currArgSlot.Offset]);
            Assert.AreEqual('}', rawMsgBuffer[currArgSlot.Offset + currArgSlot.Length - 1]);
#endif

            if (currArgSlot.Length == 3)
            {
                // fast track for {0}, {1}, ... {9}
                var c = rawMsgBuffer[currArgSlot.Offset + 1];
                if (c >= '0' && c <= '9')
                {
                    return ArgumentInfo.Number(c - '0');
                }
            }

            var rawString = new FixedString512Bytes();
            rawString.Append(&rawMsgBuffer[currArgSlot.Offset], currArgSlot.Length);

            var bodySegment = ParseSegment.Reduce(currArgSlot, 1, 1);

            ArgumentInfo.DestructingType destructingType = ArgumentInfo.DestructingType.Default;

            var firstCharInName = rawMsgBuffer[bodySegment.Offset];
            if (firstCharInName == '@' || firstCharInName == '$')
            {
                destructingType = firstCharInName == '@' ? ArgumentInfo.DestructingType.Destructure : ArgumentInfo.DestructingType.Stringify;
                bodySegment = ParseSegment.Reduce(bodySegment, 1, 0);
            }

            if (bodySegment.Length <= 0)
                return default; // no name

            var bodySegmentEnd = bodySegment.OffsetEnd;
            var bodySegmentNewEnd = bodySegmentEnd;

            var formatSegment = new ParseSegment { Length = -1, Offset = -1};
            var alignmentSegment = new ParseSegment { Length = -1, Offset = -1};

            for (var i = bodySegment.Offset; i < bodySegmentEnd;)
            {
                var prevI = i;
                var res = Unicode.Utf8ToUcs(out var rune, rawMsgBuffer, ref i, rawMsgBufferLength);
                Assert.IsTrue(res == ConversionError.None);

                if (rune.value == ':')
                {
                    if (bodySegmentNewEnd > prevI)
                        bodySegmentNewEnd = prevI;

                    if (alignmentSegment.Offset != -1) // we detected alignmentSegment before, finish it here
                        alignmentSegment.Length = prevI - alignmentSegment.Offset;

                    // everything to the right is a 'format'
                    formatSegment = ParseSegment.RightPart(bodySegment, i);
                    break;
                }

                if (rune.value == ',')
                {
                    if (bodySegmentNewEnd > prevI)
                        bodySegmentNewEnd = prevI;

                    alignmentSegment.Offset = i;
                }
            }

            // we detected alignmentSegment before and didn't finish, so finish it here
            if (alignmentSegment.Offset != -1 && alignmentSegment.Length == -1)
            {
                alignmentSegment.Length = bodySegmentEnd - alignmentSegment.Offset;
            }

            FixedString512Bytes format = "";
            if (formatSegment.IsValid)
            {
                format.Append(&rawMsgBuffer[formatSegment.Offset], formatSegment.Length);

                // not burst compatible
                // foreach (var c in format)
                // {
                //     var valid = c.value == ' ' || c.value == '+' || Unicode.Rune.IsDigit(c) || char.IsLetterOrDigit((char)c.value) || char.IsPunctuation((char)c.value);
                //     if (valid == false)
                //         return default;
                // }
            }

            var alignment = 0;
            if (alignmentSegment.IsValid)
            {
                var rawStringLocalAlignmentSegment = ParseSegment.Local(currArgSlot, alignmentSegment);

                var alignmentSegmentParseOffset = rawStringLocalAlignmentSegment.Offset;

                if (rawString.Parse(ref alignmentSegmentParseOffset, ref alignment) == ParseError.None)
                {
                    if (alignment == 0) // ',-0' or ',0' are illegal
                        return default;

                    if (alignmentSegmentParseOffset != rawStringLocalAlignmentSegment.OffsetEnd) // there is an int, but also something else
                        return default;
                }
                else
                    return default; // not an int
            }

            if (bodySegmentNewEnd < bodySegmentEnd)
                bodySegment = ParseSegment.LeftPart(bodySegment, bodySegmentNewEnd);

            var rawStringLocalNameSegment = ParseSegment.Local(currArgSlot, bodySegment);

            FixedString512Bytes name = default;
            var index = 0;

            var nameSegmentParseOffset = rawStringLocalNameSegment.Offset;
            if (rawString.Parse(ref nameSegmentParseOffset, ref index) == ParseError.None && nameSegmentParseOffset == rawStringLocalNameSegment.OffsetEnd)
            {
                // name is just an integer

                if (index == 0 && rawStringLocalNameSegment.Length > 1) // -0
                    return default;

                if (index < 0)
                    return default;
            }
            else
            {
                name.Append(&rawMsgBuffer[bodySegment.Offset], bodySegment.Length);

                foreach (var rune in name)
                {
                    var valid = rune.value != '.' && rune.value != '{' && rune.value != '}' && rune.value != '-' && rune.value != '@' && rune.value != '$' && rune.value != '&' && rune.value != ' ';
                    //var valid = rune.value == '_' || char.IsLetterOrDigit((char)rune.value); //-- not burst compatible

                    if (valid == false)
                        return default;
                }

                index = -1;
            }

            return new ArgumentInfo(index, name, destructingType, format, alignment);
        }

        internal static unsafe ParseContextResult FindNextParseStringSegment(in byte* rawBuffer, in int rawBufferLength, ref ParseSegment currMsgSegment, out ParseSegment argSlot)
        {
            int endMsgOffset = rawBufferLength;
            int argOpenOffset = -1;
            int argCloseOffset = -1;
            bool validSlot = false;
            bool escapeOpenBrace = false;
            bool escapeCloseBrace = false;

            int currWorkingIndex = currMsgSegment.Offset;
            int readIndex = currWorkingIndex;

            while (readIndex < endMsgOffset)
            {
                currWorkingIndex = readIndex;

                // Iterate over each UTF-8 char until we find the start of a format argument slot '{' or reach the end
                // of the message segment (meaning has no format slots)
                if (Unicode.Utf8ToUcs(out var currChar, rawBuffer, ref readIndex, rawBufferLength) != ConversionError.None)
                    continue;

                // End of message string
                if (readIndex >= endMsgOffset)
                    break;

                // "Peek" at next char but don't advance our readIndex
                int tempPos = readIndex;
                if (Unicode.Utf8ToUcs(out var nextChar, rawBuffer, ref tempPos, rawBufferLength) != ConversionError.None)
                    continue;

                // Check for escaped braces ("{{" and "}}") which we'll treat as special case format slots were the
                // double brace is replaced by an argument string holding a single brace char
                if (currChar.value == '{' && nextChar.value == '{')
                {
                    argOpenOffset = currWorkingIndex;
                    argCloseOffset = currWorkingIndex + 1;
                    escapeOpenBrace = true;
                }
                else if (currChar.value == '}' && nextChar.value == '}')
                {
                    argOpenOffset = currWorkingIndex;
                    argCloseOffset = currWorkingIndex + 1;
                    escapeCloseBrace = true;
                }
                else if (currChar.value == '{' && tempPos < endMsgOffset)
                {
                    // Found a valid Open to a parameter slot, make sure it also has a valid Close
                    argOpenOffset = currWorkingIndex;
                    readIndex = tempPos;

                    while (readIndex < endMsgOffset)
                    {
                        currWorkingIndex = readIndex;

                        if (Unicode.Utf8ToUcs(out currChar, rawBuffer, ref readIndex, rawBufferLength) != ConversionError.None)
                            continue;

                        // Find the first closing brace
                        // NOTE: Won't check for escaping '}' because of this situation: "{{{0}}}" which we expect to output: "{hello}"
                        // If we check for escaped brace we'll end up with "{0}}": first pair of closing braces produces a '}' which invalidates format expansion
                        if (currChar.value == '}')
                        {
                            argCloseOffset = currWorkingIndex;
                            break;
                        }
                    }
                }
                else if (tempPos >= endMsgOffset)
                {
                    // Even if found a valid '{' we're at the end of the message so won't be a valid close
                    break;
                }
                else continue;

                if (argOpenOffset >= 0 && argCloseOffset >= 0 && argCloseOffset > argOpenOffset && argOpenOffset >= currMsgSegment.Offset)
                {
                    validSlot = true;
                    break;
                }
            }

            // If a valid slot was found, update the parser's state to save the arg's position
            // Otherwise advance the read length to the end of the message string (no valid slots).
            ParseContextResult result;
            if (validSlot)
            {
                currMsgSegment.Length = argOpenOffset - currMsgSegment.Offset;

                argSlot = new ParseSegment
                {
                    Offset = argOpenOffset,
                    Length = argCloseOffset - argOpenOffset + 1,
                };

                if (escapeOpenBrace)
                {
                    result = ParseContextResult.EscOpenBrace;
                }
                else if (escapeCloseBrace)
                {
                    result = ParseContextResult.EscCloseBrace;
                }
                else
                {
                    result = ParseContextResult.NormalArg;
                }
            }
            else
            {
                argSlot = new ParseSegment();
                result = ParseContextResult.NoArgs;

                // Disregard any NULs at the end of the buffer; any 0 values at the end should just be padding
                int remainingLength = endMsgOffset - currMsgSegment.Offset;
                if (remainingLength > 0)
                {
                    while (remainingLength > 0 && rawBuffer[currMsgSegment.Offset + remainingLength - 1] == 0)
                        remainingLength--;
                }
                currMsgSegment.Length = remainingLength;
            }

            return result;
        }

        private static unsafe int RetrieveContextArgumentIndex(in NativeArray<byte> msgBuffer, in ParseSegment argSlotSegment, bool isThisTemplate)
        {
            var rawBuffer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(msgBuffer);
            return RetrieveContextArgumentIndex(in rawBuffer, in argSlotSegment, isThisTemplate);
        }

        private static unsafe int RetrieveContextArgumentIndex(in ArgumentInfo arg, bool isThisTemplate)
        {
            if (arg.IsValid == false)
                return -1;

            if (arg.IsBuiltInTimestamp) return BuiltInTimestampId;
            if (arg.IsBuiltInLevel) return BuiltInLevelId;
            if (arg.IsBuiltInStackTrace) return BuiltInStackTrace;

            if (isThisTemplate)
            {
                // template only messages
                if (arg.IsBuiltInMessage) return BuiltInMessage;

                return -1; // Index cannot be used in the template
            }

            return arg.Index;
        }

        private static unsafe int RetrieveContextArgumentIndex(in byte* rawBuffer, in ParseSegment argSlotSegment, bool isThisTemplate)
        {
            var arg = ParseArgument(rawBuffer, argSlotSegment.OffsetEnd, argSlotSegment);

            return RetrieveContextArgumentIndex(arg, isThisTemplate);
        }

        private const int BuiltInTimestampId = -100;
        private const int BuiltInLevelId = -101;
        private const int BuiltInMessage = -102;
        private const int BuiltInStackTrace = -103;
    }
}
