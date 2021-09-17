using System.Collections.Generic;
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

        public static bool Execute(in GeneratorExecutionContext context, ulong assemblyHash, out LogCallsCollection invokeData, out StringBuilder generatedCode)
        {
            using var _ = new Profiler.Auto("LogMethodGenerator.Execute");

            invokeData = new LogCallsCollection();
            generatedCode = new StringBuilder();

            var generator = new LogMethodGenerator
            {
                m_Context = context
            };

            if (!generator.ExtractLogInvocationData(out var data))
                return false;

            generatedCode = LogMethodEmitter.Emit(context, data, assemblyHash);
            invokeData = data;
            return true;
        }

        private bool ExtractLogInvocationData(out LogCallsCollection data)
        {
            using var _ = new Profiler.Auto("LogMethodGenerator.ExtractLogInvocationData");

            data = new LogCallsCollection();

            // Get all the instances of calls to Log.Info from the syntax processor
            var syntaxReceiver = (LogCallFinder)m_Context.SyntaxReceiver;
            if (syntaxReceiver == null || syntaxReceiver.LogCalls.Count <= 0)
            {
                if (syntaxReceiver == null)
                    Debug.LogVerbose(m_Context, $"[ExtractLogCall][FAIL] syntaxReceiver == null");
                else
                    Debug.LogVerbose(m_Context, $"[ExtractLogCall] syntaxReceiver.LogCalls.Count = {syntaxReceiver.LogCalls.Count}");
                return false;
            }

            var instances = new Dictionary<LogCallKind, List<LogCallData>>();
            for (var i = 0; i < syntaxReceiver.LogCalls.Count; i++)
            {
                var logCall = syntaxReceiver.LogCalls[i];
                var logCallLevel = syntaxReceiver.LogCallsLevel[i];

                if (ExtractLogCall(logCall, logCallLevel, out var invokeInstData))
                {
                    Debug.LogVerbose(m_Context, $"[ExtractLogCall] Extracted <{logCallLevel}> Log Call: <{logCall}>\n{logCall.GetLocation()}\n");

                    // Check if this is a new invocation and if so add it to the list
                    if (instances.TryGetValue(logCallLevel, out var ins) == false)
                    {
                        ins = new List<LogCallData>(32);
                        instances[logCallLevel] = ins;
                    }

                    UpdateInvocationListWithNewInstance(ins, invokeInstData);
                }
                else
                {
                    Debug.LogVerbose(m_Context, $"[ExtractLogCall][FAIL] Failed to Extract <{logCallLevel}> Log Call: <{logCall}>\n{logCall.GetLocation()}\n");
                }
            }

            var total = instances.Sum(ins => ins.Value.Count);
            if (total > 0)
            {
                var dictOfList = new Dictionary<LogCallKind, List<LogCallArgumentData>>(m_ArgumentRegistryLevel.Count);
                foreach (var v in m_ArgumentRegistryLevel)
                {
                    dictOfList[v.Key] = v.Value.Values.ToList();
                }

                data = new LogCallsCollection(instances, dictOfList);
            }

            return data.IsValid;
        }

        private bool ExtractLogCall(InvocationExpressionSyntax textLoggerWriteCall, LogCallKind logCallKind, out LogCallData data)
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

                if (!GenerateDefaultMessageData(msgData.Symbol, argsCount, out msgData))
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

            return ArgumentTypeExtractor.Extract(m_Context, expression, typeInfo, out qualifiedName);
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

        private bool GenerateDefaultMessageData(ITypeSymbol typeSymbol, int numArgs, out LogCallMessageData data)
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
                data = LogCallMessageData.LiteralAsFixedString(typeSymbol, null, msgType, message);
            }
            else
            {
                m_Context.LogCompilerError(CompilerMessages.MessageLengthError);
                data = default;
            }

            return data.IsValid;
        }

        public static bool IsValidFixedStringType(GeneratorExecutionContext m_Context, ITypeSymbol symbol, out FixedStringUtils.FSType fsType)
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
            var mergedMessageType = MergeTypes(newCall.MessageData, oldCall.MessageData);
            if (mergedMessageType.IsValid == false)
            {
                // messages are not merged
                return false;
            }

            var mergedArguments = MergeArguments(newCall.ArgumentData, oldCall.ArgumentData);
            var needToReplaceOldCall = mergedArguments != null;

            if (needToReplaceOldCall)
            {
                // oldCall will be deleted
                newCall = new LogCallData(mergedMessageType, mergedArguments);
            }
            else
            {
                // update newCall and oldCall with common message type
                oldCall = new LogCallData(mergedMessageType, oldCall.ArgumentData);
                newCall = new LogCallData(mergedMessageType, newCall.ArgumentData);
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

        private LogCallMessageData MergeTypes(LogCallMessageData newMessageData, LogCallMessageData oldMessageData)
        {
            if (newMessageData.MessageType == oldMessageData.MessageType)
                return oldMessageData;

            if (newMessageData.FixedStringType.IsValid && oldMessageData.FixedStringType.IsValid)
            {
                return newMessageData.FixedStringType.MaxLength > oldMessageData.FixedStringType.MaxLength ? newMessageData : oldMessageData;
            }

            return default;
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

            if (currInvocations.FirstOrDefault(i => i.Equals(newInst)).IsValid)
                return;

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

        private GeneratorExecutionContext                       m_Context;
        private readonly Dictionary<LogCallKind, ArgumentRegistry> m_ArgumentRegistryLevel = new();
    }
}
