using NUnit.Framework;
using SourceGenerator.Logging.Declarations;

namespace Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class MessageAnalysisTests
    {
        [Test]
        public void TestNoError()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(452, ""hi!""); // omitted
        Unity.Logging.Log.Info(""Used: {0} used: {1}"", 452, ""hi!"");
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(2, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(0, diagnostics.Length);
        }

        [Test]
        public void TestBugWithOmmitedMessage() // Bug MTT-4188
        {
            var testData = @"
struct StructWithAllNonSerializedAttribute
{
    [NotLogged]
    public long Long1;

    [NotLogged]
    public int Int1;

    [NotLogged]
    public FixedString128Bytes Fs64;
}

class ClassA()
{
    public void A()
    {
        var s1 = new StructWithAllNonSerializedAttribute
        {
            Fs64 = ""FString64\"",
            Int1 = 42,
            Long1 = -12315253213
        };

        Unity.Logging.Log.Info(""{Arg1} {NewLine} hello"", s1);

        Unity.Logging.Log.Info(s1); // omitted
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(2, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);

            var methods = generator.methodsGenCode;
            Assert.IsTrue(methods.Contains("public static void Info(string msg, in global::StructWithAllNonSerializedAttribute arg0)"));
            Assert.IsTrue(methods.Contains("public static void Info(in global::StructWithAllNonSerializedAttribute arg0)"));
        }

        [Test]
        public void TestExtraHole()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Used: {0} Not used: {1}  !"", 452);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(1, diagnostics.Length);

            var warn = diagnostics[0];
            Assert.AreEqual(CompilerMessages.LiteralMessageMissingArgForHole.Item1, warn.Id);
            Assert.AreEqual(string.Format(CompilerMessages.LiteralMessageMissingArgForHole.Item2, "{1}"), warn.Descriptor.MessageFormat.ToString());

            var loc = warn.Location.SourceTree.ToString().Substring(warn.Location.SourceSpan.Start, warn.Location.SourceSpan.Length);
            Assert.AreEqual("{1}", loc);
        }

        [Test]
        public void TestReservedArg()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Hello, {Level}!"");
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
        }

        [Test]
        public void TestMissingNamedArgWithReservedWord()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Hello, {Level} {WorldName}!"");
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(1, diagnostics.Length);

            var warn = diagnostics[0];
            Assert.AreEqual(CompilerMessages.LiteralMessageMissingArgForHole.Item1, warn.Id);
            Assert.AreEqual(string.Format(CompilerMessages.LiteralMessageMissingArgForHole.Item2, "{WorldName}"), warn.Descriptor.MessageFormat.ToString());

            var loc = warn.Location.SourceTree.ToString().Substring(warn.Location.SourceSpan.Start, warn.Location.SourceSpan.Length);
            Assert.AreEqual("{WorldName}", loc);
        }

        [Test]
        public void TestMissingNamedArg()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Hello, {WorldName}!"");
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(1, diagnostics.Length);

            var warn = diagnostics[0];
            Assert.AreEqual(CompilerMessages.LiteralMessageMissingArgForHole.Item1, warn.Id);
            Assert.AreEqual(string.Format(CompilerMessages.LiteralMessageMissingArgForHole.Item2, "{WorldName}"), warn.Descriptor.MessageFormat.ToString());

            var loc = warn.Location.SourceTree.ToString().Substring(warn.Location.SourceSpan.Start, warn.Location.SourceSpan.Length);
            Assert.AreEqual("{WorldName}", loc);
        }

        [Test]
        public void TestExtraHoleNumericNoErrors()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Used: {0} Not used: {1} {0} {0:X} !"", 452, 3);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(0, diagnostics.Length);
        }

        [Test]
        public void TestExtraHoleNonNumeric()
        {
            // https://messagetemplates.org/
            // Templates that use numeric property names like {0} and {1} exclusively imply that arguments to the template are captured by numeric index
            // If any of the property names are non-numeric, then all arguments are captured by matching left-to-right with holes in the order in which they appear
            // Repeated names are not allowed
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Used: {0} Not used: {named} {0:D} {0:X} !"", 452, 3);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(1, diagnostics.Length);

            var warn = diagnostics[0];
            Assert.AreEqual(CompilerMessages.LiteralMessageRepeatingNamedArg.Item1, warn.Id);
            Assert.AreEqual(string.Format(CompilerMessages.LiteralMessageRepeatingNamedArg.Item2, "{0}"), warn.Descriptor.MessageFormat.ToString());
            var loc = warn.Location.SourceTree.ToString().Substring(warn.Location.SourceSpan.Start, warn.Location.SourceSpan.Length);
            Assert.AreEqual("{0:D}", loc);
        }

        [Test]
        public void TestMissingNumericHole()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Used: {0} Not used: {2} {0} {0:X} !"", 452, 3, 23);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(1, diagnostics.Length);

            var warn = diagnostics[0];

            Assert.AreEqual(CompilerMessages.LiteralMessageMissingIndexArg.Item1, warn.Id);
            Assert.AreEqual(string.Format(CompilerMessages.LiteralMessageMissingIndexArg.Item2, "1"), warn.Descriptor.MessageFormat.ToString());

            var loc = warn.Location.SourceTree.ToString().Substring(warn.Location.SourceSpan.Start, warn.Location.SourceSpan.Length);
            Assert.AreEqual("\"Used: {0} Not used: {2} {0} {0:X} !\"", loc);
        }

        [Test]
        public void TestExtraNamedHoleWithReservedWord()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Used: {0} {Level} Not used: {named:##.0;-##.0;zero}  !"", 452);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(1, diagnostics.Length);

            var warn = diagnostics[0];
            Assert.AreEqual(CompilerMessages.LiteralMessageMissingArgForHole.Item1, warn.Id);
            Assert.AreEqual(string.Format(CompilerMessages.LiteralMessageMissingArgForHole.Item2, "{named}"), warn.Descriptor.MessageFormat.ToString());

            var loc = warn.Location.SourceTree.ToString().Substring(warn.Location.SourceSpan.Start, warn.Location.SourceSpan.Length);
            Assert.AreEqual("{named:##.0;-##.0;zero}", loc);
        }

        [Test]
        public void TestExtraNamedHole()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Used: {0} Not used: {named:##.0;-##.0;zero}  !"", 452);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(1, diagnostics.Length);

            var warn = diagnostics[0];
            Assert.AreEqual(CompilerMessages.LiteralMessageMissingArgForHole.Item1, warn.Id);
            Assert.AreEqual(string.Format(CompilerMessages.LiteralMessageMissingArgForHole.Item2, "{named}"), warn.Descriptor.MessageFormat.ToString());

            var loc = warn.Location.SourceTree.ToString().Substring(warn.Location.SourceSpan.Start, warn.Location.SourceSpan.Length);
            Assert.AreEqual("{named:##.0;-##.0;zero}", loc);
        }

        [Test]
        public void TestExtraArg()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Us{{ed: {{{0}}} Not u}}sed: {1}"", 452, 23, ""Extra"");
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(1, diagnostics.Length);

            var warn = diagnostics[0];

            Assert.AreEqual(CompilerMessages.LiteralMessageMissingHoleForArg.Item1, warn.Id);
            Assert.AreEqual(CompilerMessages.LiteralMessageMissingHoleForArg.Item2, warn.Descriptor.MessageFormat.ToString());
            var loc = warn.Location.SourceTree.ToString().Substring(warn.Location.SourceSpan.Start, warn.Location.SourceSpan.Length);
            Assert.AreEqual("\"Extra\"", loc);
        }

        [Test]
        public void TestWrongMessageFormat()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Us{ed- {0} Not u}}sed- {1}"", 452, 23);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(1, diagnostics.Length);

            var warn = diagnostics[0];

            Assert.AreEqual(CompilerMessages.LiteralMessageInvalidArgument.Item1, warn.Id);
            Assert.AreEqual(CompilerMessages.LiteralMessageInvalidArgument.Item2, warn.Descriptor.MessageFormat.ToString());
            var loc = warn.Location.SourceTree.ToString().Substring(warn.Location.SourceSpan.Start, warn.Location.SourceSpan.Length);
            Assert.AreEqual("{ed- {0}", loc);
        }

        [Test]
        public void TestPriorityCheck()
        {
            // first check malformed, then count

            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Saving {File to {Directory}"", ""file"", ""dir"");
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);
            Assert.AreEqual(1, diagnostics.Length);

            var warn = diagnostics[0];
            Assert.AreEqual(CompilerMessages.LiteralMessageInvalidArgument.Item1, warn.Id);
            Assert.AreEqual(CompilerMessages.LiteralMessageInvalidArgument.Item2, warn.Descriptor.MessageFormat.ToString());
            var loc = warn.Location.SourceTree.ToString().Substring(warn.Location.SourceSpan.Start, warn.Location.SourceSpan.Length);
            Assert.AreEqual("{File to {Directory}", loc);
        }

        [Test]
        public void TestException()
        {
            // first check malformed, then count

            var testData = @"
class ClassA()
{
    public void A()
    {
        string AbsFilename ""abc"";
        System.Exception e = new System.Exception();

        Unity.Logging.Log.Error(""Exception during reading {FileName}: {Exception}"", AbsFilename, e);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
        }


        [Test]
        public void TestNewLine()
        {
            // first check malformed, then count

            var testData = @"
class ClassA()
{
    public void A()
    {
        string AbsFilename = ""abc"";

        Unity.Logging.Log.Error(""Exception during reading {FileName} {NewLine}"", AbsFilename);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
        }

        [Test]
        public void TestMessageIsOKToUseInLog()
        {
            // first check malformed, then count

            var testData = @"
class ClassA()
{
    public void A()
    {
        string s1 = ""abc"";
        string s2 = ""abc"";

        Unity.Logging.Log.Info(""{Arg1} {NewLine} {Message} {Properties}"", s1, s2); // can use 'Message' in log
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
        }



    }
}
