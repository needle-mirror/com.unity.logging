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
        public readonly ErrorCodes Error;

        public readonly byte* MessageBufferPointer;
        public readonly int MessageBufferLength;

        public readonly PayloadHandle* Payloads;
        public readonly int PayloadsCount;
        public readonly int ContextBufferCount;
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

        public ushort DecorationPairs => (ushort)(DecorationBufferCount / 2);

        public int ContextStartIndex => DecorationStartIndex + DecorationBufferCount;

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
