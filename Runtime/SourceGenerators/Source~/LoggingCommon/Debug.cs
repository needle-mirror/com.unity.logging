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
        static TextWriter GetLogWriter(ContextWrapper ctx)
        {
            return Console.Out;
        }

        public static void Log(ContextWrapper ctx, string level, string message)
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

        public static void LogException(ContextWrapper ctx, Exception e, string message = null)
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

        public static void LogInfo(ContextWrapper ctx, string message)
        {
            //Log(ctx, "Info", message);
        }

        public static void LogWarning(ContextWrapper ctx, string message)
        {
            //Log(ctx, "Warning", message);
        }

        public static void LogError(ContextWrapper ctx, string message)
        {
            //Log(ctx, "Error", message);
        }

        [Conditional("VERBOSE_LOGGING")]
        public static void LogVerbose(ContextWrapper ctx, string message)
        {
            //Log(ctx, "Verbose", message);
        }
    }
}
