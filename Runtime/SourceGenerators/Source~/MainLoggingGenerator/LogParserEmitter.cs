using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using LoggingCommon;
using Microsoft.CodeAnalysis;
using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    internal static class LogParserEmitter
    {
        public static StringBuilder Emit(in ContextWrapper context, in LogStructureTypesData structData, ulong assemblyHash)
        {
            using var _ = new Profiler.Auto("LogParserEmitter.Emit");

            var sb = new StringBuilder();
            var uniquePostfix = Common.CreateMD5String($"LogParserEmitter{assemblyHash}");

            sb.Append($@"{EmitStrings.SourceFileHeader}
{EmitStrings.SourceFileHeaderIncludes}

namespace Unity.Logging
{{
    {EmitStrings.BurstCompileAttr}
    internal struct TextLoggerParserOutputHandlers{assemblyHash:X4}
    {{
        public static IntPtr HandlerToken;

        internal static void RegisterTextLoggerParserOutputHandlers()
        {{
            HandlerToken = LogWriterUtils.AddOutputHandler(WriteContextFormattedOutput{uniquePostfix}, true);
        }}

        {EmitStrings.BurstCompileAttr}
        [AOT.MonoPInvokeCallback(typeof(Unity.Logging.LogWriterUtils.OutputWriterHandler))]
        static LogWriterUtils.ContextWriteResult WriteContextFormattedOutput{uniquePostfix}(ref FormatterStruct formatter, ref UnsafeText hstring, ref BinaryParser mem, IntPtr memAllocatorPtr, ref ArgumentInfo currArgSlot)
        {{
            var length = mem.LengthInBytes;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (hstring.IsCreated == false || mem.IsValid == false || length < UnsafeUtility.SizeOf<ulong>())
            {{
                return LogWriterUtils.ContextWriteResult.Failed;
            }}
#endif
            Unity.Logging.Internal.LoggerManager.DebugBreakpointPlaceForOutputWriterHandlers();

            ref LogMemoryManager memAllocator = ref LogMemoryManager.FromPointer(memAllocatorPtr);

            bool success = false;
            int typeLength = 0;

            var headerSize = UnsafeUtility.SizeOf<ulong>();
            var header = mem.Peek<ulong>();

            // Each generated struct holds a 'TypeId' as its first field identifying the struct's type
            switch (header)
            {{
                {EmitTextLoggerStructureFormatWriterCases(structData.StructTypes)}

                default:
                    return LogWriterUtils.ContextWriteResult.UnknownType;
            }}

            return success ? LogWriterUtils.ContextWriteResult.Success : LogWriterUtils.ContextWriteResult.Failed;
        }}
    }}
}}

{EmitStrings.SourceFileFooter}
");

            return sb;
        }

        private static StringBuilder EmitTextLoggerStructureFormatWriterCases(List<LogStructureDefinitionData> types)
        {
            var sb = new StringBuilder();

            var defaultSpecialTypes = new[]
            {
                (1, "int"),
                (2, "uint"),
                (3, "ulong"),
                (4, "long"),
                (5, "char"),
                (6, "float"),
                (7, "double"),
                (8, "bool"),

                (10, "short"),
                (11, "ushort"),
                (12, "sbyte"),
                (13, "byte"),

                (14, "IntPtr"),
                (15, "UIntPtr"),
            };

            foreach (var (typeId, typeName) in defaultSpecialTypes)
            {
                sb.Append($@"
                case {typeId}:
                    typeLength = UnsafeUtility.SizeOf<{typeName}>() + headerSize;
                    success = length >= typeLength && formatter.WriteProperty(ref hstring, """", mem.Skip(headerSize).Peek<{typeName}>(), ref currArgSlot);
                    break;
");
            }

            sb.Append($@"
                case 110:
                case 200:
                    mem = mem.Skip(headerSize);
                    int dataLength = mem.Peek<int>();
                    success = mem.Skip<int>().AppendUTF8StringToUnsafeText(ref hstring, ref formatter, dataLength, ref currArgSlot);
                    break;
");

            foreach (var st in types)
            {
                if (st.IsUserType)
                    sb.Append(@"
                // user type");

                sb.Append($@"
                case {st.TypeId}:
                    typeLength = UnsafeUtility.SizeOf<{st.FullGeneratedTypeName}>();
                    success = length >= typeLength && mem.AppendToUnsafeText<{st.FullGeneratedTypeName}>(ref hstring, ref formatter, ref memAllocator, ref currArgSlot);
                    break;
");
            }

            return sb;
        }
    }
}
