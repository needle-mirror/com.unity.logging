using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceGenerator.Logging
{
    public readonly struct LogStructureTypesData
    {
        public readonly List<LogStructureDefinitionData>  StructTypes;
        public readonly string                            StructIdFieldName;

        private const string TextLoggerStructTypeFieldName = @"__Internal_Unity_TextLogger_Struct_TypeId__";

        public LogStructureTypesData(List<LogStructureDefinitionData> structTypes)
        {
            StructTypes = structTypes;

            // A "TypeId" field is added to each generated struct but the name of this field cannot match any of the user's field names
            // Although the base name is unique, we cannot allow any possibility of a conflict, and therefore if the base name is actually
            // used in any of the user's structs we'll append a GUID to it and check again, until a unique name is found.
            //
            // NOTE: Only Fields from the user structs are reproduced within the internal generated structs; Properties, Methods, etc. aren't
            // transfered over and therefore a conflict with these other identifiers isn't possible.

            const string baseName = TextLoggerStructTypeFieldName;
            var candidateName = baseName;
            bool conflict;
            var iteration = 0;

            do
            {
                conflict = false;

                // If candidateName matches any field name on any struct OR the Type name for any user struct, we have a conflict and must rename candidateName
                if (StructTypes.FirstOrDefault(st => st.FieldData.FirstOrDefault(f => f.FieldName.Equals(candidateName)).IsValid).IsValid ||
                    StructTypes.FirstOrDefault(st => st.Symbol.Name.Equals(candidateName)).IsValid)
                {
                    conflict = true;
                    iteration++;

                    candidateName = baseName + Common.CreateUniqueCompilableString();
                }

                // TODO: We also need to check the enum type name, once enums are supported.
                // public struct MyStruct
                // {
                //     enum __Internal_Unity_Struct_TypeId__ { one, two, three }
                //     __Internal_Unity_Struct_TypeId__ MyEnum;     // This will also cause an identifier conflict
                // }

                // Failsafe if this logic fails
                if (iteration > 100)
                {
                    throw new System.OperationCanceledException($"Unable to find a unique name for generated TypeId field {baseName}. Please make sure this identifier isn't being used within any structs passed into Log.Info.");
                }
            }
            while (conflict);

            StructIdFieldName = candidateName;
        }

        public bool IsValid => (StructTypes?.Count ?? 0) > 0;
    }
}
