using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IL2CPP.CompilerServices;
using Unity.Logging.Internal.Debug;

namespace Unity.Logging
{
    /// <summary>
    /// Decodes payload for LogMessages
    /// </summary>
    [BurstCompile]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public unsafe readonly struct HeaderData
    {
        /// <summary>
        /// Error code that was detected during the parsing of the binary data
        /// </summary>
        public readonly ErrorCodes Error;

        /// <summary>
        /// Pointer to message buffer UTF8 string
        /// </summary>
        public readonly byte* MessageBufferPointer;
        /// <summary>
        /// Message buffer length in bytes
        /// </summary>
        public readonly int MessageBufferLength;

        /// <summary>
        /// Payload handles attached to the LogMessage
        /// </summary>
        public readonly PayloadHandle* Payloads;
        /// <summary>
        /// Count of payload handles attached to the LogMessage
        /// </summary>
        public readonly int PayloadsCount;
        /// <summary>
        /// Payload handles that are context buffers
        /// </summary>
        public readonly int ContextBufferCount;

        /// <summary>
        /// Payload handles that are decoration buffers
        /// </summary>
        public readonly ushort DecorationBufferCount;

        const int DecorationStartIndex = 2;

        private HeaderData(PayloadHandle* payloads, int payloadsCount, ushort totalDecorationCount, NativeArray<byte> messageBuffer)
        {
            Payloads = payloads;
            PayloadsCount = payloadsCount;

            DecorationBufferCount = totalDecorationCount;

            // Context buffers should always occupy the last set of payloads.
            ContextBufferCount = payloadsCount - (DecorationStartIndex + totalDecorationCount);

            MessageBufferPointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(messageBuffer);
            MessageBufferLength = messageBuffer.Length;

            Error = ErrorCodes.NoError;
        }

        private HeaderData(ErrorCodes errorState)
        {
            Payloads = default;
            PayloadsCount = 0;
            DecorationBufferCount = 0;
            ContextBufferCount = 0;
            MessageBufferLength = 0;
            MessageBufferPointer = null;

            Error = errorState;
        }

        /// <summary>
        /// Number of payload handles that are decoration pairs
        /// </summary>
        public ushort DecorationPairs => (ushort)(DecorationBufferCount / 2);

        /// <summary>
        /// Index of payload handle that is context buffer
        /// </summary>
        public int ContextStartIndex => DecorationStartIndex + DecorationBufferCount;

        /// <summary>
        /// Parses Header of the binary data
        /// </summary>
        /// <param name="messageData">LogMessage that owns the binary data</param>
        /// <param name="memAllocator">Memory manager that has the binary data</param>
        /// <returns>Parsed HeaderData</returns>
        public static HeaderData Parse(in LogMessage messageData, ref LogMemoryManager memAllocator)
        {
            // Payload handle is expected to reference a DisjointedBuffer.
            // The "head" buffer's length from a DisjointedBuffer allocation must be a multiple of PayloadHandle size, If not something is very wrong.
            if (!memAllocator.RetrievePayloadBuffer(messageData.Payload, out var headBuffer) || headBuffer.Length % UnsafeUtility.SizeOf<PayloadHandle>() != 0)
            {
                return FailedWithError(ErrorCodes.UnableToRetrieveDisjointedMessageBuffer);
            }

            var payloads = (PayloadHandle*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(headBuffer);
            var payloadsCount = headBuffer.Length / UnsafeUtility.SizeOf<PayloadHandle>();

            if (payloadsCount <= 1)
            {
                return FailedWithError(ErrorCodes.UnableToRetrieveValidPayloadsFromDisjointedMessageBuffer);
            }

            if (!memAllocator.RetrievePayloadBuffer(payloads[0], out var messageBuffer))
            {
                return FailedWithError(ErrorCodes.UnableToRetrieveMessageFromContextBuffer);
            }

            if (!memAllocator.RetrievePayloadBuffer(payloads[1], out var decorationInfo))
            {
                return FailedWithError(ErrorCodes.UnableToRetrieveDecoratorsInfo);
            }

            // 0 handle is message
            // 1 handle is decoration header (contains info about localConstHandlesCount, globalConstHandlesCount and total decorator count, see BuildDecorators
            // [total decorator count] payloads
            // till the end - context buffers

            // decoration info
            var decorationIsCorrect = ExtractDecorationInfo(decorationInfo, payloadsCount, out var totalDecorationCount);
            if (decorationIsCorrect == false)
            {
                return FailedWithError(ErrorCodes.CorruptedDecorationInfo);
            }

            return new HeaderData(payloads, payloadsCount, totalDecorationCount, messageBuffer);
        }

        private static bool ExtractDecorationInfo(NativeArray<byte> decorationInfo, int totalPayloadCount, out ushort totalDecorCount)
        {
            totalDecorCount = 0;

            var decorationSizeInBytes = decorationInfo.Length;

            if (decorationSizeInBytes != sizeof(ushort) * 3)
                return false;

            var payloadLocalCountPtr = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(decorationInfo);
            var localConstPayloadsCount = *payloadLocalCountPtr++;
            var globalConstPayloadsCount = *payloadLocalCountPtr++;
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

        private static HeaderData FailedWithError(ErrorCodes errCode) => new HeaderData(errCode);

        /// <summary>
        /// Try to get the <see cref="PayloadHandle"/> for the context payload by index
        /// </summary>
        /// <param name="contextIndex">Index of the context payload</param>
        /// <param name="payloadHandle">Resulting PayloadHandle, or default if not found</param>
        /// <returns>True if the context payload was found</returns>
        public bool TryGetContextPayload(int contextIndex, out PayloadHandle payloadHandle)
        {
            if (contextIndex < 0 || contextIndex >= ContextBufferCount)
            {
                payloadHandle = default;
                return false;
            }

            payloadHandle = Payloads[ContextStartIndex + contextIndex];
            return true;
        }

        /// <summary>
        /// Try to get the <see cref="PayloadHandle"/> for the decoration payload by the pair index
        /// </summary>
        /// <param name="decorationPairIndex">Pair index</param>
        /// <param name="memAllocator">Memory manager that has the binary data</param>
        /// <param name="nameArray">Result - name of the decoration</param>
        /// <param name="dataHandle">Result - data of the decoration</param>
        /// <returns>True if the decoration payload was found</returns>
        public bool TryGetDecorationPayload(int decorationPairIndex, ref LogMemoryManager memAllocator, out NativeArray<byte> nameArray, out PayloadHandle dataHandle)
        {
            var indx = decorationPairIndex * 2;

            if (memAllocator.RetrievePayloadBuffer(Payloads[DecorationStartIndex + indx], out nameArray) == false)
            {
                dataHandle = default;
                return false;
            }

            dataHandle = Payloads[DecorationStartIndex + indx + 1];
            return true;
        }
    }
}
