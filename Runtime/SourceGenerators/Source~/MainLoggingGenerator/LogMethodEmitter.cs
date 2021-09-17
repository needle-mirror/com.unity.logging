using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    class LogMethodEmitter
    {
        private LogMethodEmitter()
        {
        }

        public static StringBuilder Emit(in GeneratorExecutionContext context, in LogCallsCollection invokeData, ulong assemblyHash)
        {
            var emitter = new LogMethodEmitter
            {
                m_InvokeData = invokeData,
            };

            var sb = new StringBuilder();

            sb.Append(EmitStrings.SourceFileHeader);
            sb.AppendFormat(EmitStrings.TextLoggerDefinitionEnclosure, emitter.EmitLogMethodDefinitions(), $"{assemblyHash:X4}");
            sb.AppendLine(EmitStrings.SourceFileFooter);

            return sb;
        }

        private StringBuilder EmitLogMethodDefinitions()
        {
            var sb = new StringBuilder();

            var uniqHashSet = new HashSet<string>();

            foreach (var levelPair in m_InvokeData.InvokeInstances)
            {
                var level = levelPair.Key;

                if (level == LogCallKind.Decorate)
                {
                    EmitLogDecorateMethodDefinitions(sb, uniqHashSet, levelPair.Value);
                }
                else
                {
                    foreach (var currMethod in levelPair.Value)
                    {
                        sb.AppendLine($"\n    /* {currMethod.ToString().Replace("*/", "* /")} */");

                        var paramList = EmitLogMethodParameterList(currMethod).ToString();
                        var paramCallList = EmitLogMethodParameterListCall(currMethod);

                        if (currMethod.IsBurstable)
                        {
                            var blittableParamList = EmitLogMethodParameterList(currMethod, blittableOnly: true).ToString();
                            var uniqPostfix = GenerateUniqPostfix(paramList, currMethod, uniqHashSet);

                            var castCode = new StringBuilder();

                            for (var i = 0; i < currMethod.ArgumentData.Count; i++)
                            {
                                var arg = currMethod.ArgumentData[i];
                                var castString = arg.GetCastCode(i);
                                if (string.IsNullOrEmpty(castString) == false)
                                    castCode.AppendLine(castString);
                            }

                            sb.AppendFormat(EmitStrings.LogCallMethodDefinitionBursted,
                                paramList,
                                EmitLogBuilders(currMethod, level),
                                uniqPostfix,
                                paramCallList,
                                level,
                                blittableParamList,
                                castCode);
                        }
                        else
                        {
                            sb.AppendFormat(EmitStrings.LogCallMethodDefinitionNotBursted,
                                paramList,
                                EmitLogBuilders(currMethod, level),
                                level,
                                paramCallList);
                        }
                    }
                }
            }

            return sb;
        }

        private void EmitLogDecorateMethodDefinitions(StringBuilder sb, HashSet<string> uniqHashSet, List<LogCallData> decorators)
        {
            foreach (var currMethod in decorators)
            {
                sb.AppendLine($"\n    /* {currMethod.ToString().Replace("*/", "* /")} */");

                var paramList = EmitLogMethodParameterList(currMethod).ToString();
                var paramCallList = EmitLogMethodParameterListCall(currMethod);

                if (currMethod.IsBurstable)
                {
                    var blittableParamList = EmitLogMethodParameterList(currMethod, blittableOnly: true).ToString();
                    var uniqPostfix = GenerateUniqPostfix(paramList, currMethod, uniqHashSet);

                    var castCode = new StringBuilder();

                    for (var i = 0; i < currMethod.ArgumentData.Count; i++)
                    {
                        var arg = currMethod.ArgumentData[i];
                        var castString = arg.GetCastCode(i);
                        if (string.IsNullOrEmpty(castString) == false)
                            castCode.AppendLine(castString);
                    }

                    sb.AppendFormat(EmitStrings.LogCallDecorateMethodDefinitionBursted,
                        paramList,
                        EmitLogHandles(currMethod),
                        uniqPostfix,
                        paramCallList,
                        blittableParamList,
                        castCode);
                }
                else
                {
                    sb.AppendFormat(EmitStrings.LogCallDecorateMethodDefinitionNotBursted,
                        paramList,
                        EmitLogHandles(currMethod),
                        paramCallList);
                }
            }
        }

        private string GenerateUniqPostfix(string paramList, LogCallData currMethod, HashSet<string> uniqHashSet)
        {
            var uniqPostfix = Common.CreateMD5String(paramList);

            for (var guard = 0; guard < 100; ++guard)
            {
                if (uniqHashSet.Add(uniqPostfix))
                    return uniqPostfix;

                uniqPostfix = Common.CreateMD5String(uniqPostfix + uniqHashSet.Count + currMethod.GetHashCode());
            }

            throw new Exception("Something is wrong with GenerateUniqPostfix - unable to generate unique string in 100 tries");
        }

        // Parameters visible to user
        private static StringBuilder EmitLogMethodParameterList(in LogCallData currInstance, bool blittableOnly = false)
        {
            var sb = new StringBuilder();

            // First comes the message parameter (this is always required)

            var msgType = currInstance.MessageData.GetParameterTypeForUser();

            sb.Append($"in {msgType} msg");

            for (var i = 0; i < currInstance.ArgumentData.Count; i++)
            {
                var arg = currInstance.ArgumentData[i];
                var argType = arg.GetParameterTypeForUser(blittableOnly, i);
                sb.Append($", in {argType}");
            }

            return sb;
        }

        // User visible function calls internal with this parameter list
        private static StringBuilder EmitLogMethodParameterListCall(in LogCallData currInstance)
        {
            var sb = new StringBuilder();

            // First comes the message parameter (this is always required)
            sb.Append("msg");

            for (var i = 0; i < currInstance.ArgumentData.Count; i++)
            {
                var arg = currInstance.ArgumentData[i];
                sb.Append($", {arg.GetInvocationParam(i)}");
            }

            return sb;
        }

        private static StringBuilder EmitLogHandles(in LogCallData currInstance)
        {
            var sbHandles = new StringBuilder();
            for (var i = 0; i < currInstance.ArgumentData.Count; i++)
            {
                if (currInstance.ArgumentData[i].IsSpecialSerializableType())
                    sbHandles.AppendFormat(EmitStrings.LogBuilderHandleSpecialTypeAllocation, i);
                else
                    sbHandles.AppendFormat(EmitStrings.LogBuilderHandleAllocation, i);
            }
            return sbHandles;
        }

        private static string EmitLogBuilders(in LogCallData currInstance, LogCallKind callKind)
        {
            var sbHandles = EmitLogHandles(currInstance);

            var fixedListSize = currInstance.ArgumentData.Count < 512 ? 512 : 4096;

            return string.Format(EmitStrings.LogBuilderInvocationSetup, fixedListSize, sbHandles, callKind);
        }

        private LogCallsCollection     m_InvokeData;
    }
}
