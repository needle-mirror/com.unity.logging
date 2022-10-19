using System;
using System.Collections.Generic;
using System.Linq;
using Bee;
using Bee.Core;
using Bee.CSharpSupport;
using Bee.CSharpSupport.Experimental;
using Bee.DotNet;
using Bee.VisualStudioSolution;
using NiceIO;
using Unity.TinyProfiling;

class Build
{
    static void Main()
    {
        using var context = new BuildProgramContext();

        ICSharpReferenceable[] refsForSourceGenerator =
        {
            new PackageReference {Name = "Microsoft.CodeAnalysis.CSharp", Version = "4.0.1"}
        };

        var DebugBuild = false;
        var VerboseLogging = DebugBuild;

        var common = new CSharpProgram2
        {
            Root = "LoggingCommon",
            TargetFramework = "netstandard2.0",
            LangVersion = "latest",
            References = {refsForSourceGenerator},
            OutputType = OutputType.Library
        };

        var generator = new CSharpProgram2
        {
            Root = "MainLoggingGenerator",
            OutputType = OutputType.Library,
            TargetFramework = "netstandard2.0",
            LangVersion = "latest",
            References =
            {
                common,
                refsForSourceGenerator
            }
        };

        if (VerboseLogging)
        {
            const string def = "VERBOSE_LOGGING";
            common.Defines.Add(def);
            generator.Defines.Add(def);
        }

        var tests = new CSharpProgram2
        {
            OutputType = OutputType.Library,
            Root = "Tests",
            TargetFramework = "net50",
            LangVersion = "latest",
            References =
            {
                generator,
                common,
                new PackageReference {Name = "NUnit", Version = "3.13.0"},
                new PackageReference {Name = "NUnit3TestAdapter", Version = "4.0.0"},
                new PackageReference {Name = "Microsoft.NET.Test.Sdk", Version = "16.5.0"}
            },
        };

        var codeGenMode = DebugBuild ? CSharpCodeGen.Debug : CSharpCodeGen.Release;

        var builtTests = tests.SetupPublishFrameworkDependent(Backend.Current.ArtifactsPath.Combine("tests"), codeGenMode);
        var publishedGenerator = generator.SetupPublishFrameworkDependent("build/generator", codeGenMode);
        publishedGenerator.DotNetAssemblies.First().DeployTo("..");

        DotNetSdk.Default.SetupDotnetTest(builtTests.DotNetAssemblies.Where(a => a.Path.FileName.EndsWith("Tests.dll" )).ToArray(), "TestExecution");

        new VisualStudioSolution()
        {
            Path = "Solution.gen.sln",
            Projects = {generator, tests, StandaloneBeeDriver.BuildProgramProjectFileFor(context)}
        }.Setup();
    }
}
