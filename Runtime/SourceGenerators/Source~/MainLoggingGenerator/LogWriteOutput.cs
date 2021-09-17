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
        public static void SourceGenTypesFile(GeneratorExecutionContext context, StringBuilder sourceGenContent)
        {
            using var _ = new Profiler.Auto("LogWriteOutput.SourceGenTypesFile");

            try
            {
                var filename = OutputPaths.GeneratedTypesPath(context.Compilation.AssemblyName);
                OutputFileInternal(context, sourceGenContent, filename, true);
            }
            catch (Exception e)
            {
                LogErrorInternal(e, context);
            }
        }

        public static void SourceGenParserFile(GeneratorExecutionContext context, StringBuilder sourceGenContent)
        {
            using var _ = new Profiler.Auto("LogWriteOutput.SourceGenParserFile");

            try
            {
                var filename = OutputPaths.GeneratedParserPath(context.Compilation.AssemblyName);
                OutputFileInternal(context, sourceGenContent, filename, true);
            }
            catch (Exception e)
            {
                LogErrorInternal(e, context);
            }
        }

        public static void SourceGenMethodsFile(GeneratorExecutionContext context, StringBuilder sourceGenContent)
        {
            using var _ = new Profiler.Auto("LogWriteOutput.SourceGenMethodsFile");

            try
            {
                var filename = OutputPaths.GeneratedMethodsPath(context.Compilation.AssemblyName);
                OutputFileInternal(context, sourceGenContent, filename, true);
            }
            catch (Exception e)
            {
                LogErrorInternal(e, context);
            }
        }

        private static void LogErrorInternal(Exception e, GeneratorExecutionContext context)
        {
            // Due to DOTS Runtime unable to add source directly to the compilation, it is required that we write
            // files to disk, so this is an error for now. When we have removed that requirement and can add
            // sourcegen via compilation context in memory, change this to a warning, and remove this wrapper
            context.LogCompilerErrorUnhandledException(e);
            context.LogCompilerError(CompilerMessages.FileWriteException);
            context.LogCompilerError((e.HResult.ToString(), e.GetType() + " : " + e.Message));
        }

        // HACK: Currently cannot add generated source code directly to the compilation context (in DOTS Runtime) and must therefore output
        // source code files under the project's Asset folder. In addition must write an .asmref file so Unity will "see" and compile the files
        // into the target assembly. Once generated code can be added in-memory this method can be removed.
        public static void AsmrefFile(GeneratorExecutionContext context)
        {
            using var _ = new Profiler.Auto("LogWriteOutput.AsmrefFile");

            var sb = new StringBuilder();

            sb.AppendFormat(
@"{{
    ""reference"": ""{0}""
}}"
                , context.Compilation.AssemblyName);

            var assemblyName = context.Compilation.AssemblyName;
            var filename = Path.Combine(OutputPaths.SourceGenOutputFolderPath, assemblyName, assemblyName + ".asmref");

            OutputFileInternal(context, sb, filename, false);
        }

        private static void OutputFileInternal(GeneratorExecutionContext context, StringBuilder sourceGenContent, string filename, bool isSourceFile)
        {
            using var _ = new Profiler.Auto("LogWriteOutput.OutputFileInternal");

#if UNITY_LOGGING_GENERATE_TO_ASSETS_FOLDER
            const bool generateToAssetsFolder = true;
#else
            const bool generateToAssetsFolder = false;
#endif

#pragma warning disable 0162
            if (generateToAssetsFolder)
            {
                var path = new FileInfo(filename);
                if (path.Directory.Exists == false)
                    path.Directory.Create();

                var newContents = sourceGenContent.ToString();
                if (path.Exists)
                {
                    // If the file already exists and the contents are the same, don't write out the file again
                    if (File.ReadAllText(path.FullName, Encoding.UTF8).Equals(newContents))
                        return;
                }

                context.LogInfoMessage($"[Debug][WritingToAssetFolder] Writing {path.FullName} file");
                File.WriteAllText(path.FullName, newContents, Encoding.UTF8);
            }
            else
            {
                if(!context.ParseOptions.PreprocessorSymbolNames.Contains("UNITY_2021_2_OR_NEWER"))
                {
                    try
                    {
                        var path = new FileInfo(filename);
                        if (path.Directory.Exists == false)
                            path.Directory.Create();

                        File.WriteAllText(path.FullName, sourceGenContent.ToString(), Encoding.UTF8);
                    }
                    catch (Exception e)
                    {
                        context.LogCompilerWarningTempWriteException(e, filename);
                    }
                }

                if (isSourceFile)
                {
                    context.AddSource(Path.GetFileNameWithoutExtension(filename), SourceText.From(sourceGenContent.ToString(), Encoding.UTF8));
                }
            }
#pragma warning restore 0162
        }
    }
}
