using System;
using System.Text;
using LoggingCommon;
using Microsoft.CodeAnalysis;
using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    class LogParserEmitter
    {
        private LogParserEmitter()
        {
        }

        public static StringBuilder Emit(in GeneratorExecutionContext context, in LogStructureTypesData structData, ulong assemblyHash)
        {
            using var _ = new Profiler.Auto("LogParserEmitter.Emit");

            var emitter = new LogParserEmitter
            {
                m_StructData = structData
            };

            var sb = new StringBuilder();
            sb.Append(EmitStrings.SourceFileHeader);

            var isBursted = true;

            var uniquePostfix = Common.CreateUniqueCompilableString();

            sb.AppendFormat(EmitStrings.TextLoggerStructureParserEnclosure,
                emitter.EmitTextLogParserDefinition(uniquePostfix),
                uniquePostfix,
                isBursted ? "true" : "false",
                $"{assemblyHash:X4}");

            sb.AppendLine(EmitStrings.SourceFileFooter);

            return sb;
        }

        private StringBuilder EmitTextLogParserDefinition(string uniquePostfix)
        {
            // TODO: This log Writer can only be Bursted if all the generated structs FormatWritters are
            // also Burst-compatible. Currently this is always true, but once custom formatters are added this
            // may not be the case.
            //
            // Ideally we'd have 2 versions of context Writers: Bursted and non-Bursted, in which the individual
            // WriteFormattedOutputs are called for the corresponding generated types. That is, if a given
            // struct has a Burst-compatible Formatter, then it's called from the Bursted version and if not
            // the non-Bursted version is used.
            //
            // Note that with nested structs, all the types included must also have Burst-compatible formatters.
            // So some additional code-gen logic is necessary to ensure the entire WriteFormattedOutput call chain
            // is Burstable before placing it in the Bursted version.

            var isBursted = true;

            var sb = new StringBuilder();

            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterMethodDefinition,
                isBursted ? EmitStrings.BurstCompileAttr : "",
                uniquePostfix,
                EmitTextLoggerStructureFormatWriterCases()
            );

            return sb;
        }

        private StringBuilder EmitTextLoggerStructureFormatWriterCases()
        {
            var sb = new StringBuilder();

            // default handlers
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 1, "int");
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 2, "uint");
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 3, "ulong");
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 4, "long");
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 5, "char");
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 6, "float");

            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeDoubleMethod, 7, "double");
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeBoolMethod, 8, "bool");

            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 100, "FixedString32Bytes");
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 101, "FixedString64Bytes");
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 102, "FixedString128Bytes");
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 103, "FixedString512Bytes");
            sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterSpecialTypeMethodCase, 104, "FixedString4096Bytes");

            foreach (var st in m_StructData.StructTypes)
            {
                sb.AppendFormat(EmitStrings.TextLoggerStructureFormatWriterMethodCase, st.TypeId, st.FullGeneratedTypeName);
            }

            return sb;
        }

        private LogStructureTypesData         m_StructData;
    }
}
