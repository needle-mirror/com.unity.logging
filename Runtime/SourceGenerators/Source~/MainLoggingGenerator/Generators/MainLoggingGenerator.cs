using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text;
using LoggingCommon;
using MainLoggingGenerator.Generators;
using Microsoft.CodeAnalysis.Text;
using SourceGenerator.Logging;
using SourceGenerator.Logging.Declarations;

// namespace + class is used to display messages in Unity
[Generator]
public class LoggingSourceGenerator : IIncrementalGenerator
                                      //ISourceGenerator
{
    public LogCallsCollection invokeData;
    public LogStructureTypesData structureData;

    public string methodsGenCode;
    public StringBuilder parserGenCode;
    public StringBuilder typesGenCode;
    public string userTypesGenCode;

    private static void WaitForDebuggerIfRequested(Compilation compilation)
    {
        string assembly = Environment.GetEnvironmentVariable("UNITY_LOGGING_DEBUG_ASSEMBLY");
        if (!string.IsNullOrEmpty(assembly) && compilation.AssemblyName == assembly)
            DebuggerUtils.Launch($"(Assembly {compilation.AssemblyName})");
    }

    static ulong GetAssemblyHash(Compilation compilation)
    {
        if (compilation.AssemblyName != null) return Common.CreateStableHashCodeFromString(compilation.AssemblyName);

        return 0;
    }

    const string LoggingAssemblyName = "Unity.Logging";

    static bool WantToProcessAssembly(Compilation compilation)
    {
        using var _s = new Profiler.Auto("CompilationProvider");

        if (compilation.AssemblyName == LoggingAssemblyName)
            return false;

        var runningTests = compilation.AssemblyName == "SourceGeneratorTestsCompilation";
        if (runningTests)
            return true;

        foreach (var refId in compilation.ReferencedAssemblyNames)
        {
            if (refId.Name == LoggingAssemblyName)
                return true;
        }
        return false;
    }

    static bool ContainsPotentialLogCall(string call)
    {
        // starts with 'Log.' or contains '.Log'
        var indexOf = call.IndexOf("Log.", StringComparison.Ordinal);
        if (indexOf > 0)
            return call[indexOf - 1] == '.';
        return indexOf == 0;
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        Profiler.Initialize();
        using var _ = new Profiler.Auto("Initialize");

        var wantToProcessAssembly = context.CompilationProvider.Select(static (compilation, _) => WantToProcessAssembly(compilation));

        var usings = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is UsingDirectiveSyntax usingDir && usingDir.Name.ToString() == LoggingAssemblyName,
            static (syntaxContext, _) => new UsingDirStruct((UsingDirectiveSyntax)syntaxContext.Node)).Collect();

        var logs = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax inv && ContainsPotentialLogCall(inv.ToString()),
                static (syntaxContext, _) => new LogStruct((InvocationExpressionSyntax)syntaxContext.Node))
            .Where(static log => log.Valid);

        var filteredLogs = logs.Combine(usings).Select(static (tuple, _) =>
        {
            var usingStats = new UsingStats(tuple.Right);
            var log = tuple.Left;
            return log.Validate(usingStats) ? log : default;
        }).Where(static l => l.Valid)
          .Collect();

        var customMirrorStructs = context.SyntaxProvider.CreateSyntaxProvider(
              static (node, _) =>
              {
                  if (node is BaseListSyntax baseList)
                  {
                      foreach (var type in baseList.Types)
                      {
                          var gen = CustomMirrorStruct.ExtractGenericSyntax(type);
                          if (gen != null && gen.Identifier.Text == CustomMirrorStruct.InterfaceName)
                              return true;
                      }
                  }
                  return false;
              },
              static (syntaxContext, _) => new CustomMirrorStruct((BaseListSyntax)syntaxContext.Node, syntaxContext.SemanticModel, GetAssemblyHash(syntaxContext.SemanticModel.Compilation)));

        var customMirrorStructsWithErrors = customMirrorStructs.Where(s => s.Status != CustomMirrorStruct.ErrorStatus.NoError).Collect();
        var customMirrorStructsNoErrors = customMirrorStructs.Where(s => s.Status == CustomMirrorStruct.ErrorStatus.NoError).Collect();

        context.RegisterSourceOutput(wantToProcessAssembly.Combine(customMirrorStructsWithErrors), static (prodContext, source) =>
        {
            var isInterested = source.Left;
            if (isInterested == false) return;

            var userStructsWithErrors = source.Right;
            foreach (var userMirrorStruct in userStructsWithErrors)
            {
                prodContext.ReportDiagnostic(userMirrorStruct.CreateDiagnostics());
            }
        });

        context.RegisterSourceOutput(wantToProcessAssembly.Combine(context.CompilationProvider.Combine(customMirrorStructsNoErrors)),
            (prodContext, source) =>
        {
            using var _reg = new Profiler.Auto("RegisterSourceOutput User Mirror Types");

            var compilation = source.Right.Left;
            WaitForDebuggerIfRequested(compilation);

            var isInterested = source.Left;
            if (isInterested == false) return;

            var asmName = compilation.AssemblyName ?? "Unknown_assembly";

            var structData = source.Right.Right;
            if (structData.Length == 0) return;

            var namespaceList = structData.Select(x => x.ContainingNamespace).Distinct().ToImmutableArray();
            var includesHashSet = new HashSet<string>(namespaceList);
            includesHashSet.UnionWith(EmitStrings.StdIncludes);

            static StringBuilder EmitUserStructs(ImmutableArray<CustomMirrorStruct> mirrorStructs)
            {
                var sb = new StringBuilder();

                foreach (var mirror in mirrorStructs)
                {
                    sb.Append($@"
namespace {mirror.ContainingNamespace}
{{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    partial struct {mirror.WrapperStructureName}
    {{
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct {CustomMirrorStruct.HeaderTypeName}
        {{
            public ulong TypeId;
            public static {CustomMirrorStruct.HeaderTypeName} Create()
            {{
                return new {CustomMirrorStruct.HeaderTypeName} {{ TypeId = TypeIdConst }};
            }}
        }}

        public const ulong TypeIdConst = {mirror.TypeId};

        static void StructureMustBeUnmanagedCheck()
        {{
            static void StructureMustBeUnmanaged<T>() where T : unmanaged {{}}
            static void StructureMustBeStructure<T>() where T : struct {{}}
            StructureMustBeUnmanaged<{mirror.WrapperStructureName}>();
            StructureMustBeStructure<{mirror.WrapperStructureName}>();
        }}
    }}
}}");
                }

                return sb;
            }

            userTypesGenCode = $@"{EmitStrings.SourceFileHeader}
{EmitStrings.GenerateIncludeHeader(includesHashSet)}

{EmitUserStructs(structData)}

{EmitStrings.SourceFileFooter}";

            {
                var filename = SourceGenerator.Logging.Declarations.OutputPaths.SourceGenTextLoggerUserTypesFileName;
                filename = Path.GetFileNameWithoutExtension(filename);
                prodContext.AddSource($"{asmName}_{filename}", SourceText.From(userTypesGenCode, Encoding.UTF8));
            }
        });

        context.RegisterSourceOutput(
            wantToProcessAssembly.Combine(filteredLogs).Combine(context.CompilationProvider.Combine(customMirrorStructsNoErrors)),
            (prodContext, source) =>
            {
                using var _reg = new Profiler.Auto("RegisterSourceOutput");

                var compilation = source.Right.Left;

                WaitForDebuggerIfRequested(compilation);

                var isInterested = source.Left.Left;
                if (isInterested == false) return;

                var asmName = compilation.AssemblyName ?? "Unknown_assembly";

                ImmutableArray<LogStruct> filteredLogCalls = source.Left.Right;

                var assemblyHash = GetAssemblyHash(compilation);

                var ctx = new ContextWrapper(prodContext, compilation, new LogCallFinder(filteredLogCalls));

                var userDefinedTypes = source.Right.Right;

                ProcessAssembly(ctx, asmName, assemblyHash, userDefinedTypes);
            });
    }

    private void ProcessAssembly(ContextWrapper ctx, string asmName, ulong assemblyHash, ImmutableArray<CustomMirrorStruct> userDefinedTypes)
    {
        //ctx.LogInfoMessage("Begin Processing assembly " + ctx.Compilation.AssemblyName + " hash #" + assemblyHash);

        var methodsFileGenerate = LogMethodGenerator.Execute(ctx, assemblyHash, userDefinedTypes, out invokeData, out methodsGenCode);
        var typesFileGenerate = LogTypesGenerator.Execute(ctx, assemblyHash, invokeData, out structureData, out typesGenCode, out parserGenCode);

        if (methodsFileGenerate)
        {
            var filename = SourceGenerator.Logging.Declarations.OutputPaths.SourceGenTextLoggerMethodsFileName;
            filename = Path.GetFileNameWithoutExtension(filename);
            ctx.AddSource($"{asmName}_{filename}", SourceText.From(methodsGenCode, Encoding.UTF8));
        }

        if (typesFileGenerate)
        {
            {
                var filename = SourceGenerator.Logging.Declarations.OutputPaths.SourceGenTextLoggerTypesFileName;
                filename = Path.GetFileNameWithoutExtension(filename);
                ctx.AddSource($"{asmName}_{filename}", SourceText.From(typesGenCode.ToString(), Encoding.UTF8));
            }
            {
                var filename = SourceGenerator.Logging.Declarations.OutputPaths.SourceGenTextLoggerParserFileName;
                filename = Path.GetFileNameWithoutExtension(filename);
                ctx.AddSource($"{asmName}_{filename}", SourceText.From(parserGenCode.ToString(), Encoding.UTF8));
            }
        }

        //ctx.LogInfoMessage("End Processing assembly " + ctx.Compilation.AssemblyName + " hash #" + assemblyHash);

        //ctx.LogInfoMessage(Profiler.PrintStats());
    }

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

    public void Execute(GeneratorExecutionContext context)
    {
        var ctx = new ContextWrapper(context);

        try
        {
            if (context.CancellationToken.IsCancellationRequested)
                return;

            if (context.Compilation.AssemblyName == "Unity.Logging") return;
            var runningTests = context.Compilation.AssemblyName == "SourceGeneratorTestsCompilation";
            if (runningTests == false)
            {
                var hasReferenceToLogging = context.Compilation.ReferencedAssemblyNames.Any(n => n.Name == "Unity.Logging");
                if (hasReferenceToLogging == false)
                    return;
            }

            using var _ = new Profiler.Auto("LoggingGenerator.Execute");
            var assemblyHash = GetAssemblyHash(context.Compilation);
            var asmName = context.Compilation.AssemblyName ?? "Unknown_assembly";

            var userDefinedTypes = ImmutableArray<CustomMirrorStruct>.Empty;

            ProcessAssembly(ctx, asmName, assemblyHash, userDefinedTypes);
        }
        catch (Exception e)
        {
            ctx.LogCompilerErrorUnhandledException(e);
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

    public LogCallFinder(ImmutableArray<LogStruct> filteredLogCalls)
    {
        foreach (var call in filteredLogCalls)
        {
            LogCalls.Add(call.Expression);
            LogCallsLevel.Add(call.Level);
        }
    }

    public LogCallFinder()
    {

    }

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

    public static string ExtractFullNamespace(InvocationExpressionSyntax ies)
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

    public static bool CallMatch(InvocationExpressionSyntax ies, out LogCallKind lvl)
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

