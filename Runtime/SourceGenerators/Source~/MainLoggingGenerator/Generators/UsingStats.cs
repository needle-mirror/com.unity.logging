using System.Collections.Generic;
using System.Collections.Immutable;

namespace MainLoggingGenerator.Generators
{
    /// <summary>
    /// Incremental source generator. Struct that has information about multiple using directives UsingDirStruct
    /// </summary>
    public readonly struct UsingStats
    {
        public readonly bool UseUnityLogging;
        public readonly ImmutableArray<string> Aliases;

        public UsingStats(ImmutableArray<UsingDirStruct> usingDirectives)
        {
            UseUnityLogging = false;
            var aliasesSet = new HashSet<string>();

            foreach (var usingDir in usingDirectives)
            {
                UseUnityLogging = UseUnityLogging || usingDir.UseUnityLogging;

                if (string.IsNullOrEmpty(usingDir.AliasName) == false)
                    aliasesSet.Add(usingDir.AliasName);
            }

            Aliases = aliasesSet.ToImmutableArray();
        }
    }
}
