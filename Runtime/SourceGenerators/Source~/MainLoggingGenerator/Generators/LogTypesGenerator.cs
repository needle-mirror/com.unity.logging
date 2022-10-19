using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;
using LoggingCommon;
using MainLoggingGenerator.Extractors;
using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    public class LogTypesGenerator
    {
        private LogTypesGenerator()
        {
        }

        public static bool Execute(in ContextWrapper context, ulong assemblyHash, in LogCallsCollection invokeData,
                                   out LogStructureTypesData structureData,
                                   out StringBuilder generatedTypesCode,
                                   out StringBuilder generatedParseCode)
        {
            using var _ = new Profiler.Auto("LogTypesGenerator.Execute");

            structureData = new LogStructureTypesData();
            generatedTypesCode = new StringBuilder();
            generatedParseCode = new StringBuilder();

            if (!invokeData.IsValid)
                return false;

            var generator = new LogTypesGenerator
            {
                m_AssemblyHash = assemblyHash,
                m_Context = context,
                m_UniqueInvokeArgs = invokeData.UniqueArgumentData,
                m_StructRegistry = new Dictionary<string, LogStructureDefinitionData>(),

                // NOTE: The actual Type ID value is a hash of the assembly name and this value
                m_LocalTypeId = 1,
            };

            var extractStructTypesResult = generator.ExtractLogCallStructureTypesData(context, out var data);
            if (extractStructTypesResult)
            {
                generatedTypesCode = LogTypesEmitter.Emit(context, data);
                structureData = data;
            }

            // Always generate parse code since we require Initializing and Shutdown of Output Handlers
            generatedParseCode = LogParserEmitter.Emit(context, data, assemblyHash);

            return true;
        }

        private bool ExtractLogCallStructureTypesData(ContextWrapper context, out LogStructureTypesData typesData)
        {
            using var _ = new Profiler.Auto("LogTypesGenerator.ExtractLogCallStructureTypesData");

            foreach (var level in m_UniqueInvokeArgs)
            {
                m_CurrentCallKind = level.Key;
                foreach (var arg in level.Value)
                {
                    context.ThrowIfCancellationRequested();

                    if (arg.IsUserType) // user defined how to serialize this struct
                    {
                        ExtractLogCallStructureInstanceUserDefined(context, this, arg);
                    }
                    else
                    {
                        ExtractLogCallStructureInstance(context, this, arg.Symbol, arg, out var _);
                    }
                }
            }

            typesData = new LogStructureTypesData(context, m_StructRegistry.Values.ToList());

            return typesData.IsValid;
        }

        static bool ExtractLogCallStructureInstanceUserDefined(ContextWrapper ctx, LogTypesGenerator gen, LogCallArgumentData argData)
        {
            using var _ = new Profiler.Auto("LogTypesGenerator.ExtractLogCallStructureInstanceUserDefined");

            // First check if this struct type has already been processed, and if so return existing data
            string qualifiedName;
            if (argData.UserDefinedMirrorStruct.IsCreated)
                qualifiedName = Common.GetFullyQualifiedTypeNameFromSymbol(argData.UserDefinedMirrorStruct.WrapperStructTypeInfo);
            else
                qualifiedName = Common.GetFullyQualifiedTypeNameFromSymbol(argData.Symbol);

            if (gen.m_StructRegistry.TryGetValue(qualifiedName, out var structData))
                return true;

            structData = new LogStructureDefinitionData(gen.m_AssemblyHash, argData.Symbol, gen.m_LocalTypeId++, argData, argData.UserDefinedMirrorStruct);

            // Register this struct type for codegen
            if (structData.IsValid)
            {
                gen.m_StructRegistry.Add(qualifiedName, structData);
            }

            return structData.IsValid;
        }


        public static bool ExtractLogCallStructureInstance(ContextWrapper ctx, LogTypesGenerator gen, in ITypeSymbol structSymbol, LogCallArgumentData argData, out LogStructureDefinitionData structData)
        {
            using var _ = new Profiler.Auto("LogTypesGenerator.ExtractLogCallStructureInstance");

            // This is the heart of TextLogger; we extract the fields from a user-defined struct, used as an argument to Log.Info, in order
            // to generate our own internal struct mirroring the user's type. When the user's variable is passed into a logging call, we copy the data
            // into an instance of the corresponding generated type (via an implicit operator), which is used to execute the log operation.
            //
            // We do this for a few reasons:
            // 1. Solves a dependency problem between the user's code, Logging assembly, and source generated code; separating the user's types from the Log flow
            // solves the problem, i.e. generated Log.Info code never "sees" user's types and therefore doesn't take a dependency on them.
            //
            // 2. TextLogger only supports POD structs, allowing us to quickly Blit the data into a message byte buffer; Reference types, Properties, etc. aren't allowed.
            // If we took the user-defined types, their structs would have to conform to this restriction, but with this approach the user's types can still contain
            // unsupported members (for other purposes), which we'll just skip/ignore.
            //
            // 3. A 'TypeId' value must be associated with each struct type so the message parser knows how to properly output the field data (can't use Reflection).
            // A simple, straightforward solution is to just add a field to our generated struct type holding this value. Although we could do this to with the user's
            // types, it does become more complicated.

            // We'll generate an internal struct for each user-defined type if any of the following conditions are met:
            // - Type is used as an actual argument in the Log.Info API call; we must have an internal type to support the source-gen implementation
            // - Type is nested within another struct that is used as an argument AND has at least 1 valid field
            //      That is, if the type is referenced within a logging struct but doesn't have any usable field data (opaque struct)
            //      it'll be skipped.
            //
            // Otherwise, we do NOT generate an internal struct

            structData = default;

            if (argData.DontCreateMirrorStruct(structSymbol))
            {
                return false; // don't add to struct registry
            }

            // First check if this struct type has already been processed, and if so return existing data
            var qualifiedName = Common.GetFullyQualifiedTypeNameFromSymbol(structSymbol);
            if (gen.m_StructRegistry.TryGetValue(qualifiedName, out structData))
                return true;

            if (argData.IsValid == false)
            {
                // If argument data wasn't provided, means we're processing a nested struct which might not be directly used as a argument in log call.
                // However, if this struct type is used as an argument elsewhere, we must ensure it's the argument data is provided otherwise our generated
                // struct names won't match those used in the WriteGenerated() parameters.
                argData = gen.m_UniqueInvokeArgs[gen.m_CurrentCallKind].FirstOrDefault(arg => arg.FullArgumentTypeName.Equals(qualifiedName));
            }

            if (argData.IsValid && argData.DontCreateMirrorStruct())
            {
                return false; // don't add to struct registry
            }

            List<IFieldSymbol> fields = null;
            if (structSymbol.IsTupleType && structSymbol is INamedTypeSymbol namedTypeSymbol)
            {
                fields = namedTypeSymbol.TupleElements.Where(f => f.IsStatic == false).ToList();
            }
            else
            {
                // Get the members from this struct and query all the "Fields", then cast each value from ISymbol to an IFieldSymbol;
                fields = structSymbol.GetMembers().Where(m => m.Kind == SymbolKind.Field).OfType<IFieldSymbol>().Where(f => f.IsStatic == false).ToList();
            }

            var fieldDataList = new List<LogStructureFieldData>(fields.Count);
            foreach (var f in fields)
            {
                using var scope = new Profiler.Auto("LogTypesGenerator.StructFieldExtractor.Extract");

                var fieldData = StructFieldExtractor.Extract(gen.m_Context, gen, f, argData);
                if (fieldData.IsValid)
                {
                    fieldDataList.Add(fieldData);
                }
            }

            var hashSetNames = new HashSet<string>();
            foreach (var field in fieldDataList)
            {
                if (hashSetNames.Add(field.PropertyNameForSerialization) == false)
                {
                    var conflictingSymbols = fieldDataList.Where(f => f.PropertyNameForSerialization == field.PropertyNameForSerialization).Select(f => f.Symbol).ToArray();

                    ctx.LogCompilerErrorFieldNameConflict(conflictingSymbols, field.PropertyNameForSerialization);
                    return false;
                }
            }

            structData = new LogStructureDefinitionData(gen.m_AssemblyHash, structSymbol, gen.m_LocalTypeId++, argData, fieldDataList);

            // Register this struct type for codegen
            if (structData.IsValid)
            {
                gen.m_StructRegistry.Add(qualifiedName, structData);
            }

            return structData.IsValid;
        }

        private ContextWrapper                         m_Context;
        private Dictionary<LogCallKind, List<LogCallArgumentData>>   m_UniqueInvokeArgs;
        private Dictionary<string, LogStructureDefinitionData>    m_StructRegistry;
        private uint                                              m_LocalTypeId;
        private LogCallKind m_CurrentCallKind;
        public ulong m_AssemblyHash;
    }
}
