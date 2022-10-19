using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGenerator.Logging;

namespace MainLoggingGenerator.Generators
{
    /// <summary>
    /// Incremental source generator. Struct that has information about Log. call
    /// </summary>
    public readonly struct LogStruct : IEquatable<LogStruct>
    {
        public readonly bool Valid;
        public readonly InvocationExpressionSyntax Expression;
        public readonly LogCallKind Level;

        private readonly string InvokeString;

        private string FullNameSpace => LogCallFinder.ExtractFullNamespace(Expression);

        public LogStruct(InvocationExpressionSyntax invoke)
        {
            Valid = LogCallFinder.CallMatch(invoke, out Level);
            if (Valid)
            {
                Expression = invoke;
                InvokeString = invoke.ToString();
            }
            else
            {
                Expression = null;
                InvokeString = "";
            }
        }

        public bool Equals(LogStruct other)
        {
            return Valid == other.Valid && Level == other.Level && InvokeString == other.InvokeString && Expression == other.Expression;
        }

        public override bool Equals(object obj)
        {
            return obj is LogStruct other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (Valid == false) return 0;

            unchecked
            {
                var hashCode = (int)Level;
                hashCode = (hashCode * 397) ^ (InvokeString != null ? InvokeString.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Expression != null ? Expression.GetHashCode() : 0);

                return hashCode;
            }
        }

        public bool Validate(UsingStats usingDirectives)
        {
            const string loggingAssemblyName = "Unity.Logging";

            var startsWithLog = InvokeString.StartsWith("Log.");

            // has using Unity.Logging and call starts with 'Log.'
            if (usingDirectives.UseUnityLogging && startsWithLog)
                return true;

            // full Unity.Logging.Log. call
            if (InvokeString.StartsWith(loggingAssemblyName + ".Log."))
                return true;

            // alias check
            foreach (var alias in usingDirectives.Aliases)
            {
                if (InvokeString.StartsWith(alias + ".Log."))
                    return true;
            }

            return FullNameSpace.StartsWith(loggingAssemblyName);
        }
    }
}
