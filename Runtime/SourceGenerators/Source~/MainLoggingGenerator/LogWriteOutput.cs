using System;
using System.Text;
using System.IO;
using LoggingCommon;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using SourceGenerator.Logging.Declarations;
using System.Linq;

namespace SourceGenerator.Logging
{
    public static class LogWriteOutput
    {
        public static void SourceGenTypesFile(ContextWrapper context, string sourceGenContent)
        {
            using var _ = new Profiler.Auto("LogWriteOutput.SourceGenTypesFile");

            try
            {
                OutputFileInternal(context, sourceGenContent, Declarations.OutputPaths.SourceGenTextLoggerTypesFileName, true);
            }
            catch (Exception e)
            {
                LogErrorInternal(e, context);
            }
        }

        public static void SourceGenParserFile(ContextWrapper context, string sourceGenContent)
        {
            using var _ = new Profiler.Auto("LogWriteOutput.SourceGenParserFile");

            try
            {
                OutputFileInternal(context, sourceGenContent, Declarations.OutputPaths.SourceGenTextLoggerParserFileName, true);
            }
            catch (Exception e)
            {
                LogErrorInternal(e, context);
            }
        }

        public static void SourceGenMethodsFile(ContextWrapper context, string sourceGenContent)
        {
            using var _ = new Profiler.Auto("LogWriteOutput.SourceGenMethodsFile");

            try
            {
                OutputFileInternal(context, sourceGenContent, Declarations.OutputPaths.SourceGenTextLoggerMethodsFileName, true);
            }
            catch (Exception e)
            {
                LogErrorInternal(e, context);
            }
        }

        private static void LogErrorInternal(Exception e, ContextWrapper context)
        {
            context.LogCompilerErrorUnhandledException(e);
            context.LogCompilerError(CompilerMessages.FileWriteException);
            context.LogCompilerError((e.HResult.ToString(), e.GetType() + " : " + e.Message));
        }

        private static void OutputFileInternal(ContextWrapper context, string sourceGenContent, string filename, bool isSourceFile)
        {
            using var _ = new Profiler.Auto("LogWriteOutput.OutputFileInternal");

            if (isSourceFile)
            {
                var asmName = context.Compilation.AssemblyName ?? "Unknown_assembly";
                filename = Path.GetFileNameWithoutExtension(filename);
                context.AddSource($"{asmName}_{filename}", SourceText.From(sourceGenContent, Encoding.UTF8));
            }
        }
    }
}
