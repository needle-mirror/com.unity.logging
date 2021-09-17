using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text;
using LoggingCommon;

namespace SourceGenerator.Logging
{
    [Generator]
    public class LoggingGenerator : ISourceGenerator
    {
        public LogCallsCollection invokeData;
        public LogStructureTypesData structureData;

        public StringBuilder methodsGenCode;
        public StringBuilder parserGenCode;
        public StringBuilder typesGenCode;

        public bool WriteFilesToDisk = true;

        /// <summary>
        /// This method is used to initialize all the SyntaxNodes we want to
        /// capture for all our various generators that we will run in serial.
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(GeneratorInitializationContext context)
        {
            //DebuggerUtils.Launch();

            Profiler.Initialize();

            // Register our syntax receiver
            context.RegisterForSyntaxNotifications(() => new LogCallFinder());
        }

        private void WaitForDebuggerIfRequested(GeneratorExecutionContext context)
        {
            string assembly = Environment.GetEnvironmentVariable("UNITY_LOGGING_DEBUG_ASSEMBLY");
            if (!string.IsNullOrEmpty(assembly) && context.Compilation.AssemblyName == assembly)
                DebuggerUtils.Launch($"(Assembly {context.Compilation.AssemblyName})");
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                WaitForDebuggerIfRequested(context);

                if (context.CancellationToken.IsCancellationRequested)
                    return;

                lock (this)
                {
                    using var _ = new Profiler.Auto("LoggingGenerator.Execute");
                    var assemblyHash = GetAssemblyHash(context);

                    context.LogInfoMessage("Begin Processing assembly " + context.Compilation.AssemblyName + " hash #" + assemblyHash);

                    var methodsFileGenerate = LogMethodGenerator.Execute(context, assemblyHash, out invokeData, out methodsGenCode);
                    var typesFileGenerate = LogTypesGenerator.Execute(context, assemblyHash, invokeData, out structureData, out typesGenCode, out parserGenCode);

                    if (WriteFilesToDisk)
                    {
                        if (methodsFileGenerate)
                        {
                            LogWriteOutput.SourceGenMethodsFile(context, methodsGenCode);
                        }

                        if (typesFileGenerate)
                        {
                            LogWriteOutput.SourceGenTypesFile(context, typesGenCode);
                            LogWriteOutput.SourceGenParserFile(context, parserGenCode);
                        }

                        if (methodsFileGenerate || typesFileGenerate)
                        {
                            LogWriteOutput.AsmrefFile(context);
                        }
                    }


                    context.LogInfoMessage($"End Processing assembly {context.Compilation.AssemblyName}");
                }

                context.LogInfoMessage(Profiler.PrintStats());

            }
            catch (Exception e)
            {
                context.LogCompilerErrorUnhandledException(e);
            }
        }

        private ulong m_AssemblyHash;
        private ulong GetAssemblyHash(GeneratorExecutionContext context)
        {
            if (m_AssemblyHash != 0)
                return m_AssemblyHash;

            var guidByte = Guid.NewGuid().ToByteArray();
            unchecked
            {
                var hashInt1 = System.BitConverter.ToUInt64(guidByte, 0);
                var hashInt2 = System.BitConverter.ToUInt64(guidByte, 8);

                m_AssemblyHash = 17;
                m_AssemblyHash = m_AssemblyHash * 31 + hashInt1;
                m_AssemblyHash = m_AssemblyHash * 31 + hashInt2;
                return m_AssemblyHash;
            }
        }
    }

    public class LogCallFinder : ISyntaxReceiver
    {
        private const string AsmName = "Unity.Logging";

        public readonly List<InvocationExpressionSyntax> LogCalls = new();
        public readonly List<LogCallKind> LogCallsLevel = new();

        private readonly List<string> m_PackageUsingAlias = new();
        private bool m_HasUsingUnityLogging;

        private static readonly Dictionary<string, LogCallKind> LogCallKinds = new()
        {
            {"Verbose", LogCallKind.Verbose},
            {"Debug", LogCallKind.Debug},
            {"Info", LogCallKind.Info},
            {"Warning", LogCallKind.Warning},
            {"Error", LogCallKind.Error},
            {"Fatal", LogCallKind.Fatal},

            {"Decorate", LogCallKind.Decorate}
        };

        private static bool s_Called;

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            using var _ = new Profiler.Auto("OnVisitSyntaxNode");

            if (s_Called == false)
            {
                //DebuggerUtils.Launch($"Syntax analysis for {syntaxNode.GetLocation().ToString()}");
                s_Called = true;
            }

            GetTextLoggerSyntaxNodes(syntaxNode);
        }

        private void GetTextLoggerSyntaxNodes(SyntaxNode node)
        {
            using var _ = new Profiler.Auto("GetTextLoggerSyntaxNodes");

            if (node is UsingDirectiveSyntax uds)
            {
                if (uds.Name.ToString() == AsmName)
                {
                    if (uds.Alias != null)
                        RegisterAlias(uds.Alias.Name.ToString());
                    else
                        RegisterUsing();
                }
            }

            if (node is InvocationExpressionSyntax ies)
            {
                var s = ies.Expression.ToString();

                // check prefix: 'Log.'
                if (CheckPrefix(ies, s))
                {
                    if (CallMatch(ies, out var level))
                    {
                        LogCalls.Add(ies);
                        LogCallsLevel.Add(level);
                    }
                }
            }
        }

        string ExtractFullNamespace(InvocationExpressionSyntax ies)
        {
            var fullNamespace = "";
            for (var parent = ies.Parent; parent != null; parent = parent.Parent)
            {
                if (parent is NamespaceDeclarationSyntax a)
                {
                    if (string.IsNullOrEmpty(fullNamespace))
                        fullNamespace = a.Name.ToString();
                    else
                        fullNamespace = a.Name + "." + fullNamespace;
                }
            }
            return fullNamespace;
        }

        private bool CheckPrefix(InvocationExpressionSyntax ies, string logCall)
        {
            var startsWithLog = logCall.StartsWith("Log.");
            var someLogMention = startsWithLog || logCall.Contains(".Log.");

            // no Log. in the expression - not our case for sure
            if (someLogMention == false)
                return false;

            // has using Unity.Logging and call starts with 'Log.'
            if (m_HasUsingUnityLogging && startsWithLog)
                return true;

            // full Unity.Logging.Log. call
            if (logCall.StartsWith(AsmName + ".Log."))
                return true;

            // alias check
            if (m_PackageUsingAlias.Any(aliases => logCall.StartsWith(aliases + ".Log.")))
                return true;

            // namespace Unity.Logging { something() { Log. call } }
            var fullNamespace = ExtractFullNamespace(ies);

            if (fullNamespace.StartsWith(AsmName))
                return true;

            return false;
        }

        private static bool CallMatch(InvocationExpressionSyntax ies, out LogCallKind lvl)
        {
            lvl = LogCallKind.Verbose;

            if (ies.Expression is MemberAccessExpressionSyntax mae)
            {
                // check that this is valid name
                if (LogCallKinds.TryGetValue(mae.Name.ToString(), out lvl))
                {
                    return true;
                }
            }

            return false;
        }

        private void RegisterAlias(string aliasName)
        {
            m_PackageUsingAlias.Add(aliasName);
        }

        private void RegisterUsing()
        {
            m_HasUsingUnityLogging = true;
        }
    }
}
