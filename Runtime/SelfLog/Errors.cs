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

    public enum ErrorCodes
    {
        NoError = 0,
        CorruptedDecorationInfo = -1,
        FailedToLockPayloadBuffer = -2,
        UnableToRetrieveTimestampAndLevel = -3,
        UnableToRetrieveStackTrace = -4,
        UnableToRetrieveDecoratorsInfo = -5,
        UnableToRetrieveSimpleMessageBuffer = -6,
        UnableToRetrieveDisjointedMessageBuffer = -7,
        UnableToRetrieveValidPayloadsFromDisjointedMessageBuffer = -8,
        UnableToRetrieveMessageFromContextBuffer = -9,
        UnableToRetrieveContextArgument = -10,
        UnableToRetrieveValidContextArgumentIndex = -11,
        UnableToRetrieveContextDataFromLogMessageBuffer = -12,
        UnknownTypeId = -13,
        FailedToCreateDisjointedBuffer = -14,
        FailedToParseMessage = -15,
        FailedToAllocatePayloadBecauseOfItsSize = -16,
    }
}
