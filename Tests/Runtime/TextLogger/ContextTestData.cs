using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;

namespace Unity.Logging.Tests
{
    public struct MessageData
    {
        public string               Message;
        public List<IContextStruct> Contexts;

        public static MessageData Create(string message, params IContextStruct[] contexts)
        {
            return new MessageData
            {
                Message = message,
                Contexts = new List<IContextStruct>(contexts),
            };
        }
    }


    public interface IContextStruct
    {
        PayloadHandle BuildContext(ref LogMemoryManager memAllocator);
        string GetFormattedFields();
    }

    public struct PrimitiveContextDataFixedString32 : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((FixedString32Bytes)"Some string", ref memAllocator);
        }

        public string GetFormattedFields() => "Some string";
    }

    public struct PrimitiveContextDataFixedString64 : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((FixedString64Bytes)"Some stringSome stringSome stringSome stringSome stringSome", ref memAllocator);
        }

        public string GetFormattedFields() => "Some stringSome stringSome stringSome stringSome stringSome";
    }

    public struct PrimitiveContextDataFixedString128 : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((FixedString128Bytes)"Fixed String 128", ref memAllocator);
        }

        public string GetFormattedFields() => "Fixed String 128";
    }

    public struct PrimitiveContextDataFixedString512 : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((FixedString512Bytes)"Fixed String 512", ref memAllocator);
        }

        public string GetFormattedFields() => "Fixed String 512";
    }

    public struct PrimitiveContextDataFixedString4096 : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((FixedString4096Bytes)"Fixed String 4096", ref memAllocator);
        }

        public string GetFormattedFields() => "Fixed String 4096";
    }

    public struct PrimitiveContextDataSbyte : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((sbyte)-120, ref memAllocator);
        }

        public string GetFormattedFields() => "-120";
    }

    public struct PrimitiveContextDataByte : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((byte)255, ref memAllocator);
        }

        public string GetFormattedFields() => "255";
    }

    public struct PrimitiveContextDataShort : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((short)-32760, ref memAllocator);
        }

        public string GetFormattedFields() => "-32760";
    }

    public struct PrimitiveContextDataUShort : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((ushort)65530, ref memAllocator);
        }

        public string GetFormattedFields() => "65530";
    }

    public struct PrimitiveContextDataInt : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((int)-214748360, ref memAllocator);
        }

        public string GetFormattedFields() => "-214748360";
    }

    public struct PrimitiveContextDataUInt : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((uint)4294967290, ref memAllocator);
        }

        public string GetFormattedFields() => "4294967290";
    }

    public struct PrimitiveContextDataLong : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((long)-12313123123, ref memAllocator);
        }

        public string GetFormattedFields() => "-12313123123";
    }

    public struct PrimitiveContextDataULong : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((ulong)18446744073709551610, ref memAllocator);
        }

        public string GetFormattedFields() => "18446744073709551610";
    }

    public struct PrimitiveContextDataChar : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType('*', ref memAllocator);
        }

        public string GetFormattedFields() => "*";
    }

    public struct PrimitiveContextDataFloat : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((float)42.42, ref memAllocator);
        }

        public string GetFormattedFields() => "42.42";
    }

    public struct PrimitiveContextDataDouble : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType((double)42.4242, ref memAllocator);
        }

        public string GetFormattedFields() => "42.4242";
    }

    public struct PrimitiveContextDataBool : IContextStruct
    {
        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContextSpecialType(true, ref memAllocator);
        }

        public string GetFormattedFields() => "true";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BasicContextData1 : IContextStruct
    {
        public const uint ContextTypeId = TextParserWrapper.TextContextBaseId + 1;
        public uint TypeId;

        public int   IntField1;
        public float FloatField;
        public bool  BoolField;
        public int   IntField2;

        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContext(this, ref memAllocator);
        }

        public string GetFormattedFields()
        {
            return $"[{IntField1}, {FloatField}, {BoolField}, {IntField2}]";
        }

        public static BasicContextData1 Create(int i1, float f, bool b, int i2)
        {
            return new BasicContextData1
            {
                TypeId = ContextTypeId,

                IntField1 = i1,
                FloatField = f,
                BoolField = b,
                IntField2 = i2,
            };
        }

        public static BasicContextData1 Create(uint seed = 100)
        {
            var rand = new Mathematics.Random(seed);
            return Create(rand.NextInt(), rand.NextFloat(), rand.NextBool(), rand.NextInt());
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BasicContextData2 : IContextStruct
    {
        public const uint ContextTypeId = TextParserWrapper.TextContextBaseId + 2;
        public uint TypeId;

        public byte   ByteField1;
        public short  ShortField;
        public double DoubleField;
        public long   LongField;
        public byte   ByteField2;

        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContext(this, ref memAllocator);
        }

        public string GetFormattedFields()
        {
            return $"[{ByteField1}, {ShortField}, {DoubleField}, {LongField}, {ByteField2}]";
        }

        public static BasicContextData2 Create(byte b1, short s, double d, long l, byte b2)
        {
            return new BasicContextData2
            {
                TypeId = ContextTypeId,

                ByteField1 = b1,
                ShortField = s,
                DoubleField = d,
                LongField = l,
                ByteField2 = b2,
            };
        }

        public static BasicContextData2 Create(uint seed = 100)
        {
            var rand = new Mathematics.Random(seed);
            return BasicContextData2.Create(
                (byte)rand.NextInt(byte.MinValue, byte.MaxValue),
                (short)rand.NextInt(short.MinValue, short.MaxValue),
                rand.NextDouble(),
                (long)rand.NextDouble(long.MinValue, long.MaxValue),
                (byte)rand.NextInt(byte.MinValue, byte.MaxValue));
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BasicContextData3 : IContextStruct
    {
        public const uint ContextTypeId = TextParserWrapper.TextContextBaseId + 3;
        public uint TypeId;

        public ulong ULongField;
        public char CharField;
        public ushort UShortField;
        public bool BoolField;

        public PayloadHandle BuildContext(ref LogMemoryManager memAllocator)
        {
            return Builder.BuildContext(this, ref memAllocator);
        }

        public string GetFormattedFields()
        {
            return $"[{ULongField}, {CharField}, {UShortField}, {BoolField}]";
        }

        public static BasicContextData3 Create(ulong l, char c, ushort us, bool b)
        {
            return new BasicContextData3
            {
                TypeId = ContextTypeId,

                ULongField = l,
                CharField = c,
                UShortField = us,
                BoolField = b,
            };
        }

        public static BasicContextData3 Create(uint seed = 100)
        {
            var rand = new Mathematics.Random(seed);
            return BasicContextData3.Create(
                (ulong)rand.NextDouble(ulong.MinValue, ulong.MaxValue),
                (char)rand.NextInt(char.MinValue, char.MaxValue),
                (ushort)rand.NextInt(ushort.MinValue, ushort.MaxValue),
                rand.NextBool());
        }
    }


    /// <summary>
    /// Integrates with the TextParser to test the parsing logic independently of the rest of TextLogger.
    /// </summary>
    /// <remarks>
    /// Unlike end-to-end logging tests, the TextParser unit test don't utilize sourcegen at all. Instead, the logic
    /// normally provided by sourcegen is replicated by these methods i.e. build a TextLogger message and output a formatted text string.
    /// Note that the Builder and Formatter pieces aren't covered by these tests; only the actual parsing logic is tested here.
    /// </remarks>
    public unsafe struct TextParserWrapper
    {
        public const uint TextContextBaseId = 0xFFFFF001;

        private static IntPtr WriteHandlerToken;
        private static LogControllerScopedLock s_Lock;

        /// <summary>
        /// Initializes the test hooks (Burst function pointers) to integrate with TextParser.
        /// </summary>
        /// <remarks>
        /// The "normal" code paths used by sourcegen relay on partial structs/methods, which we can't use here because the Test code runs from a separate assembly;
        /// partial structs/methods must be defined in the same assembly.
        /// </remarks>
        public static void Initialize()
        {
            // Allocate large buffers and disable grow/shrink logic
            LogMemoryManagerParameters.GetDefaultParameters(out var memParams);
            memParams.BufferGrowThreshold = 0;
            memParams.BufferShrinkThreshold = 0;
            memParams.InitialBufferCapacity = 1024 * 100;
            memParams.OverflowBufferSize = 1024 * 50;

            Log.Logger = new LoggerConfig()
                .MinimumLevel.Verbose()
                .OutputTemplate("{Message}")
                .WriteTo.Console().CreateLogger(memParams);

            s_Lock = LogControllerScopedLock.Create(Log.Logger.Handle);

            WriteHandlerToken = TextLoggerParser.AddOutputHandler(OutputWriterHandler_NonBursted, false);
        }

        /// <summary>
        /// Clears the test hooks; resets Burst function pointers
        /// </summary>
        public static void Shutdown()
        {
            TextLoggerParser.RemoveOutputHandler(WriteHandlerToken);

            s_Lock.Dispose();
            s_Lock = default;

            Log.Logger = null;
            LoggerManager.DeleteAllLoggers();
        }


        private static ref LogController s_Controller => ref s_Lock.GetLogController();

        /// <summary>
        /// Builds and dispatches a TextLogger message using the provided data.
        /// </summary>
        /// <remarks>
        /// This method mimics the logic used in the Log.Info() method produced by sourcegen. While it must ensure the
        /// message and payload buffers are allocated the same, it doesn't have to exactly match the sourcegen code. That is
        /// we don't have to use Burst compatible code and can allocate the buffers differently, so long as the data matches.
        /// </remarks>
        public static bool WriteTextMessage(ref NativeList<PayloadHandle> handles, out string messageOutput, out string errorMessage)
        {
            PayloadHandle logPayload = new PayloadHandle();
            PayloadHandle messagePayload = new PayloadHandle();

            try
            {
                logPayload = s_Controller.MemoryManager.CreateDisjointedPayloadBufferFromExistingPayloads(ref handles);
                if (!logPayload.IsValid)
                {
                    throw new System.IO.InvalidDataException($"Failed to allocated disjointed buffer");
                }

                // Generate a LogMessage and directly pass it into the Parser; return the results
                var msgString = new UnsafeText();
                var errString = new FixedString512Bytes();
                bool success = TextLoggerParser.ParseMessage("{Message}", new LogMessage(logPayload, 0, 0, LogLevel.Info), ref msgString, ref errString, ref s_Controller.MemoryManager);

                messageOutput = msgString.ToString();
                errorMessage = errString.ToString();

                // Make sure to release the payload buffers
                s_Controller.MemoryManager.ReleasePayloadBuffer(logPayload, out PayloadReleaseResult result, true);

                return success;
            }
            catch
            {
                // If something goes wrong ensure all allocated memory is release, ie force release everything
                s_Controller.MemoryManager.ReleasePayloadBuffer(logPayload, out PayloadReleaseResult result1, true);
                s_Controller.MemoryManager.ReleasePayloadBuffer(messagePayload, out PayloadReleaseResult result2, true);
                throw;
            }
        }

        public static bool WriteTextMessage(ref MessageData data, out string messageOutput, out string errorMessage)
        {
            var messagePayload = BuildMessage(data);
            var contextPayloads = BuildContexts(data);

            var handles = ArrangePayloadBuffers(messagePayload, contextPayloads);

            return WriteTextMessage(ref handles, out messageOutput, out errorMessage);
        }

        private static PayloadHandle BuildMessage(in MessageData data)
        {
            PayloadHandle handle;
            int utf8Length = Encoding.UTF8.GetByteCount(data.Message);

            if (utf8Length < FixedString32Bytes.UTF8MaxLengthInBytes)
            {
                handle = Builder.BuildMessage((FixedString32Bytes)data.Message, ref s_Controller.MemoryManager);
            }
            else if (utf8Length < FixedString64Bytes.UTF8MaxLengthInBytes)
            {
                handle = Builder.BuildMessage((FixedString64Bytes)data.Message, ref s_Controller.MemoryManager);
            }
            else if (utf8Length < FixedString128Bytes.UTF8MaxLengthInBytes)
            {
                handle = Builder.BuildMessage((FixedString128Bytes)data.Message, ref s_Controller.MemoryManager);
            }
            else if (utf8Length < FixedString512Bytes.UTF8MaxLengthInBytes)
            {
                handle = Builder.BuildMessage((FixedString512Bytes)data.Message, ref s_Controller.MemoryManager);
            }
            else
            {
                handle = Builder.BuildMessage((FixedString4096Bytes)data.Message, ref s_Controller.MemoryManager);
            }

            if (!handle.IsValid)
            {
                throw new System.IO.InvalidDataException($"Payload handle isn't valid for message: '{data.Message}'");
            }

            return handle;
        }

        private static PayloadHandle[] BuildContexts(in MessageData data)
        {
            var handles = new FixedList4096Bytes<PayloadHandle>();

            Builder.BuildDecorators(ref s_Controller, s_Lock, ref handles);

            foreach (var item in data.Contexts)
            {
                var handle = item.BuildContext(ref s_Controller.MemoryManager);
                if (!handle.IsValid)
                {
                    throw new System.IO.InvalidDataException($"Payload handle isn't valid for context: '{item}'");
                }
                handles.Add(handle);
            }

            return handles.ToArray();
        }

        private static NativeList<PayloadHandle> ArrangePayloadBuffers(in PayloadHandle msgPayload, PayloadHandle[] contextPayloads)
        {
            var handles = new NativeList<PayloadHandle>(contextPayloads.Length + 5, Allocator.Temp);

            // IMPORTANT: The order of each payload buffer is very important and this must match the order used in the Log.Info sourcegen code:
            // - Message string is always the first payload
            // - Each context buffer is added in order of it's format "index" in the message string
            handles.Add(msgPayload);
            foreach (var item in contextPayloads)
            {
                handles.Add(item);
            }

            return handles;
        }

        [AOT.MonoPInvokeCallback(typeof(Unity.Logging.TextLoggerParser.OutputWriterHandler))]
        private static TextLoggerParser.ContextWriteResult OutputWriterHandler_NonBursted(ref UnsafeText outputHeapText, byte* dataBuffer, int bufferLength)
        {
            TextLoggerParser.ContextWriteResult result = TextLoggerParser.ContextWriteResult.UnknownType;
            var st = ContextDataFromPointer(dataBuffer, bufferLength);
            if (st != null)
            {
                var contextString = st.GetFormattedFields();

                if (String.IsNullOrEmpty(contextString))
                {
                    result = TextLoggerParser.ContextWriteResult.Failed;
                }
                else
                {
                    result = outputHeapText.Append(contextString) == FormatError.None ? TextLoggerParser.ContextWriteResult.Success : TextLoggerParser.ContextWriteResult.Failed;
                }
            }
            return result;
        }

        private static unsafe IContextStruct ContextDataFromPointer(byte* ptr, int length)
        {
            IContextStruct value = null;

            switch (*(uint*)ptr)
            {
                case BasicContextData1.ContextTypeId:

                    if (Marshal.SizeOf<BasicContextData1>() <= length)
                        value = Marshal.PtrToStructure<BasicContextData1>((IntPtr)ptr);
                    break;

                case BasicContextData2.ContextTypeId:
                    if (Marshal.SizeOf<BasicContextData1>() <= length)
                        value = Marshal.PtrToStructure<BasicContextData2>((IntPtr)ptr);
                    break;

                case BasicContextData3.ContextTypeId:
                    if (Marshal.SizeOf<BasicContextData3>() <= length)
                        value = Marshal.PtrToStructure<BasicContextData3>((IntPtr)ptr);
                    break;
            }

            return value;
        }
    }
}
