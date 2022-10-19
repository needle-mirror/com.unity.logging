using System;
using Unity.Collections;

namespace Unity.Logging.Internal.Debug
{
    /// <summary>
    /// Static class with Error messages
    /// </summary>
    internal static class Errors
    {
        public static FixedString512Bytes FromEnum(ErrorCodes code)
        {
            return code switch
            {
                ErrorCodes.NoError => "",

                ErrorCodes.CorruptedDecorationInfo => CorruptedDecorationInfo,
                ErrorCodes.FailedToLockPayloadBuffer => FailedToLockPayloadBuffer,
                ErrorCodes.UnableToRetrieveTimestampAndLevel => UnableToRetrieveTimestampAndLevel,
                ErrorCodes.UnableToRetrieveDecoratorsInfo => UnableToRetrieveDecoratorsInfo,
                ErrorCodes.UnableToRetrieveSimpleMessageBuffer => UnableToRetrieveSimpleMessageBuffer,
                ErrorCodes.UnableToRetrieveDisjointedMessageBuffer => UnableToRetrieveDisjointedMessageBuffer,
                ErrorCodes.UnableToRetrieveMessageFromContextBuffer => UnableToRetrieveMessageFromContextBuffer,
                ErrorCodes.UnableToRetrieveContextArgument => UnableToRetrieveContextArgument,
                ErrorCodes.UnableToRetrieveValidContextArgumentIndex => UnableToRetrieveValidContextArgumentIndex,
                ErrorCodes.UnableToRetrieveContextDataFromLogMessageBuffer => UnableToRetrieveContextDataFromLogMessageBuffer,
                ErrorCodes.UnknownTypeId => UnknownTypeId,
                ErrorCodes.FailedToCreateDisjointedBuffer => FailedToCreateDisjointedBuffer,
                ErrorCodes.FailedToParseMessage => FailedToParseMessage,
                ErrorCodes.FailedToAllocatePayloadBecauseOfItsSize => FailedToAllocatePayloadBecauseOfItsSize,
                ErrorCodes.UnableToRetrieveStackTrace => UnableToRetrieveStackTrace,
                ErrorCodes.UnableToRetrieveValidPayloadsFromDisjointedMessageBuffer => UnableToRetrieveValidPayloadsFromDisjointedMessageBuffer,
                _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
            };
        }
        public static FixedString512Bytes CorruptedDecorationInfo => "Error: Corrupted decoration info";
        public static FixedString512Bytes FailedToLockPayloadBuffer => "Error: Failed to lock LogMessage buffer";
        public static FixedString512Bytes UnableToRetrieveTimestampAndLevel => "Error: Failed to retrieve timestamp and level buffer";
        public static FixedString512Bytes UnableToRetrieveStackTrace => "Error: Failed to retrieve a stacktrace";
        public static FixedString512Bytes UnableToRetrieveDecoratorsInfo => "Error: Failed to retrieve decoration info";
        public static FixedString512Bytes UnableToRetrieveSimpleMessageBuffer => "Error: Unable to retrieve simple LogMessage buffer";
        public static FixedString512Bytes UnableToRetrieveDisjointedMessageBuffer => "Error: Unable to retrieve disjointed LogMessage buffer";
        public static FixedString512Bytes UnableToRetrieveValidPayloadsFromDisjointedMessageBuffer => "Error: Unable to retrieve valid disjointed LogMessage buffer";
        public static FixedString512Bytes UnableToRetrieveMessageFromContextBuffer => "Error: Unable to retrieve message string from a Context LogMessage buffer";
        public static FixedString512Bytes UnableToRetrieveContextArgument => "Error: Unable to read context argument and/or match argument to a context buffer";
        public static FixedString512Bytes UnableToRetrieveValidContextArgumentIndex => "Error: Invalid context argument index";
        public static FixedString512Bytes UnableToRetrieveContextDataFromLogMessageBuffer => "Error: Unable to retrieve Context data from LogMessage buffer";
        public static FixedString512Bytes UnknownTypeId => "Error: Unknown Type for OutputHandlers. TypeId: ";
        public static FixedString512Bytes UnknownTypeIdBecauseOfEmptyHandlers => "Error: List of OutputHandler was empty, so TypeId was not parsed. TypeId: ";
        public static FixedString512Bytes FailedToCreateDisjointedBuffer => "Error: Failed to create disjointed buffer";
        public static FixedString512Bytes FailedToParseMessage => "Error: Failed to parse the message";
        public static FixedString512Bytes FailedToAllocatePayloadBecauseOfItsSize => "Error: Failed to allocate a payload because of its size = ";
        public static FixedString512Bytes EmptyTemplateForTextLogger => "Error: Template is empty! Nothing will be logged";
    }

    /// <summary>
    /// Error codes for logging
    /// </summary>
    public enum ErrorCodes
    {
        /// <summary>
        /// No error detected
        /// </summary>
        NoError = 0,
        /// <summary>
        /// Header of the message that contains decoration is corrupted
        /// </summary>
        CorruptedDecorationInfo = -1,
        /// <summary>
        /// Failed to lock payload buffer in the memory manager
        /// </summary>
        FailedToLockPayloadBuffer = -2,
        /// <summary>
        /// Message is corrupted - cannot retrieve timestamp and level information
        /// </summary>
        UnableToRetrieveTimestampAndLevel = -3,
        /// <summary>
        /// Message is corrupted - cannot retrieve stacktrace information
        /// </summary>
        UnableToRetrieveStackTrace = -4,
        /// <summary>
        /// Message is corrupted - cannot retrieve decorators information
        /// </summary>
        UnableToRetrieveDecoratorsInfo = -5,
        /// <summary>
        /// Message is corrupted - cannot retrieve message buffer
        /// </summary>
        UnableToRetrieveSimpleMessageBuffer = -6,
        /// <summary>
        /// Message is corrupted - cannot retrieve disjointed message buffer
        /// </summary>
        UnableToRetrieveDisjointedMessageBuffer = -7,
        /// <summary>
        /// Message is corrupted - cannot retrieve payloads from disjointed message buffer
        /// </summary>
        UnableToRetrieveValidPayloadsFromDisjointedMessageBuffer = -8,
        /// <summary>
        /// Message is corrupted - cannot retrieve message from the context buffer
        /// </summary>
        UnableToRetrieveMessageFromContextBuffer = -9,
        /// <summary>
        /// Message is corrupted - cannot retrieve argument
        /// </summary>
        UnableToRetrieveContextArgument = -10,
        /// <summary>
        /// Message is corrupted - cannot retrieve valid argument index
        /// </summary>
        UnableToRetrieveValidContextArgumentIndex = -11,
        /// <summary>
        /// Message is corrupted - cannot retrieve context data from log message buffer
        /// </summary>
        UnableToRetrieveContextDataFromLogMessageBuffer = -12,
        /// <summary>
        /// Type is unknown
        /// </summary>
        UnknownTypeId = -13,
        /// <summary>
        /// Failed to allocate/create disjointed buffer
        /// </summary>
        FailedToCreateDisjointedBuffer = -14,
        /// <summary>
        /// Failed to parse the message
        /// </summary>
        FailedToParseMessage = -15,
        /// <summary>
        /// Failed to allocate a payload because of its size
        /// </summary>
        FailedToAllocatePayloadBecauseOfItsSize = -16,
    }
}
