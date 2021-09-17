using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LoggingCommon;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    internal static class Debug
    {
        private static string ProjectPath { get; set; } = "";
        private static string OutputFolder { get; set; } = Path.Combine("Temp", "LoggingGenerated");


        private static readonly Dictionary<string, string> s_ctx2LogName = new();
        private static string GetLogFile(GeneratorExecutionContext ctx)
        {
            lock (s_ctx2LogName)
            {
                var asmName = $"#{Thread.CurrentThread.ManagedThreadId}_{ctx.Compilation.AssemblyName}";

                if (s_ctx2LogName.TryGetValue(asmName, out var logName))
                    return logName;

                logName = $"Log_{Guid.NewGuid()}.log";
                s_ctx2LogName[asmName] = logName;

                return logName;
            }
        }

        private static string GetOutputPath(GeneratorExecutionContext ctx)
        {
            if (string.IsNullOrEmpty(ProjectPath))
            {
                ProjectPath = Environment.CurrentDirectory;
                var l = ProjectPath.LastIndexOf("/Library/");
                if (l < 0)
                    l = ProjectPath.LastIndexOf("\\Library\\");

                if (l >= 0)
                    ProjectPath = ProjectPath.Substring(0, l);
            }

            var assemblyName = ctx.Compilation.AssemblyName;

            var outputPath = Path.Combine(ProjectPath, OutputFolder, assemblyName);
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            return outputPath;
        }

        private static string GetLogFilePath(GeneratorExecutionContext ctx)
        {
            return Path.Combine(GetOutputPath(ctx), GetLogFile(ctx));
        }

        static TextWriter GetLogWriter(GeneratorExecutionContext ctx)
        {
            if(!ctx.ParseOptions.PreprocessorSymbolNames.Contains("UNITY_2021_2_OR_NEWER"))
                return File.AppendText(GetLogFilePath(ctx));
            return Console.Out;
        }

        public static void Log(GeneratorExecutionContext ctx, string level, string message)
        {
            using var _ = new Profiler.Auto("Debug.Log overhead");
            try
            {
                using var writer = GetLogWriter(ctx);
                writer.WriteLine($"[{level}]{message}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception while writing to log: {exception.Message}. {exception.StackTrace}");
            }
        }

        public static void LogException(GeneratorExecutionContext ctx, Exception e, string message = null)
        {
            using var _ = new Profiler.Auto("Debug.LogException overhead");
            try
            {
                using var writer = GetLogWriter(ctx);
                writer.Write("[Exception]");
                if (!string.IsNullOrEmpty(message))
                    writer.WriteLine(message);
                writer.WriteLine(e.ToString());
                writer.WriteLine("Callstack:");
                writer.Write(e.StackTrace);
                writer.Write('\n');

                Console.WriteLine($"{e.Message}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Exception while writing to log: {exception.Message}. {exception.StackTrace}");
            }
        }

        public static void LogInfo(GeneratorExecutionContext ctx, string message)
        {
            Log(ctx, "Info", message);
        }

        public static void LogWarning(GeneratorExecutionContext ctx, string message)
        {
            Log(ctx, "Warning", message);
        }

        public static void LogError(GeneratorExecutionContext ctx, string message)
        {
            Console.WriteLine($"Error: {message}");
            Log(ctx, "Error", message);
        }

        [Conditional("VERBOSE_LOGGING")]
        public static void LogVerbose(GeneratorExecutionContext ctx, string message)
        {
            Console.WriteLine($"Error: {message}");
            Log(ctx, "Verbose", message);
        }
    }
}
