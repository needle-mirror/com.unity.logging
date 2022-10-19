using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator.Logging
{
    public readonly struct LogCallArgumentData : IEquatable<LogCallArgumentData>
    {
        public readonly ITypeSymbol Symbol;
        public readonly string      ArgumentTypeName;
        public readonly string      FullArgumentTypeName;
        public readonly string      GeneratedTypeName;
        public readonly string      FullGeneratedTypeName;
        public readonly string      LiteralValue;
        public readonly ExpressionSyntax Expression;
        public readonly FixedStringUtils.FSType FixedStringType;
        public readonly CustomMirrorStruct UserDefinedMirrorStruct;

        private readonly string      m_UniqueId;

        public bool        IsUserType => UserDefinedMirrorStruct.IsCreated;

        private bool IsLiteral => LiteralValue != null;
        public bool IsValid => Symbol != null;
        private bool IsNonLiteralString => IsString && !IsLiteral;
        private bool IsString => Symbol != null && Symbol.SpecialType == SpecialType.System_String;
        public bool IsManagedString => IsString && (FixedStringType.IsValid == false && IsNativeOrUnsafeText == false);
        private bool IsUnsafePointer => Symbol != null && Symbol.TypeKind == TypeKind.Pointer;
        public bool IsUnsafe => IsUnsafePointer;

        private readonly bool IsNativeText;
        private readonly bool IsNativeOrUnsafeText;
        public bool IsConvertibleToString =>
                        Symbol != null &&
                        Symbol.SpecialType == SpecialType.None &&
                        Symbol.TypeKind != TypeKind.Struct &&
                        FixedStringType.IsValid == false &&
                        (IsNativeOrUnsafeText || Symbol.IsReferenceType || Symbol.TypeKind == TypeKind.Enum);
        public bool ShouldUsePayloadHandle => (IsString || IsConvertibleToString) && IsNativeOrUnsafeText == false;

        // public static LogCallArgumentData Pointer(ITypeSymbol typeSymbol, ExpressionSyntax expression)
        // {
        //     //return TODO_IMPLEMENT_ME;
        // }

        public static LogCallArgumentData UserDefinedType(TypeInfo typeInfo, ArgumentSyntax argumentSyntax, CustomMirrorStruct userOverload, out string qualifiedName)
        {
            qualifiedName = "";

            var typeSymbol = typeInfo.Type;
            if (typeSymbol is null or IErrorTypeSymbol)
            {
                return default;
            }

            qualifiedName = Common.GetFullyQualifiedTypeNameFromSymbol(typeSymbol);

            return new LogCallArgumentData(typeSymbol, typeSymbol.Name, "", argumentSyntax.Expression, userDefinedMirrorStruct: userOverload);
        }

        public LogCallArgumentData(ITypeSymbol typeSymbol, string argType, string litValue, ExpressionSyntax expressionSyntax, FixedStringUtils.FSType fixedStringType = default, CustomMirrorStruct userDefinedMirrorStruct = default)
        {
            UserDefinedMirrorStruct = userDefinedMirrorStruct;
            FixedStringType = fixedStringType;
            Symbol = typeSymbol;
            ArgumentTypeName = argType;
            FullArgumentTypeName = Common.GetFullyQualifiedTypeNameFromSymbol(typeSymbol);
            m_UniqueId = Common.CreateMD5String(ArgumentTypeName + FullArgumentTypeName + typeSymbol.Name);
            Expression = expressionSyntax;

            var containingNamespace = "";

            var usersType = UserDefinedMirrorStruct.IsCreated;
            if (usersType)
            {
                GeneratedTypeName = UserDefinedMirrorStruct.WrapperStructureName;

                containingNamespace = Common.GetFullyQualifiedNameSpaceFromNamespaceSymbol(UserDefinedMirrorStruct.WrapperStructTypeInfo.ContainingNamespace);

                LiteralValue = null;
                IsNativeText = false;
                IsNativeOrUnsafeText = false;
            }
            else
            {
                if (typeSymbol.SpecialType == SpecialType.System_String)
                {
                    GeneratedTypeName = ArgumentTypeName;
                }
                else
                {
                    GeneratedTypeName = $"{ArgumentTypeName}_{m_UniqueId}";
                }

                containingNamespace = Common.GetFullyQualifiedNameSpaceFromNamespaceSymbol(typeSymbol.ContainingNamespace);

                LiteralValue = litValue;

                IsNativeOrUnsafeText = LiteralValue == null && Symbol != null && (Symbol.Name == "UnsafeText" || Symbol.Name == "NativeText");
                IsNativeText = IsNativeOrUnsafeText && Symbol.Name == "NativeText";
            }

            // The TypeGenerator will place the generated struct in the same namespace as the user's type, unless
            // the user's struct was declared in the "root" namespace. We'd like to avoid adding generated types
            // to the global namespace so substitute Unity.Logging instead.

            if (!string.IsNullOrWhiteSpace(containingNamespace))
            {
                FullGeneratedTypeName = "global::" + containingNamespace + "." + GeneratedTypeName;
            }
            else
            {
                if (usersType)
                    FullGeneratedTypeName = "global::" + GeneratedTypeName;
                else
                    FullGeneratedTypeName = "global::Unity.Logging." + GeneratedTypeName;
            }
        }

        public static LogCallArgumentData LiteralAsFixedString(ITypeSymbol typeSymbol, FixedStringUtils.FSType fixedString, string argText, ExpressionSyntax expression)
        {
            return new LogCallArgumentData(typeSymbol, fixedString.Name, argText, expression, fixedString);
        }

        public bool Equals(LogCallArgumentData other)
        {
            return m_UniqueId == other.m_UniqueId;
        }

        public bool IsSpecialSerializableType()
        {
            if (IsValid == false)
                return false;

            return FixedStringType.IsValid || FixedStringUtils.IsSpecialSerializableType(Symbol);
        }

        public override string ToString()
        {
            if (IsSpecialSerializableType())
                return $"{{[Argument. Special type] literal=<{LiteralValue}> | expression=<{Expression}>}}";
            return $"{{[Argument] genTypeName=<{GeneratedTypeName}> literal=<{LiteralValue}> | expression=<{Expression}>}}";
        }

        public (string type, string name) GetParameterTypeForUser(bool visibleToBurst, int i)
        {
            if (IsSpecialSerializableType())
            {
                if (visibleToBurst)
                {
                    if (Symbol.SpecialType is SpecialType.System_Char or SpecialType.System_Boolean)
                        return ("int", $"iarg{i}");
                }

                return (ArgumentTypeName, $"arg{i}");
            }

            if (Symbol.SpecialType == SpecialType.System_String)
            {
                if (visibleToBurst)
                    return ("PayloadHandle", $"arg{i}");

                return (FullArgumentTypeName, $"arg{i}");
            }

            if (IsNativeOrUnsafeText)
            {
                if (visibleToBurst)
                    return ("PayloadHandle", $"arg{i}");
                return (FullArgumentTypeName, $"arg{i}");
            }

            if (Symbol.TypeKind == TypeKind.Enum)
            {
                if (visibleToBurst)
                    return ("PayloadHandle", $"arg{i}");
                return (FullArgumentTypeName, $"arg{i}");
            }

            if (Symbol.IsReferenceType)
            {
                if (visibleToBurst)
                    return ("PayloadHandle", $"arg{i}");
                return (FullArgumentTypeName, $"arg{i}");
            }

            // blittable - use mirror struct
            if (visibleToBurst)
                return (FullGeneratedTypeName, $"arg{i}");

            // visible to user - use original struct
            return (FullArgumentTypeName, $"arg{i}");
        }

        /// <summary>
        /// The code that is executed before calling the burst function, to make the burst function burst-friendly
        /// </summary>
        public void AppendConvertCode(int argNumber, StringBuilder sbConvert, StringBuilder sbConvertGlobal)
        {
            if (ShouldUsePayloadHandle)
            {
                static string EmitCopyStringToPayloadBuffer(string dst, string src, bool globalMemManager, bool prependTypeId = false, bool prependLength = false, bool deferredRelease = false)
                {
                    var sbOptParams = new StringBuilder();
                    if (prependTypeId)
                        sbOptParams.Append(", prependTypeId: true");
                    if (prependLength)
                        sbOptParams.Append(", prependLength: true");
                    if (deferredRelease)
                        sbOptParams.Append(", deferredRelease: true");

                    var memManager = "ref logController.MemoryManager";
                    if (globalMemManager)
                        memManager = "ref Unity.Logging.Internal.LoggerManager.GetGlobalDecoratorMemoryManager()";

                    return $"var payloadHandle_{dst} = Unity.Logging.Builder.CopyStringToPayloadBuffer({src}, {memManager}{sbOptParams});";
                }

                var call = (IsConvertibleToString && !IsNonLiteralString) ? ".ToString()" : "";
                sbConvert.AppendLine(EmitCopyStringToPayloadBuffer($"arg{argNumber}", $"arg{argNumber}" + call, globalMemManager: false, prependTypeId: true, prependLength: true));
                sbConvertGlobal?.AppendLine(EmitCopyStringToPayloadBuffer($"arg{argNumber}", $"arg{argNumber}" + call, globalMemManager: true, prependTypeId: true, prependLength: true));
            }
        }

        /// <summary>
        /// The code that is executed in the burst function that converts received arguments (burst-friendly ones) into the ones that should be used instead
        /// </summary>
        public StringBuilder AppendCastCode(int i, StringBuilder castCode)
        {
            if (IsSpecialSerializableType())
            {
                if (Symbol.SpecialType == SpecialType.System_Char)
                {
                    return castCode.AppendLine($"var arg{i} = (char)iarg{i};");
                }

                if (Symbol.SpecialType == SpecialType.System_Boolean)
                {
                    return castCode.AppendLine($"var arg{i} = iarg{i} == 1;");
                }
            }

            return castCode;
        }

        public StringBuilder AppendUserVisibleParameter(StringBuilder sb, bool visibleToBurst, int i)
        {
            var argTypeName = GetParameterTypeForUser(visibleToBurst, i);

            if (IsNativeText)
                return sb.Append($"in NativeTextBurstWrapper {argTypeName.name}");

            if (argTypeName.type == "string" || argTypeName.type == "global::System.String")
                return sb.Append($"string {argTypeName.name}");

            if (IsUnsafePointer)
            {
                if (visibleToBurst)
                    return sb.Append($"IntPtr arg{i}"); // casting to IntPtr

                return sb.Append($"{argTypeName.type} {argTypeName.name}"); // keep unsafe pointer here. need to mark the function as unsafe
            }

            return sb.Append($"in {argTypeName.type} {argTypeName.name}");
        }

        public StringBuilder AppendCallParameterForBurst(StringBuilder sb, int i)
        {
            if (ShouldUsePayloadHandle)
                return sb.Append($"payloadHandle_arg{i}");

            if (IsUnsafePointer)
                return sb.Append($"new IntPtr(arg{i})");

            if (Symbol.SpecialType == SpecialType.System_Char)
                return sb.Append($"(int)arg{i}"); // casting to int

            if (Symbol.SpecialType == SpecialType.System_Boolean)
                return sb.Append($"(arg{i} ? 1 : 0)");

            return sb.Append($"arg{i}");
        }

        public StringBuilder AppendHandlesBuildCode(StringBuilder sb, int i)
        {
            if (IsSpecialSerializableType() || IsUnsafePointer)
            {
                return sb.Append($@"
            handle = Unity.Logging.Builder.BuildContextSpecialType(arg{i}, ref memManager);
            if (handle.IsValid)
                handles.Add(handle);
");
            }

            if (ShouldUsePayloadHandle)
            {
                return sb.Append($@"
            if (arg{i}.IsValid)
                handles.Add(arg{i});
");
            }

            return sb.Append($@"
            handle = Unity.Logging.Builder.BuildContext(arg{i}, ref memManager);
            if (handle.IsValid)
                handles.Add(handle);
");
        }

        public bool DontCreateMirrorStruct(ITypeSymbol symbol)
        {
            if (symbol == null) return true;

            if (symbol.SpecialType == SpecialType.None &&
                symbol.TypeKind != TypeKind.Struct &&
                FixedStringType.IsValid == false &&
                ((symbol.Name == "UnsafeText" || symbol.Name == "NativeText") || symbol.IsReferenceType || symbol.TypeKind == TypeKind.Enum))
                return true;

            if (FixedStringUtils.IsSpecialSerializableType(symbol)) return true;
            if (symbol.SpecialType == SpecialType.System_String) return true;
            if (symbol.TypeKind == TypeKind.Pointer) return true;

            return false;
        }

        public bool DontCreateMirrorStruct()
        {
            if (IsConvertibleToString) return true; // string/payloadHandle will be used instead
            if (IsSpecialSerializableType()) return true;
            if (IsString) return true;
            if (IsUnsafePointer) return true;

            return false;
        }
    }
}
