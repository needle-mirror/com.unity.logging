using Unity.Collections;

namespace Unity.Logging.Internal.Debug
{
    /// <summary>
    /// Static class with Error messages
    /// </summary>
    internal static class Errors
    {
        public static FixedString512Bytes CorruptedDecorationInfo => "Error: Corrupted decoration info";
        public static FixedString512Bytes FailedToLockPayloadBuffer => "Error: Failed to lock LogMessage buffer";
        public static FixedString512Bytes UnableToRetrieveTimestampAndLevel => "Error: Failed to retrieve timestamp and level buffer";
        public static FixedString512Bytes UnableToRetrieveStackTrace => "Error: Failed to retrieve a stacktrace";
        public static FixedString512Bytes UnableToRetrieveDecoratorsInfo => "Error: Failed to retrieve decoration info";
        public static FixedString512Bytes UnableToRetrieveSimpleMessageBuffer => "Error: Unable to retrieve simple LogMessage buffer";
        public static FixedString512Bytes UnableToRetrieveDisjointedMessageBuffer => "Error: Unable to retrieve disjointed LogMessage buffer";
        public static FixedString512Bytes UnableToRetrieveMessageFromContextBuffer => "Error: Unable to retrieve message string from a Context LogMessage buffer";
        public static FixedString512Bytes UnableToRetrieveContextArgument => "Error: Unable to read context argument and/or match argument to a context buffer";
        public static FixedString512Bytes UnableToRetrieveValidContextArgumentIndex => "Error: Invalid context argument index";
        public static FixedString512Bytes UnableToRetrieveContextDataFromLogMessageBuffer => "Error: Unable to retrieve Context data from LogMessage buffer";
        public static FixedString512Bytes UnknownTypeId => "Error: Unknown Context struct TypeId: ";
        public static FixedString512Bytes FailedToCreateDisjointedBuffer => "Error: Failed to create disjointed buffer";
        public static FixedString512Bytes FailedToParseMessage => "Error: Failed to parse the message";
        public static FixedString512Bytes FailedToAllocatePayloadBecauseOfItsSize => "Error: Failed to allocate a payload because of its size = ";
    }
}
