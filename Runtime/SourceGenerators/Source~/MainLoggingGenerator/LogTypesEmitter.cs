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
        private GeneratorExecutionContext     m_Context;
        private LogStructureTypesData         m_StructData;

        private LogTypesEmitter()
        {
        }

        public static StringBuilder Emit(in GeneratorExecutionContext context, in LogStructureTypesData structData)
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

            var structList = m_StructData.StructTypes.Where(x => x.ContainingNamespace == currNamespace);
            foreach (var inst in structList)
            {
                sb.Append($@"
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct {inst.GeneratedTypeName} : IWriterFormattedOutput
    {{
        public const ulong {inst.GeneratedTypeName}_TypeIdValue = {inst.TypeId.ToString()};

        public ulong {m_StructData.StructIdFieldName};

        {EmitFields(inst)}

        public bool WriteFormattedOutput(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth)
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
            {
                sb.Append($@"
        // Field name {f.PropertyNameForSerialization}");

                // attribute
                if (f.Symbol.Type.SpecialType == SpecialType.System_Boolean)
                {
                    sb.Append($@"
        public byte {f.FieldName};");
                }
                else if (f.Symbol.Type.SpecialType == SpecialType.System_Char)
                {
                    sb.Append($@"
        public int {f.FieldName};");
                }
                else if (f.NeedsPayload)
                {
                    sb.Append($@"
        public PayloadHandle {f.FieldName};");
                }
                else
                {
                    sb.Append($@"
        public {f.FieldTypeName} {f.FieldName};");
                }
            }

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

            if (currField.IsGeneratedType)
            {
                // If field is also a generated struct type, then simply call it's own Write method
                sb.Append($@"
            success = formatter.WriteChild(ref output, ""{currField.PropertyNameForSerialization}"", ref {currField.FieldName}, ref memAllocator, ref currArgSlot, depth + 1) && success;");
            }
            else
            {
                // Generate default writers for each primitive field in the struct
                switch (currField.Symbol.Type.Name)
                {
                    // These types are supported directly by UnsafeText and don't require a cast
                    case "Char":
                        // cast needed, because it is stored as int (blittable), char is not blittable
                        sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{currField.PropertyNameForSerialization}"", (char){currField.FieldName}, ref currArgSlot) && success;");

                        break;

                    case "Byte":
                    case "SByte":
                    case "Int16":
                    case "UInt16":
                    case "Int32":
                    case "UInt32":
                    case "Int64":
                    case "UInt64":
                    case "Single":
                        sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{currField.PropertyNameForSerialization}"", {currField.FieldName}, ref currArgSlot) && success;");
                        break;

                    // These types require and explicit cast to avoid compile errors
                    case "Double":
                        sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{currField.PropertyNameForSerialization}"", {currField.FieldName}, ref currArgSlot) && success;");
                        break;

                    case "Boolean":
                        sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{currField.PropertyNameForSerialization}"", {currField.FieldName} != 0, ref currArgSlot) && success;");
                        break;

                    // TODO: These should be formatted without throwing out the precision
                    case "Decimal":
                        sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{currField.PropertyNameForSerialization}"", {currField.FieldName}, ref currArgSlot) && success;");
                        break;

                    // TODO: These should be formatted into hex strings, e.g. 0x0012345
                    case "IntPtr":
                    case "UIntPtr":
                        sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{currField.PropertyNameForSerialization}"", {currField.FieldName}, ref currArgSlot) && success;");
                        break;

                    case "String":
                    case "UnsafeText":
                    case "NativeText":
                        sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{currField.PropertyNameForSerialization}"", {currField.FieldName}, ref memAllocator, ref currArgSlot) && success;");
                        break;

                    default:

                        var warningNeeded = true;
                        var fieldTypeName = currField.Symbol.Type.Name;

                        if (fieldTypeName.StartsWith("FixedString"))
                        {
                            sb.Append($@"
            success = formatter.WriteProperty(ref output, ""{currField.PropertyNameForSerialization}"", {currField.FieldName}, ref currArgSlot) && success;");
                            warningNeeded = false;
                        }

                        if (warningNeeded)
                        {
                            m_Context.LogCompilerWarning(CompilerMessages.OutputWriterError);
                        }
                        break;
                }
            }

            return sb;
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

            var sb = new StringBuilder();

            sb.Append($@"
        public static implicit operator {currStruct.GeneratedTypeName}(in {currStruct.FullTypeName} arg)
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

                if (field.Symbol.Type.Name == "String")
                {
                    sb.Append(@$"
                {field.FieldName} = Unity.Logging.Builder.CopyStringToPayloadBuffer({middle}.{fieldName}, ref memAllocator, prependLength: true, deferredRelease: true),
");
                }
                else if (field.Symbol.Type.Name == "NativeText" || field.Symbol.Type.Name == "UnsafeText")
                {
                    sb.Append(@$"
                {field.FieldName} = Unity.Logging.Builder.CopyCollectionStringToPayloadBuffer({middle}.{fieldName}, ref memAllocator, prependLength: true, deferredRelease: true),
");
                }
                else if (field.Symbol.Type.SpecialType == SpecialType.System_Boolean)
                {
                    sb.Append(@$"
                {field.FieldName} = (byte)({middle}.{fieldName} ? 1 : 0),
");
                }
                else
                {
                    sb.Append(@$"
                {field.FieldName} = {middle}.{fieldName},
");
                }
            }

            return sb;
        }
    }
}
