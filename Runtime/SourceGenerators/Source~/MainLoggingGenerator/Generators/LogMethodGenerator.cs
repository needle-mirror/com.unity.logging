using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using LoggingCommon;
using MainLoggingGenerator.Extractors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    using ArgumentRegistry = Dictionary<string, LogCallArgumentData>;

    public class LogMethodGenerator
    {
        private LogMethodGenerator() {}

        public static bool Execute(in ContextWrapper context, ulong assemblyHash, ImmutableArray<CustomMirrorStruct> userTypes, out LogCallsCollection invokeData, out string generatedCode)
        {
            using var _ = new Profiler.Auto("LogMethodGenerator.Execute");

            invokeData = new LogCallsCollection();
            generatedCode = "";

            var generator = new LogMethodGenerator
            {
                m_Context = context,
                m_UserTypes = userTypes
            };

            if (!generator.ExtractLogInvocationData(context, out var data))
                return false;

            generatedCode = LogMethodEmitter.Emit(context, data, assemblyHash);
            invokeData = data;
            return true;
        }

        private bool ExtractLogInvocationData(ContextWrapper context, out LogCallsCollection data)
        {
            using var _ = new Profiler.Auto("LogMethodGenerator.ExtractLogInvocationData");

            data = new LogCallsCollection();

            // Get all the instances of calls to Log.Info from the syntax processor
            var syntaxReceiver = context.UserData as LogCallFinder;
            if (syntaxReceiver == null)
            {
                Debug.LogVerbose(m_Context, $"[ExtractLogCall][FAIL] syntaxReceiver == null");
                return false;
            }

            var instances = new Dictionary<LogCallKind, List<LogCallData>>();

            var allLevels = Enum.GetValues(typeof(LogCallKind));
            // we generate all types of Log. calls so user can see them in the autocompletion
            foreach (LogCallKind kind in allLevels)
                instances[kind] = new List<LogCallData>(32);

            for (var i = 0; i < syntaxReceiver.LogCalls.Count; i++)
            {
                context.ThrowIfCancellationRequested();

                var logCall = syntaxReceiver.LogCalls[i];
                var logCallLevel = syntaxReceiver.LogCallsLevel[i];

                if (ExtractLogCall(context, logCall, logCallLevel, out var invokeInstData))
                {
                    Debug.LogVerbose(m_Context, $"[ExtractLogCall] Extracted <{logCallLevel}> Log Call: <{logCall}>\n{logCall.GetLocation()}\n");

                    UpdateInvocationListWithNewInstance(instances[logCallLevel], invokeInstData);

                    if (invokeInstData.HasLiteralStringMessage)
                    {
                        AnalyzeMessageStringWarnings(context, logCallLevel, in invokeInstData);
                    }
                }
                else
                {
                    Debug.LogVerbose(m_Context, $"[ExtractLogCall][FAIL] Failed to Extract <{logCallLevel}> Log Call: <{logCall}>\n{logCall.GetLocation()}\n");
                }
            }

            foreach (LogCallKind kind in allLevels)
            {
                UpdateInvocationListWithNewInstance(instances[kind], new LogCallData(LogCallMessageData.FixedString32(context), Array.Empty<LogCallArgumentData>()));
            }

            var dictOfList = new Dictionary<LogCallKind, List<LogCallArgumentData>>(m_ArgumentRegistryLevel.Count);
            foreach (var v in m_ArgumentRegistryLevel)
            {
                dictOfList[v.Key] = v.Value.Values.ToList();
            }

            data = new LogCallsCollection(instances, dictOfList);

            return true;
        }

        private void AnalyzeMessageStringWarnings(ContextWrapper context, LogCallKind level, in LogCallData invokeInstData)
        {
            if (level == LogCallKind.Decorate) return;

            var loc = invokeInstData.MessageData.Expression?.GetLocation();

            if (loc == null && invokeInstData.ArgumentData.Count > 0)
            {
                loc = invokeInstData.ArgumentData[0].Expression?.GetLocation();
            }

            if (loc == null)
            {
                loc = context.Compilation.SyntaxTrees.First().GetRoot().GetLocation();
            }

            var analysis = new MessageParserAnalysis(invokeInstData.MessageData.LiteralValue);

            if (analysis.Success == false)
            {
                var err = analysis.ParseRes.Errors.FirstOrDefault();
                if (err != null)
                {
                    loc = invokeInstData.MessageData.GetLocation(context, err.segment.Offset, err.segment.Length);

                    context.LogCompilerLiteralMessage(CompilerMessages.LiteralMessageGeneralError.Item1, CompilerMessages.LiteralMessageGeneralError.Item2, loc);
                }

                //
                return;
            }

            // first let's check if there are no malformed holes
            var invalidArg = analysis.Arguments.FirstOrDefault(a => a.argumentInfo.IsValid == false);
            if (invalidArg != null)
            {
                loc = invokeInstData.MessageData.GetLocation(context, invalidArg.segment.Offset, invalidArg.segment.Length);

                context.LogCompilerLiteralMessageInvalidArgument(invalidArg.ToString(), loc);
                return;
            }

            // now count check
            var parsedArgs = analysis.Arguments;

            var parsedArgsCount = parsedArgs.Count;

            if (analysis.HasNamedArgument == false)
            {
                // Log.Info("{0} {0} {0} {1}", 42, 2432);

                var args = parsedArgs.Select(a => a.argumentInfo.Index).ToImmutableHashSet();
                parsedArgsCount = args.Count;

                for (var i = 0; i < parsedArgsCount; i++)
                {
                    // Log.Info("{0} {2}", 42, 2432, 23); -- 2432 is not used, {1} was missed

                    if (args.Contains(i) == false)
                    {
                        // missed number
                        context.LogCompilerLiteralMessageMissingIndexArg(i, loc);
                        return;
                    }
                }
            }
            else
            {
                // all arguments must be unique

                var hashSetNames = new HashSet<string>();
                foreach (var arg in parsedArgs)
                {
                    var name = arg.argumentInfo.Name;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = arg.argumentInfo.Index.ToString();
                    }

                    if (hashSetNames.Add(name) == false)
                    {
                        loc = invokeInstData.MessageData.GetLocation(context, arg.segment.Offset, arg.segment.Length);
                        context.LogCompilerLiteralMessageRepeatingNamedArg(arg.argumentInfo.ToString(), loc);
                        return;
                    }
                }
            }

            if (parsedArgsCount > invokeInstData.ArgumentData.Count)
            {
                // not enough arguments for the function
                var missingArgInStr = parsedArgs[invokeInstData.ArgumentData.Count];
                loc = invokeInstData.MessageData.GetLocation(context, missingArgInStr.segment.Offset, missingArgInStr.segment.Length);

                context.LogCompilerLiteralMessageMissingArgForHole(missingArgInStr.argumentInfo.ToString(), loc);
                return;
            }

            if (parsedArgsCount < invokeInstData.ArgumentData.Count)
            {
                // too much arguments for the function
                loc = invokeInstData.ArgumentData[parsedArgsCount].Expression.GetLocation();

                context.LogCompilerLiteralMessageMissingHoleForArg(loc);
                return;
            }
        }

        private bool ExtractLogCall(ContextWrapper context, InvocationExpressionSyntax textLoggerWriteCall, LogCallKind logCallKind, out LogCallData data)
        {
            using var _ = new Profiler.Auto("LogMethodGenerator.ExtractLogCall");

            var args = textLoggerWriteCall.ArgumentList.Arguments;

            var argsCount = args.Count;

            data = new LogCallData();
            if (argsCount <= 0)
            {
                m_Context.LogCompilerError(CompilerMessages.InvalidWriteCall, textLoggerWriteCall.GetLocation());

                return false;
            }

            Profiler.Begin("GetSemanticModel");
            var textLoggerWriteCallModel = m_Context.Compilation.GetSemanticModel(textLoggerWriteCall.SyntaxTree);
            Profiler.End();

            var firstArgument = args[0];

            // Skip this invocation if failed to extract message; a specific compiler error should have already been logged
            if (!ExtractLogCallMessage(textLoggerWriteCallModel, firstArgument, out var firstArgumentType, out var msgData, out var msgOmitted))
                return false;

            // Handle special case to generate default message string, if it was omitted
            if (msgOmitted)
            {
                if (logCallKind == LogCallKind.Decorate)
                {
                    // user cannot skip 'message', because it is name of the property in this case
                    m_Context.LogCompilerErrorMissingDecoratePropertyName(firstArgument, firstArgumentType);
                    return false;
                }

                if (!GenerateDefaultMessageData(context, msgData.Symbol, argsCount, out msgData))
                    return false;
            }

            var extractedArgData = new List<LogCallArgumentData>(argsCount);

            if (logCallKind == LogCallKind.Decorate)
            {
                if (!ExtractLogDecorateCall(extractedArgData, logCallKind, args, textLoggerWriteCall, textLoggerWriteCallModel))
                    return false;
            }
            else
            {
                for (var i = (msgOmitted ? 0 : 1); i < argsCount; i++)
                {
                    // Skip this invocation if failed to extract argument(s); a specific compiler error should have already been logged
                    if (!ExtractLogCallArgumentAndRegister(textLoggerWriteCallModel, logCallKind, args[i], out var _, out var argData))
                        return false;

                    extractedArgData.Add(argData);
                }
            }

            data = new LogCallData(msgData, extractedArgData);
            return true;
        }

        private bool ExtractLogDecorateCall(List<LogCallArgumentData> extractedArgData, LogCallKind logCallKind, SeparatedSyntaxList<ArgumentSyntax> args, InvocationExpressionSyntax textLoggerWriteCall,
            SemanticModel textLoggerWriteCallModel)
        {
            // there are 2 types:
            //Log. ... .Decorate("Mandatory name", any_arg_that_can_be_ExtractLogCallArgument_ed) (2 arguments in total)
            //Log. ... .Decorate("Mandatory name", function_delegate, bool isBurstable) (3 arguments in total)

            // 0 - message
            // 1 - param / function
            // 2 - bool isBurstable

            var argsCount = args.Count;

            switch (argsCount)
            {
                case > 3:
                    // too much arguments
                    m_Context.LogCompilerErrorTooMuchDecorateArguments(args[3]);
                    return false;
                // argsCount is in [2..3] here
                case 1:
                    // only 'message' (name) argument
                    m_Context.LogCompilerErrorMissingDecorateArguments(textLoggerWriteCall.GetLocation());
                    return false;
                case 3:
                {
                    // delegate + bool.

                    // Delegate check
                    ExtractLogCallArgument(textLoggerWriteCallModel, args[1], out var arg1TypeInfo, out _);

                    // more validation is done on Unity side. Roslyn cannot get the type info of the delegate for some reason
                    if (arg1TypeInfo.Type?.SpecialType == SpecialType.System_Void)
                    {
                        m_Context.LogCompilerErrorVoidType(args[1].GetLocation());
                    }

                    // Checking that 3rd is bool
                    ExtractLogCallArgument(textLoggerWriteCallModel, args[2], out var arg2TypeInfo, out _);

                    if (arg2TypeInfo.Type?.SpecialType != SpecialType.System_Boolean)
                    {
                        m_Context.LogCompilerErrorExpectedBoolIn3rdDecorateArgument(args[2], arg2TypeInfo);
                    }

                    // Generator will generate this call anyway, so we're not registering this call
                    return false;
                }
                default:
                {
                    // argsCount is 2 in here
                    // this argument is the same one as in Log.Info(message, THIS_ONE)
                    // so we need to extract and register it, so it can be merged with other calls like that afterwards

                    if (!ExtractLogCallArgumentAndRegister(textLoggerWriteCallModel, logCallKind, args[1], out _, out var argDecorateData))
                        return false;

                    extractedArgData.Add(argDecorateData);

                    // only this kind of call we need to register and return true for
                    return true;
                }
            }
        }

        private bool ExtractLogCallMessage(SemanticModel textLoggerWriteCallModel, ArgumentSyntax arg, out TypeInfo argTypeInfo, out LogCallMessageData data, out bool messageOmitted)
        {
            using var _ = new Profiler.Auto("LogMethodGenerator.ExtractLogCallMessage");

            messageOmitted = false;

            Profiler.Begin("model.GetTypeInfo");

            var expression = arg.Expression;
            argTypeInfo = textLoggerWriteCallModel.GetTypeInfo(expression);

            Profiler.End();

            (data, messageOmitted) = MessageTypeExtractor.Extract(m_Context, expression, argTypeInfo);

            return data.IsValid;
        }

        private LogCallArgumentData ExtractLogCallArgument(SemanticModel model, ArgumentSyntax arg, out TypeInfo typeInfo, out string qualifiedName)
        {
            using var _ = new Profiler.Auto("LogMethodGenerator.ExtractLogCallArgument");

            Profiler.Begin("model.GetTypeInfo");

            var expression = arg.Expression;
            typeInfo = model.GetTypeInfo(expression);

            Profiler.End();

            var userOverload = GetUserOverload(typeInfo);
            if (userOverload.IsCreated)
            {
                return LogCallArgumentData.UserDefinedType(typeInfo, arg, userOverload, out qualifiedName);
            }

            return ArgumentTypeExtractor.Extract(m_Context, expression, typeInfo, out qualifiedName);
        }

        private CustomMirrorStruct GetUserOverload(TypeInfo typeInfo)
        {
            foreach (var userType in m_UserTypes)
            {
                if (userType.OriginalStructTypeInfo.Type != null && userType.OriginalStructTypeInfo.Type.Equals(typeInfo.Type, SymbolEqualityComparer.Default))
                    return userType;
                if (userType.WrapperStructTypeInfo != null && userType.WrapperStructTypeInfo.Equals(typeInfo.Type, SymbolEqualityComparer.Default))
                    return userType;
            }
            return default;
        }

        private bool ExtractLogCallArgumentAndRegister(SemanticModel model, LogCallKind callKind, ArgumentSyntax arg, out TypeInfo argTypeInfo, out LogCallArgumentData data)
        {
            using var _ = new Profiler.Auto("LogMethodGenerator.ExtractLogCallArgumentAndRegister");

            data = ExtractLogCallArgument(model, arg, out argTypeInfo, out var qualifiedName);

            // Register the argument data for this typename
            if (data.IsValid && string.IsNullOrEmpty(qualifiedName) == false)
            {
                var registry = GetArgumentRegistry(callKind);
                if (registry.ContainsKey(qualifiedName) == false)
                    registry.Add(qualifiedName, data);
            }

            return data.IsValid;
        }

        private bool GenerateDefaultMessageData(ContextWrapper context, ITypeSymbol typeSymbol, int numArgs, out LogCallMessageData data)
        {
            using var _ = new Profiler.Auto("LogMethodGenerator.GenerateDefaultMessageData");

            // Create a message string, using the logging API syntax, to output structure parameters as-is with no other message

            var sb = new StringBuilder(numArgs * 4);
            for (var i = 0; i < numArgs; i++)
            {
                sb.Append('{').Append(i).Append('}');
                if (i != numArgs - 1)
                    sb.Append(' ');
            }
            var message = sb.ToString();

            var msgType = FixedStringUtils.GetSmallestFixedStringTypeForMessage(message, m_Context);

            if (msgType.IsValid)
            {
                var strType = context.Compilation.GetTypeByMetadataName("System.String");
                data = LogCallMessageData.OmittedLiteralAsFixedString(strType, message);
            }
            else
            {
                m_Context.LogCompilerError(CompilerMessages.MessageLengthError);
                data = default;
            }

            return data.IsValid;
        }

        public static bool IsValidFixedStringType(ContextWrapper m_Context, ITypeSymbol symbol, out FixedStringUtils.FSType fsType)
        {
            // Check if this type is a valid FixedString type; perform some extra error checking if not.
            fsType = FixedStringUtils.GetFSType(symbol.Name);
            if (!fsType.IsValid)
            {
                if (symbol.Name.StartsWith("FixedString"))
                {
                    var inNamespace = symbol.ContainingNamespace;
                    if (Common.GetFullyQualifiedNameSpaceFromNamespaceSymbol(inNamespace) != "Unity.Collections")
                    {
                        // Must be a Unity FixedString type
                        m_Context.LogCompilerError(CompilerMessages.UnsupportedFixedStringType);
                    }
                    else
                    {
                        // Must match one of the known variation types, e.g. FixedString32, FixedString64, etc.
                        m_Context.LogCompilerError(CompilerMessages.UnknownFixedStringType);
                    }
                    return false;
                }
                return false;
            }

            return true;
        }

        bool MergeLogCallData(ref LogCallData newCall, ref LogCallData oldCall)
        {
            var newMessage = newCall.MessageData;
            var oldMessage = oldCall.MessageData;

            var ableToMerge = MergeTypes(ref newMessage, ref oldMessage);
            if (ableToMerge == false)
            {
                return false;
            }

            var mergedArguments = MergeArguments(newCall.ArgumentData, oldCall.ArgumentData);
            var needToReplaceOldCall = mergedArguments != null;

            if (needToReplaceOldCall)
            {
                // oldCall will be deleted
                newCall = new LogCallData(newMessage, mergedArguments);
            }
            else
            {
                // update newCall and oldCall with common message type
                oldCall = new LogCallData(oldMessage, oldCall.ArgumentData);
                newCall = new LogCallData(newMessage, newCall.ArgumentData);
            }

            return needToReplaceOldCall;
        }

        private List<LogCallArgumentData> MergeArguments(List<LogCallArgumentData> newArgumentData, List<LogCallArgumentData> oldArgumentData)
        {
            if (newArgumentData.Count != oldArgumentData.Count)
                return null;

            var mergedArguments = new List<LogCallArgumentData>(newArgumentData);
            for (var i = 0; i < mergedArguments.Count; i++)
            {
                var arg = MergeTypes(mergedArguments[i], oldArgumentData[i]);

                if (arg.IsValid == false)
                    return null;

                mergedArguments[i] = arg;
            }

            return mergedArguments;
        }

        private bool MergeTypes(ref LogCallMessageData newMessageData, ref LogCallMessageData oldMessageData)
        {
            if (newMessageData.Omitted == oldMessageData.Omitted && newMessageData.MessageType == oldMessageData.MessageType)
                return true;

            if (newMessageData.FixedStringType.IsValid && oldMessageData.FixedStringType.IsValid)
            {
                if (newMessageData.FixedStringType.MaxLength > oldMessageData.FixedStringType.MaxLength)
                    oldMessageData = newMessageData;
                else
                    newMessageData = oldMessageData;
                return true;
            }

            return false;
        }

        private LogCallArgumentData MergeTypes(LogCallArgumentData newArgumentData, LogCallArgumentData oldArgumentData)
        {
            if (newArgumentData.FullGeneratedTypeName == oldArgumentData.FullGeneratedTypeName)
                return oldArgumentData;

            if (newArgumentData.FixedStringType.IsValid && oldArgumentData.FixedStringType.IsValid)
            {
                return newArgumentData.FixedStringType.MaxLength > oldArgumentData.FixedStringType.MaxLength ? newArgumentData : oldArgumentData;
            }

            return default;
        }

        private void UpdateInvocationListWithNewInstance(List<LogCallData> currInvocations, LogCallData newInst)
        {
            using var _ = new Profiler.Auto("LogMethodGenerator.UpdateInvocationListWithNewInstance");

            // Check if we already have an Invocation instance that matches newInst's signature, and if so don't add the new invocation record
            if (currInvocations.Any(a => a.Equals(newInst)))
            {
                return;
            }

            // find all the 'same' calls and merge into one

            var newArgs = newInst.ArgumentData.Count;

            var instToAdd = newInst;

            for (var i = currInvocations.Count - 1; i >= 0; i--)
            {
                var currInst = currInvocations[i];
                if (newArgs != currInst.ArgumentData.Count) continue;

                var needToDeleteCurrInst = MergeLogCallData(ref instToAdd, ref currInst);

                if (needToDeleteCurrInst)
                {
                    currInvocations.RemoveAt(i);
                }
                else
                {
                    currInvocations[i] = currInst;
                }
            }

            currInvocations.Add(instToAdd);
        }

        ArgumentRegistry GetArgumentRegistry(LogCallKind callKind)
        {
            if (m_ArgumentRegistryLevel.TryGetValue(callKind, out var result) == false)
            {
                result = new ArgumentRegistry();
                m_ArgumentRegistryLevel[callKind] = result;
            }
            return result;
        }

        private ContextWrapper                       m_Context;
        private readonly Dictionary<LogCallKind, ArgumentRegistry> m_ArgumentRegistryLevel = new();
        public ImmutableArray<CustomMirrorStruct> m_UserTypes;
    }
}
