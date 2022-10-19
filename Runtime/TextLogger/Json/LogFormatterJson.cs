using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IL2CPP.CompilerServices;
using Unity.Logging.Internal;
using Unity.Logging.Internal.Debug;
using Unity.Logging.Sinks;

using IntHashSet = Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashSet<int>;

namespace Unity.Logging
{
    /// <summary>
    /// Json structured formatter
    /// </summary>
    [BurstCompile]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public static class LogFormatterJson
    {
        private static FormatterStruct s_Formatter = default;

        /// <summary>
        /// FormatterStruct that can format JSON
        /// </summary>
        public static FormatterStruct Formatter
        {
            get
            {
                if (s_Formatter.IsCreated == false)
                {
                    s_Formatter = new FormatterStruct
                    {
                        OnFormatMessage = new OnLogMessageFormatterDelegate(OnLogMessageFormatterFunc),
                        UseText = false
                    };
                }

                return s_Formatter;
            }
        }

        /// <summary>
        /// Parses the LogMessage to Json UnsafeText
        /// </summary>
        /// <param name="logEvent">LogMessage to parse</param>
        /// <param name="formatter">Formatter that sink is using</param>
        /// <param name="outTemplate">Unused</param>
        /// <param name="messageBuffer">Memory to store the message</param>
        /// <param name="memoryManager">LogMemoryManager to get data from</param>
        /// <param name="userData">Unused</param>
        /// <param name="allocator">Allocator to allocate some temp data inside of this parser</param>
        /// <returns>Length of the messageBuffer. Negative on error</returns>
        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(OnLogMessageFormatterDelegate.Delegate))]
        public static int OnLogMessageFormatterFunc(in LogMessage logEvent, ref FormatterStruct formatter, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator)
        {
            var errorMessage = default(FixedString512Bytes);

            ref var memAllocator = ref LogMemoryManager.FromPointer(memoryManager);

            var length = ParseContextMessageToJson(in logEvent, ref formatter, ref messageBuffer, ref errorMessage, ref memAllocator, allocator);
            if (length < 0)
            {
                SelfLog.OnFailedToParseMessage();
            }

            return length;
        }

        private static int ParseContextMessageToJson(in LogMessage messageData, ref FormatterStruct formatter, ref UnsafeText jsonOutput, ref FixedString512Bytes errorMessage, ref LogMemoryManager memAllocator, Allocator allocator)
        {
            jsonOutput.Length = 0;

            var headerData = HeaderData.Parse(in messageData, ref memAllocator);

            if (headerData.Error != ErrorCodes.NoError)
            {
                SelfLog.Error(Errors.FromEnum(headerData.Error));
                return (int)headerData.Error;
            }

            var success = WriteJsonMessage(in messageData, ref formatter, ref headerData, ref jsonOutput, ref errorMessage, ref memAllocator, allocator);

            // Regardless of current success value, if an error was raised return "failed"
            // NOTE: Internally we may return  "success" even when some errors are raised so as to continue parsing the message.
            if (success == false || !errorMessage.IsEmpty)
                return -10;

            return jsonOutput.Length;
        }

        private static unsafe bool WriteJsonMessage(in LogMessage messageData, ref FormatterStruct formatter, ref HeaderData headerData, ref UnsafeText messageOutput, ref FixedString512Bytes errorMessage, ref LogMemoryManager memAllocator, Allocator allocator)
        {
            var stackTraceIdData = messageData.StackTraceId;
            var timestamp = messageData.Timestamp;
            var logLevel = messageData.Level;

            // Estimate initial size of the output buffer; UnsafeListString will resize by re-allocating memory (like to avoid), so try to get a ballpark size guess.
            var tempBuffer = new UnsafeText(headerData.MessageBufferLength * 2, allocator);

            {
                messageOutput.Append((FixedString32Bytes)"{\"Timestamp\":\"");
                tempBuffer.Clear();
                LogWriterUtils.WriteFormattedTimestamp(timestamp, ref tempBuffer);
                JsonWriter.AppendEscapedJsonString(ref messageOutput, tempBuffer.GetUnsafePtr(), tempBuffer.Length);
            }
            {
                messageOutput.Append((FixedString32Bytes)"\",\"Level\":\"");
                tempBuffer.Clear();
                LogWriterUtils.WriteFormattedLevel(logLevel, ref tempBuffer);
                JsonWriter.AppendEscapedJsonString(ref messageOutput, tempBuffer.GetUnsafePtr(), tempBuffer.Length);
            }

            var messageBufferPointer = headerData.MessageBufferPointer;
            var messageBufferLength = headerData.MessageBufferLength;

            messageOutput.Append((FixedString32Bytes)"\",\"Message\":\"");
            JsonWriter.AppendEscapedJsonString(ref messageOutput, messageBufferPointer, messageBufferLength);

            if (stackTraceIdData != 0)
            {
                tempBuffer.Clear();
                ManagedStackTraceWrapper.AppendToUnsafeText(stackTraceIdData, ref tempBuffer);
                messageOutput.Append((FixedString32Bytes)"\",\"Stacktrace\":\"");

                JsonWriter.AppendEscapedJsonString(ref messageOutput, tempBuffer.GetUnsafePtr(), tempBuffer.Length);
            }

            messageOutput.Append((FixedString32Bytes)"\",\"Properties\":{");

            var currMsgSegment = new ParseSegment();

            var argIndexInString = -1;
            var done = false;
            var success = true;
            var hashName = new IntHashSet(128, allocator);
            var firstProperty = true;

            if (headerData.ContextBufferCount > 0)
            {
                do
                {
                    var result = MessageParser.FindNextParseStringSegment(messageBufferPointer, messageBufferLength, ref currMsgSegment, out var currArgSlot);

                    switch (result)
                    {
                        case MessageParser.ParseContextResult.NormalArg:
                        {
                            var rawBuffer = messageBufferPointer;
                            var arg = ArgumentInfo.ParseArgument(rawBuffer, currArgSlot.OffsetEnd, currArgSlot);

                            if (arg.Type == ArgumentInfo.HoleType.UserDefined || arg.Type == ArgumentInfo.HoleType.BuiltinMessage) // message is user defined in this context
                            {
                                if (arg.IsValid)
                                {
                                    ++argIndexInString;

                                    var contextIndex = arg.Index;

                                    var jsonName = arg.Name;
                                    if (jsonName.IsEmpty)
                                    {
                                        jsonName.Append((FixedString32Bytes)"arg");
                                        jsonName.Append(arg.Index);
                                    }
                                    else
                                    {
                                        contextIndex = argIndexInString;
                                    }

                                    if (hashName.Add(jsonName.GetHashCode()))
                                    {
                                        if (firstProperty == false)
                                            success = formatter.AppendDelimiter(ref messageOutput) && success;
                                        else
                                            firstProperty = false;

                                        if (headerData.TryGetContextPayload(contextIndex, out var contextPayload))
                                        {
                                            success = formatter.BeginProperty(ref messageOutput, ref jsonName) && success;
                                            success = LogWriterUtils.WriteFormattedContextData(ref formatter, in contextPayload, ref messageOutput, ref errorMessage, ref memAllocator, ref arg) && success;
                                            success = formatter.EndProperty(ref messageOutput, ref jsonName) && success;
                                        }
                                        else
                                        {
                                            errorMessage = Errors.UnableToRetrieveValidContextArgumentIndex;
                                            SelfLog.Error(errorMessage);

                                            success = false;
                                        }
                                    }
                                }
                            }

                            break;
                        }
                        case MessageParser.ParseContextResult.NoArgs:
                            done = true;

                            break;
                    }

                    currMsgSegment.Offset = currArgSlot.Offset + currArgSlot.Length;
                    if (currMsgSegment.Offset >= messageBufferLength)
                        done = true;
                }
                while (!done);
            }

            var tempNameBuffer = new FixedString512Bytes();

            var decorN = headerData.DecorationPairs;
            var argDefault = default(ArgumentInfo);
            for (var i = 0; i < decorN; ++i)
            {
                if (headerData.TryGetDecorationPayload(i, ref memAllocator, out var namePayload, out var dataPayload) == false)
                {
                    errorMessage = Errors.UnableToRetrieveDecoratorsInfo;
                    SelfLog.Error(errorMessage);

                    return false;
                }

                var namePayloadBuffer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(namePayload);
                var namePayloadBufferLength = namePayload.Length;

                if (namePayloadBufferLength == UnsafePayloadRingBuffer.MinimumPayloadSize)
                {
                    // name was short, so there could be null terminated symbols less than MinimumPayloadSize that is usually 4
                    for (uint k = 0; k < UnsafePayloadRingBuffer.MinimumPayloadSize; ++k)
                    {
                        if (namePayloadBuffer[k] == 0)
                        {
                            namePayloadBufferLength = (int)k;
                            break;
                        }
                    }
                }

                tempNameBuffer.Clear();
                tempNameBuffer.Append(namePayloadBuffer, namePayloadBufferLength);
                if (hashName.Add(tempNameBuffer.GetHashCode()))
                {
                    if (firstProperty == false)
                        success = formatter.AppendDelimiter(ref messageOutput) && success;
                    else
                        firstProperty = false;

                    success = formatter.BeginProperty(ref messageOutput, ref tempNameBuffer) && success;
                    success = LogWriterUtils.WriteFormattedContextData(ref formatter, dataPayload, ref messageOutput, ref errorMessage, ref memAllocator, ref argDefault) && success;
                    success = formatter.EndProperty(ref messageOutput, ref tempNameBuffer) && success;
                }
            }

            hashName.Dispose();

            messageOutput.Append((FixedString32Bytes)"}}");

            return success;
        }
    }
}
