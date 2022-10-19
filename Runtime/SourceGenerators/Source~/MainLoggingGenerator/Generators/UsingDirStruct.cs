using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MainLoggingGenerator.Generators
{
    /// <summary>
    /// Incremental source generator. Struct that has information about using directive - to detect using Alias = Unity.Logging or using Unity.Logging;
    /// </summary>
    public readonly struct UsingDirStruct : IEquatable<UsingDirStruct>
    {
        public readonly bool UseUnityLogging;
        public readonly string AliasName;

        public UsingDirStruct(UsingDirectiveSyntax usingDirective)
        {
            UseUnityLogging = usingDirective.Alias == null;
            AliasName = UseUnityLogging ? "" : usingDirective.Alias.Name.ToString();
        }

        public bool Equals(UsingDirStruct other)
        {
            return UseUnityLogging == other.UseUnityLogging && AliasName == other.AliasName;
        }

        public override bool Equals(object obj)
        {
            return obj is UsingDirStruct other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (UseUnityLogging.GetHashCode() * 397) ^ (AliasName != null ? AliasName.GetHashCode() : 0);
            }
        }
    }
}
