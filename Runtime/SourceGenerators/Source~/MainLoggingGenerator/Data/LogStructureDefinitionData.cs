using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace SourceGenerator.Logging
{
    public readonly struct LogStructureDefinitionData
    {
        public readonly ITypeSymbol                         Symbol;
        public readonly UInt64                              TypeId;
        public readonly string                              FullTypeName;
        public readonly string                              UniqueId;
        public readonly string                              GeneratedTypeName;
        public readonly string                              FullGeneratedTypeName;
        public readonly string                              Formatter;
        public readonly string                              ContainingNamespace;
        public readonly List<LogStructureFieldData>         FieldData;

        public bool IsValid => Symbol != null && TypeId != 0;
        public bool IsTaggedForLogging => !string.IsNullOrEmpty(Formatter);
        public bool ContainsPayloads => FieldData.Any(f => f.NeedsPayload);

        public LogStructureDefinitionData(ulong assemblyHash, ITypeSymbol typeSymbol, uint localId, in LogCallArgumentData argInstance, List<LogStructureFieldData> fields)
        {
            Symbol = typeSymbol;
            FullTypeName = Common.GetFullyQualifiedTypeNameFromSymbol(typeSymbol);

            TypeId = Common.CreateStructTypeId(assemblyHash, localId);

            // Typically the generated type name is produced by LogCallInvocationArgumentData, when analyzing arguments passed into a Log.Info call,
            // in which case we simply copy over those values. However, when dealing with nested structs, we may have types that aren't directly used as
            // arguments into a Log call but we still require generated types. In this case, simply create a "dummy" argument and then copy out the generated values.

            if (argInstance.IsValid)
            {
                UniqueId = argInstance.UniqueId;
                GeneratedTypeName = argInstance.GeneratedTypeName;
                FullGeneratedTypeName = argInstance.FullGeneratedTypeName;
            }
            else
            {
                var dummyArg = new LogCallArgumentData(typeSymbol, "", "", null);

                UniqueId = dummyArg.UniqueId;
                GeneratedTypeName = dummyArg.GeneratedTypeName;
                FullGeneratedTypeName = dummyArg.FullGeneratedTypeName;
            }

            // Parse out the namespace from the full type name
            ContainingNamespace = FullGeneratedTypeName.Replace("global::", "").Replace("." + GeneratedTypeName, "");

            Formatter = "";
            FieldData = fields;
        }
    }
}
