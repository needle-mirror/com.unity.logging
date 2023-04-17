using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Linq;
using LoggingCommon;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    public static class Common
    {
        public static string GetFullyQualifiedTypeNameFromSymbol(ITypeSymbol symbol)
        {
            if (symbol == null)
            {
                return "";
            }

            var symbolDisplayFormat = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

            // skipping field names to make
            // var data1 = (Id: 42, Position: (5, 6, 7));
            // and
            // var data2 = (42, (5, 6, 7));
            // identical types. Otherwise it is an ambiguous error

            var withoutNames = symbol.ToDisplayParts(symbolDisplayFormat)
                .Where(a => a.Kind != SymbolDisplayPartKind.FieldName && a.Kind != SymbolDisplayPartKind.Space);
            return string.Join("", withoutNames);
        }

        public static string GetFullyQualifiedNameSpaceFromNamespaceSymbol(INamespaceSymbol symbol)
        {
            // If the namespace is inaccessible (e.g. global namespace) return empty string
            if (symbol == null || !symbol.CanBeReferencedByName)
                return "";

            var symbolDisplayFormat = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

            return symbol.ToDisplayString(symbolDisplayFormat);
        }

        public static string CreateUniqueCompilableString()
        {
            return CreateMD5String(Guid.NewGuid() + Guid.NewGuid().ToString());
        }

        public static string CreateMD5String(string input)
        {
            // Use input string to calculate MD5 hash
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);
     
                var base64Chars = new char[24];
                System.Convert.ToBase64CharArray(hashBytes, 0, hashBytes.Length, base64Chars, 0);

                var stuffedChars = new char[48];
                int n = 0;
                foreach (var c in base64Chars)
                {
                    if (c == '_' ||c == '+' || c == '/' || c == '=') {
                        stuffedChars[n++] = '_';
                        switch (c) {
                            case '_': 
                                stuffedChars[n++] = '_';
                                break;
                            case '+':
                                stuffedChars[n++] = 'P';
                                break;
                            case '/':
                                stuffedChars[n++] = 'S';
                                break;
                            case '=':
                                stuffedChars[n++] = 'E';
                                break;
                        }
                    } else {
                        stuffedChars[n++] = c;
                    }
                }

                return new string(stuffedChars, 0, n);
            }
        }

        public static ulong CreateStableHashCodeFromString(string str)
        {
            unchecked
            {
                var hash1 = 5381UL;
                var hash2 = hash1;

                for(var i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i+1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i+1];
                }

                return hash1 + (hash2*1566083941UL);
            }
        }

        public static ulong CreateStructTypeId(ulong assemblyHash, uint localId)
        {
            // We must compute an ID value for each generated that's unique across all Compilation Units.
            //
            // Since each Compilation Unit (assembly) is processed individually, we cannot verify each ID
            // value is unique (through source gen alone). So we'll compute a value from a hash of the
            // current assembly name and combined it with a "localId" value.

            unchecked
            {
                ulong hash = assemblyHash;
                hash = hash * 31 + localId;
                return hash;
            }
        }

        public static void LogInfoMessage(this ContextWrapper context, string message)
        {
            Debug.LogInfo(context, message);
        }

        public static void LogCompilerInfo(this ContextWrapper context, string errorCode, string message, string description = "")
        {
            Debug.LogInfo(context, $"{errorCode}: {message} {description}");
            LogCompilerMessage(context, errorCode, message, DiagnosticSeverity.Info, description);
        }

        public static void LogCompilerInfo(this ContextWrapper context, (string, string) info, string description = "")
        {
            Debug.LogInfo(context, $"{info.Item1}: {info.Item2} {description}");
            LogCompilerMessage(context, info.Item1, info.Item2, DiagnosticSeverity.Info, description);
        }

        public static void LogCompilerWarning(this ContextWrapper context, string errorCode, string message, string description = "")
        {
            Debug.LogWarning(context, $"{errorCode}: {message} {description}");
            LogCompilerMessage(context, errorCode, message, DiagnosticSeverity.Warning, description);
        }

        public static void LogCompilerWarning(this ContextWrapper context, (string, string) warning, string description = "")
        {
            Debug.LogWarning(context, $"{warning.Item1}: {warning.Item2} {description}");
            LogCompilerMessage(context, warning.Item1, warning.Item2, DiagnosticSeverity.Warning, description);
        }

        public static void LogCompilerError(this ContextWrapper context, string errorCode, string message, string description = "")
        {
            Debug.LogError(context, $"{errorCode}: {message} {description}");
            LogCompilerMessage(context, errorCode, message, DiagnosticSeverity.Error, description);
        }

        public static void LogCompilerError(this ContextWrapper context, (string, string) error)
        {
            Debug.LogError(context, $"{error.Item1}: {error.Item2} ");
            LogCompilerMessage(context, error.Item1, error.Item2, DiagnosticSeverity.Error, "");
        }

        public static void LogCompilerError(this ContextWrapper context, (string, string) error, Location location)
        {
            Debug.LogError(context, $"{error.Item1}: {error.Item2} " + location);
            LogCompilerMessage(context, error.Item1, error.Item2, DiagnosticSeverity.Error, "", location);
        }

        public static void LogCompilerErrorTooMuchDecorateArguments(this ContextWrapper context, ArgumentSyntax excessArg)
        {
            var error = CompilerMessages.TooMuchDecorateArguments;

            Debug.LogError(context, $"{error.Item1}: {error.Item2}");
            LogCompilerMessage(context, error.Item1, error.Item2, DiagnosticSeverity.Error, "", excessArg.GetLocation());
        }

        public static void LogCompilerErrorExpectedBoolIn3rdDecorateArgument(this ContextWrapper context, ArgumentSyntax notBoolArg, TypeInfo notBoolButThisType)
        {
            var error = CompilerMessages.ExpectedBoolIn3rdDecorateArgument;

            var typeDescription = (notBoolButThisType.Type == null) ? "null" : notBoolButThisType.Type.ToDisplayString();
            var desc = error.Item2 + " : " + typeDescription;

            Debug.LogError(context, $"{error.Item1}: {desc}");
            LogCompilerMessage(context, error.Item1, desc, DiagnosticSeverity.Error, "", notBoolArg.GetLocation());
        }

        public static void LogCompilerErrorMissingDecorateArguments(this ContextWrapper context, Location location)
        {
            var error = CompilerMessages.MissingDecorateArguments;

            Debug.LogError(context, $"{error.Item1}: {error.Item2}");
            LogCompilerMessage(context, error.Item1, error.Item2, DiagnosticSeverity.Error, "", location);
        }

        public static void LogCompilerErrorMissingDecoratePropertyName(this ContextWrapper context, ArgumentSyntax firstArgThatMustBeString, TypeInfo firstArgType)
        {
            var error = CompilerMessages.MissingDecoratePropertyName;

            var typeDescription = (firstArgType.Type == null) ? "null" : firstArgType.Type.ToDisplayString();
            var desc = error.Item2 + " : " + typeDescription;

            Debug.LogError(context, $"{error.Item1}: {desc}");
            LogCompilerMessage(context, error.Item1, desc, DiagnosticSeverity.Error, "", firstArgThatMustBeString.GetLocation());
        }

        public static void LogCompilerErrorVoidType(this ContextWrapper context, Location location)
        {
            var error = CompilerMessages.CannotBeVoidError;

            Debug.LogError(context, $"{error.Item1}: {error.Item2} {location}");
            LogCompilerMessage(context, error.Item1, error.Item2, DiagnosticSeverity.Error, "", location);
        }

        public static void LogCompilerErrorEnumUnsupported(this ContextWrapper context, Location location)
        {
            var error = CompilerMessages.EnumsUnsupportedError;

            Debug.LogError(context, $"{error.Item1}: {error.Item2} {location}");
            LogCompilerMessage(context, error.Item1, error.Item2, DiagnosticSeverity.Error, "", location);
        }

        public static void LogCompilerErrorInvalidArgument(this ContextWrapper context, TypeInfo typeInfo, ExpressionSyntax exp)
        {
            var error = CompilerMessages.InvalidArgument;

            var typeSymbol = typeInfo.Type;
            var typeConvertedSymbol = typeInfo.ConvertedType;

            var typeSymbolDesc = (typeSymbol == null) ? "<null>" : typeSymbol.ToDisplayString();
            var typeConvertedSymbolDesc = (typeConvertedSymbol == null) ? "<null>" : typeConvertedSymbol.ToDisplayString();
            var expDesc = (exp == null) ? "<null>" : exp.Kind().ToString();

            var generalDesc = $"TypeConvertedSymbol = '{typeConvertedSymbolDesc}' TypeSymbol = '{typeSymbolDesc}' Exp = '{expDesc}'";

            var location = exp.GetLocation();

            var desc = error.Item2 + " : " + generalDesc;
            Debug.LogError(context, $"{error.Item1}: {desc} {location}");
            LogCompilerMessage(context, error.Item1, desc, DiagnosticSeverity.Error, "", location);
        }

        public static void LogCompilerErrorCannotFitInFixedString(this ContextWrapper context, Location location)
        {
            var error = CompilerMessages.MessageFixedStringError;
            Debug.LogError(context, $"{error.Item1}: {error.Item2} {location}");
            LogCompilerMessage(context, error.Item1, error.Item2, DiagnosticSeverity.Error, "", location);
        }

        public static void LogCompilerErrorFieldNameConflict(this ContextWrapper context, IFieldSymbol[] fields, string conflictingName)
        {
            var error = CompilerMessages.MessageErrorFieldNameConflict;
            var msg = string.Format(error.Item2, conflictingName);

            Location location = null;

            if (fields.Length > 0)
            {
                location = fields[fields.Length - 1].Locations.FirstOrDefault();
                if (location == null)
                    location = fields[0].Locations.FirstOrDefault();
            }

            if (location == null)
                location = Location.None;

            Debug.LogError(context, $"{error.Item1}: {msg} {location}");
            LogCompilerMessage(context, error.Item1, msg, DiagnosticSeverity.Error, "", location);
        }

        public static void LogCompilerWarningReferenceType(this ContextWrapper context, ExpressionSyntax expression)
        {
            var warning = CompilerMessages.ReferenceError;

            var location = expression.GetLocation();
            var desc = $"{warning.Item2} : {expression}";
            Debug.LogWarning(context, $"{warning.Item1}: {desc} {location}");
            LogCompilerMessage(context, warning.Item1, desc, DiagnosticSeverity.Warning, "", location);
        }

        public static void LogCompilerWarningOutputWriter(this ContextWrapper context, IFieldSymbol currFieldSymbol)
        {
            var warning = CompilerMessages.OutputWriterError;

            var location = currFieldSymbol.Locations.Length == 0 ? null : currFieldSymbol.Locations[0];
            var desc = $"{warning.Item2} : {currFieldSymbol}";
            Debug.LogWarning(context, $"{warning.Item1}: {desc} {location}");
            LogCompilerMessage(context, warning.Item1, desc, DiagnosticSeverity.Warning, "", location);
        }

        public static void LogCompilerWarningEnumUnsupportedAsField(this ContextWrapper context, ExpressionSyntax expression, IFieldSymbol fieldSymbol)
        {
            var warning = CompilerMessages.EnumsUnsupportedError;

            var location = expression.GetLocation();
            var desc = $"{warning.Item2} : <{expression}> has field type <{fieldSymbol}> it will be ignored";
            Debug.LogWarning(context, $"{warning.Item1}: {desc} {location}");
            LogCompilerMessage(context, warning.Item1, desc, DiagnosticSeverity.Warning, "", location);
        }

        public static void LogCompilerErrorUnhandledException(this ContextWrapper context, Exception e)
        {
            Debug.LogException(context, e);
            var error = CompilerMessages.GeneralException;
            LogCompilerMessage(context, error.Item1, error.Item2 + ": " + e, DiagnosticSeverity.Error, "", null);
        }

        public static void LogCompilerWarningWrongArgumentsInWriteCall(this ContextWrapper context, InvocationExpressionSyntax invocation)
        {
            var warn = CompilerMessages.InvalidWriteCall;
            Debug.LogWarning(context, "Empty Write call ignored " + invocation.GetLocation());
            LogCompilerMessage(context, warn.Item1, warn.Item2, DiagnosticSeverity.Warning, "", invocation.GetLocation());
        }

        public static void LogCompilerErrorCannotUseField(this ContextWrapper context, IFieldSymbol field, Location location = null)
        {
            if (location == null)
                location = field.Locations.FirstOrDefault();
            var error = CompilerMessages.FieldValueTypeError;
            Debug.LogError(context, "Cannot serialize field " + field.Type);
            LogCompilerMessage(context, error.Item1, error.Item2, DiagnosticSeverity.Error, "", location);
        }

        public static void LogCompilerErrorUnsupportedField(this ContextWrapper context, IFieldSymbol field,
            Location location = null)
        {
            if (location == null)
                location = field.Locations.FirstOrDefault();
            var error = CompilerMessages.UnsupportedFieldTypeError;
            var msg = string.Format(error.Item2, field.Type);
            Debug.LogError(context, msg);
            LogCompilerMessage(context, error.Item1, msg, DiagnosticSeverity.Error, "", location);
        }

        // literal message analysis messages
        public static void LogCompilerLiteralMessage(this ContextWrapper context, string errorCode, string message, Location location)
        {
            LogCompilerMessage(context, errorCode, message, DiagnosticSeverity.Warning, "", location);
        }

        public static void LogCompilerLiteralMessageMissingArgForHole(this ContextWrapper context, string msg, Location location)
        {
            var warn = CompilerMessages.LiteralMessageMissingArgForHole;
            msg = string.Format(warn.Item2, msg);

            LogCompilerMessage(context, warn.Item1, msg, DiagnosticSeverity.Warning, "", location);
        }

        public static void LogCompilerLiteralMessageMissingHoleForArg(this ContextWrapper context, Location location)
        {
            var warn = CompilerMessages.LiteralMessageMissingHoleForArg;
            var msg = warn.Item2;

            LogCompilerMessage(context, warn.Item1, msg, DiagnosticSeverity.Warning, "", location);
        }

        public static void LogCompilerLiteralMessageInvalidArgument(this ContextWrapper context, string msg, Location location)
        {
            var warn = CompilerMessages.LiteralMessageInvalidArgument;
            msg = warn.Item2;

            LogCompilerMessage(context, warn.Item1, msg, DiagnosticSeverity.Warning, "", location);
        }

        public static void LogCompilerLiteralMessageMissingIndexArg(this ContextWrapper context, int missedIndx, Location location)
        {
            var warn = CompilerMessages.LiteralMessageMissingIndexArg;
            var msg = string.Format(warn.Item2, missedIndx);

            LogCompilerMessage(context, warn.Item1, msg, DiagnosticSeverity.Warning, "", location);
        }

        public static void LogCompilerLiteralMessageRepeatingNamedArg(this ContextWrapper context, string msg, Location location)
        {
            var warn = CompilerMessages.LiteralMessageRepeatingNamedArg;
            msg = string.Format(warn.Item2, msg);

            LogCompilerMessage(context, warn.Item1, msg, DiagnosticSeverity.Warning, "", location);
        }

        // ---------------------------------

        private static void LogCompilerMessage(ContextWrapper context, string errorCode, string message, DiagnosticSeverity severity, string description = "")
        {
            LogCompilerMessage(context, errorCode, message, severity, description, context.DefaultLocation());
        }

        private static void LogCompilerMessage(ContextWrapper context, string errorCode, string message, DiagnosticSeverity severity, string description, Location location)
        {
            // Unity shows:
            //  severity, error code, message and location.

            var descriptor = new DiagnosticDescriptor(errorCode, "", message, "Logging. Source Generator", severity, true, description);
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location));
        }
    }
}
