using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using LoggingCommon;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    class LogTypesEmitter
    {
        private ContextWrapper     m_Context;
        private LogStructureTypesData         m_StructData;

        private LogTypesEmitter()
        {
        }

        public static StringBuilder Emit(in ContextWrapper context, in LogStructureTypesData structData)
        {
            using var _ = new Profiler.Auto("LogTypesEmitter.Emit");

            var emitter = new LogTypesEmitter
            {
                m_Context = context,
                m_StructData = structData,
            };

            var namespaceList = structData.StructTypes.Select(x => x.ContainingNamespace).Distinct().ToImmutableArray();
            var includesHashSet = new HashSet<string>(namespaceList);
            includesHashSet.UnionWith(EmitStrings.StdIncludes);

            var sb = new StringBuilder();

            sb.Append($@"{EmitStrings.SourceFileHeader}
{EmitStrings.GenerateIncludeHeader(includesHashSet)}

{emitter.EmitTextLogStructureTypeDefinitions(namespaceList)}

{EmitStrings.SourceFileFooter}
");

            return sb;
        }

        private StringBuilder EmitTextLogStructureTypeDefinitions(ImmutableArray<string> namespaceList)
        {
            using var _ = new Profiler.Auto("LogTypesEmitter.EmitTextLogStructureTypeDefinitions");

            var sb = new StringBuilder();

            foreach (var name in namespaceList)
            {
                sb.Append($@"
namespace {name}
{{
{EmitTextLogStructureTypeDefinitionsForNamespace(name)}
}}");
            }

            return sb;
        }

        private StringBuilder EmitTextLogStructureTypeDefinitionsForNamespace(string currNamespace)
        {
            var sb = new StringBuilder();

            var structList = m_StructData.StructTypes.Where(x => x.ContainingNamespace == currNamespace && x.IsUserType == false);
            foreach (var inst in structList)
            {
                sb.Append($@"    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct {inst.GeneratedTypeName} : ILoggableMirrorStruct<{inst.FullTypeName}>
    {{
        public const ulong {inst.GeneratedTypeName}_TypeIdValue = {inst.TypeId.ToString()};

        public ulong {m_StructData.StructIdFieldName};

        {EmitFields(inst)}

        public bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth)
        {{
            bool success = formatter.BeforeObject(ref output);

            {EmitTextLogStructureWriterContent(inst)}

            success = formatter.AfterObject(ref output) && success;

            return success;
        }}

        {EmitTextLogStructureConversion(inst)}
    }}
");
            }

            return sb;
        }

        private static StringBuilder EmitFields(in LogStructureDefinitionData currStruct)
        {
            var sb = new StringBuilder();

            foreach (var f in currStruct.FieldData)
                sb = f.AppendFieldDeclaration(sb);

            return sb;
        }

        private StringBuilder EmitTextLogStructureWriterContent(in LogStructureDefinitionData currStruct)
        {
            var sb = new StringBuilder();

            var first = true;
            foreach (var f in currStruct.FieldData)
            {
                sb.Append(EmitTextLogStructureWriteExpression(currStruct, f, first));
                first = false;
            }

            return sb;
        }

        private StringBuilder EmitTextLogStructureWriteExpression(in LogStructureDefinitionData currStruct, in LogStructureFieldData currField, bool isFirst)
        {
            var sb = new StringBuilder();

            if (!isFirst)
                sb.Append(@"
            success = formatter.AppendDelimiter(ref output) && success;");

            return currField.AppendFieldWriter(m_Context, sb);
        }

        private StringBuilder EmitTextLogStructureConversion(LogStructureDefinitionData currStruct)
        {
            var sbInit = "";
            var sbFree = "";
            if (currStruct.ContainsPayloads)
            {
                const string initPayloads = @"
            LogControllerScopedLock @lock = default;
            var handle = PerThreadData.ThreadLoggerHandle;
            try
            {
                if (handle.IsValid)
                {
                    @lock = LogControllerScopedLock.CreateAlreadyUnderLock(handle);
                }
                else
                {
                    @lock = LogControllerScopedLock.Create();
                }

                ref var memAllocator = ref @lock.GetLogController().MemoryManager;
";

                const string postfixPayloads = @"
            }
            finally
            {
                @lock.Dispose();
            }
";

                sbInit = initPayloads;
                sbFree = postfixPayloads;
            }


            var optionalUnsafe = currStruct.ShouldBeMarkedUnsafe ? "unsafe " : "";

            var sb = new StringBuilder();

            sb.Append($@"
        public {optionalUnsafe}static implicit operator {currStruct.GeneratedTypeName}(in {currStruct.FullTypeName} arg)
        {{
            {sbInit}
            return new {currStruct.GeneratedTypeName}
            {{
                {EmitTypeConversionFieldCopy(currStruct)}
            }};
            {sbFree}
        }}
");

            return sb;
        }

        private StringBuilder EmitTypeConversionFieldCopy(in LogStructureDefinitionData typeInstance)
        {
            var sb = new StringBuilder();

            // Initialize the TypeId field with const type value of the containing struct
            sb.Append($@"
                {m_StructData.StructIdFieldName} = {typeInstance.GeneratedTypeName}.{typeInstance.GeneratedTypeName}_TypeIdValue,");

            var i = 0;
            foreach (var field in typeInstance.FieldData)
            {
                ++i;
                var fieldName = typeInstance.Symbol.IsTupleType ? $"Item{i}" : field.FieldName;
                var middle = field.IsStatic ? $"{field.FieldTypeName}" : "arg";

                sb = field.AppendFieldConvert(sb, middle, fieldName);
            }

            return sb;
        }
    }
}
