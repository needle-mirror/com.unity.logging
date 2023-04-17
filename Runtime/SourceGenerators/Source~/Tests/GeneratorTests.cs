using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using SourceGenerator.Logging;
using SourceGenerator.Logging.Declarations;

namespace Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class GeneratorTests
    {
        [Test]
        public void TestEnum()
        {
            var testData = @"
class ClassA()
{
    public enum Count {
        Zero,
        One,
        Two,
        Three,
        Many,
    };

    public void A()
    {
        Count c0 = Count.Zero;
        Count c1 = Count.One;
        Count c2 = Count.Two;
        Count c3 = Count.Three;
        Count cM = Count.Many;
        Unity.Logging.Log.Info(""Counting: {0} {1} {2} {3} {4}"", c0, c1, c2, c3, cM);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
            var m = generator.methodsGenCode;

            /*
            Assert.IsTrue(generator.invokeData.IsValid);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);

            Assert.AreEqual(1, generator.structureData.StructTypes.Count);
            Assert.AreEqual(2, generator.structureData.StructTypes[0].FieldData.Count);

            Assert.AreEqual("global::System.Int32", generator.structureData.StructTypes[0].FieldData[0].FieldTypeName);
            Assert.AreEqual("string", generator.structureData.StructTypes[0].FieldData[1].FieldTypeName);
            */

            var s = generator.typesGenCode.ToString();
// XXX            Assert.IsTrue(s.Contains("burstable=<True>"));
        }
        [Test]
        public void TestClass()
        {
            var testData = @"
class Foo()
{
    private int n;
    private string s;

    Foo(int argn, string args)
    {
        n = argn;
        s = args;
    }

    public override string ToString()
    {
        return $""{{s}}: {{n}}"";
    }
}

class ClassA()
{

    public void A()
    {
        var foo = new Foo(1337, ""Hecate"");
        Unity.Logging.Log.Info(""Class Foo: {class}"", foo);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
            var m = generator.methodsGenCode;

            /*
            Assert.IsTrue(generator.invokeData.IsValid);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);

            Assert.AreEqual(1, generator.structureData.StructTypes.Count);
            Assert.AreEqual(2, generator.structureData.StructTypes[0].FieldData.Count);

            Assert.AreEqual("global::System.Int32", generator.structureData.StructTypes[0].FieldData[0].FieldTypeName);
            Assert.AreEqual("string", generator.structureData.StructTypes[0].FieldData[1].FieldTypeName);
            */

            var s = generator.typesGenCode.ToString();
// XXX            Assert.IsTrue(s.Contains("burstable=<True>"));
        }

        [Test]
        public void TestMultipleCallsWithStrings()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        string M = ""this is a simple string"";
        StringBuilder sb = new StringBuilder();
        sb.Append(""Hello, friend"");
        StringBuilder sbArg = new StringBuilder();
        sbArg.Append(""I'd like to have an argument, please."");

        Unity.Logging.Log.Info(""3awmdoawjdoaiwjdioawjd {0} end3"", ((FixedString128Bytes)""adwnipdnaiwpndipawndpainwdpianwdianwdinawdi"", (FixedString64Bytes)""adawdawddamwdoijawJD@(*EY&)!Y@EH""))
        Unity.Logging.Log.Info(""{0}"", (FixedString32Bytes)""Fixed String Data"");
        Unity.Logging.Log.Info(M);
        Unity.Logging.Log.Info(sb);
        Unity.Logging.Log.Info(sb.ToString());
        Unity.Logging.Log.Info(sb, sbArg);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(6, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
            var m = generator.methodsGenCode;

            /*
            Assert.IsTrue(generator.invokeData.IsValid);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);

            Assert.AreEqual(1, generator.structureData.StructTypes.Count);
            Assert.AreEqual(2, generator.structureData.StructTypes[0].FieldData.Count);

            Assert.AreEqual("global::System.Int32", generator.structureData.StructTypes[0].FieldData[0].FieldTypeName);
            Assert.AreEqual("string", generator.structureData.StructTypes[0].FieldData[1].FieldTypeName);
            */

            var s = generator.typesGenCode.ToString();
// XXX            Assert.IsTrue(s.Contains("burstable=<True>"));
        }

        [Test]
        public void TestStringDecoratorFixed64StringFunction()
        {
            var testData = @"
class ClassA()
{
    public static void DecoratorFixedStringInt(ref LogContextWithDecorator d)
    {
        Unity.Logging.Log.To(d).Decorate((FixedString64Bytes)""SomeIntAAAA"", (FixedString32Bytes)""fs32"");
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
            var m = generator.methodsGenCode;

            Assert.IsNotEmpty(m);

            Assert.IsFalse(m.Contains("payloadHandle_")); // fixedstring must be passed as-is
        }

        [Test]
        public void TestStringInfoFixed64StringFunction()
        {
            var testData = @"
class ClassA()
{
    internal struct LogStringsFixedCombinationUniq
    {
        internal int a;
    }

    public static void DecoratorFixedStringInt(ref LogContextWithDecorator d)
    {
        Unity.Logging.Log.To(d).Info((FixedString64Bytes)""SomeIntAAAA {0} {1}"", 321, (FixedString32Bytes)""fs32"");

        sbyte sb = 32;
        var smsg = ""c {2} {1} {0}"";
        var sarg = ""bbb"";
        Unity.Logging.Log.Info(""a {1} {2} {0}"", new LogStringsFixedCombinationUniq(), ""arg"", sb);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(2, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
            var m = generator.methodsGenCode;

            Assert.IsNotEmpty(m);



            Assert.IsTrue(ContainsAndPreviousLineHas(m, "Info(in FixedString64Bytes msg, in Int32 arg0, in FixedString32Bytes arg1)"));
            Assert.IsTrue(ContainsAndPreviousLineHas(m, "_E_E(in FixedString64Bytes msg, in Int32 arg0, in FixedString32Bytes arg1, ref LogController logController, ref LogControllerScopedLock @lock)", "[BurstCompile"));


            Assert.IsTrue(ContainsAndPreviousLineHas(m, "_E_E(in PayloadHandle msg, in global::Unity.Logging.LogStringsFixedCombinationUniq__SiZRgLDn_PBnNMDKN9RgKjg_E_E arg0, in PayloadHandle arg1, in SByte arg2, ref LogController logController, ref LogControllerScopedLock @lock)", "[BurstCompile"));
            Assert.IsTrue(ContainsAndPreviousLineHas(m, "string arg1, in SByte arg2)"));
            Assert.IsTrue(m.Contains("payloadHandle_arg1")); // payloadHandle_arg1 fixedstring must be passed as-is
        }

        [Test]
        public void TestStringDecoratorFunction()
        {
            var testData = @"
class ClassA()
{
    public static void DecoratorFixedStringInt(ref LogContextWithDecorator d)
    {
        Unity.Logging.Log.To(d).Decorate(""SomeIntAAAA"", 321);
    }

    public void A()
    {
        using var decor1 = Unity.Logging.Log.Decorate((FixedString64Bytes)""SomeInt"", DecoratorFixedStringInt, true);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(2, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
            var m = generator.methodsGenCode;

            Assert.IsNotEmpty(m);

            // should NOT have (FixedString32Bytes True, Int32 True)
            Assert.IsFalse(m.Contains("__(in FixedString32Bytes msg, in Int32 arg0, ref LogContextWithDecorator handles)"));
            Assert.IsFalse(m.Contains("public static LogDecorateScope Decorate(in FixedString32Bytes msg, in Int32 arg0)"));
            Assert.IsFalse(m.Contains("public static LogDecorateScope Decorate(in this LogContextWithLock ctx, in FixedString32Bytes msg, in Int32 arg0)"));
            Assert.IsFalse(m.Contains("public static void Decorate(ref this LogContextWithDecoratorLogTo dec, in FixedString32Bytes msg, in Int32 arg0)"));

            // (string False, Int32 True)
            // [BurstCompile]
            // private static void WriteBurstedDecorateCny49N71douKNTGuCMRNkA__(in PayloadHandle msg, in Int32 arg0, ref LogContextWithDecorator handles)
            // public static LogDecorateScope Decorate(string msg, in Int32 arg0)
            // public static LogDecorateScope Decorate(in this LogContextWithLock ctx, string msg, in Int32 arg0)
            // public static void Decorate(this LogContextWithDecoratorLogTo dec, string msg, in Int32 arg0)
            Assert.IsTrue(ContainsAndPreviousLineHas(m, "_E_E(in PayloadHandle msg, in Int32 arg0, ref LogContextWithDecorator handles)", "[BurstCompile"));
            Assert.IsTrue(ContainsAndPreviousLineHas(m, "public static LogDecorateScope Decorate(string msg, in Int32 arg0)"));
            Assert.IsTrue(ContainsAndPreviousLineHas(m, "public static LogDecorateScope Decorate(in this LogContextWithLock ctx, string msg, in Int32 arg0)"));
            Assert.IsTrue(ContainsAndPreviousLineHas(m, "public static void Decorate(in this LogContextWithDecoratorLogTo dec, string msg, in Int32 arg0)"));

            Assert.IsTrue(m.Contains("public static LogDecorateHandlerScope Decorate(FixedString512Bytes message, LoggerManager.OutputWriterDecorateHandler Func, bool isBurstable)"));
        }

        static bool ContainsAndPreviousLineHas(string inputStr, string containsSubStr, string prevLineHas = "")
        {
            var indx = inputStr.IndexOf(containsSubStr, StringComparison.Ordinal);

            if (indx == -1)
                return false;

            if (string.IsNullOrEmpty(prevLineHas))
                return true;

            var lastNewLine = inputStr.LastIndexOf('\n', indx);

            if (lastNewLine == -1)
                return false;

            var firstNewLine = inputStr.LastIndexOf('\n', lastNewLine - 1);
            if (firstNewLine == -1)
                firstNewLine = 0;

            var n = lastNewLine - firstNewLine;

            return inputStr.IndexOf(prevLineHas, firstNewLine, n, StringComparison.Ordinal) != -1;
        }

        [Test]
        public void TestStringInfoFunction()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info((FixedString64Bytes)""SomeInt {a} {dd}"", ""d"", 42);
    }
}";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
            var m = generator.methodsGenCode;

            Assert.IsNotEmpty(m);

            Assert.IsTrue(ContainsAndPreviousLineHas(m, "_E_E(in FixedString64Bytes msg, in PayloadHandle arg0, in Int32 arg1, ref LogController logController, ref LogControllerScopedLock @lock)", "[BurstCompile"));
            Assert.IsTrue(ContainsAndPreviousLineHas(m, "public static void Info(in FixedString64Bytes msg, string arg0, in Int32 arg1)"));
            Assert.IsTrue(ContainsAndPreviousLineHas(m, "public static void Info(this LogContextWithLock dec, in FixedString64Bytes msg, string arg0, in Int32 arg1)"));
        }

        [Test]
        public void TestTupleContainingString()
        {
            var testData = @"
class ClassA()
{
    public void A()
    {
        Unity.Logging.Log.Info(""Tuple {0}"", (42, ""Hello, friend""));
    }
}";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);

            Assert.IsTrue(generator.invokeData.IsValid);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);

            Assert.AreEqual(1, generator.structureData.StructTypes.Count);
            Assert.AreEqual(2, generator.structureData.StructTypes[0].FieldData.Count);

            Assert.AreEqual("global::System.Int32", generator.structureData.StructTypes[0].FieldData[0].FieldTypeName);
            Assert.AreEqual("global::System.String", generator.structureData.StructTypes[0].FieldData[1].FieldTypeName);

            var s = generator.typesGenCode.ToString();
// XXX            Assert.IsTrue(s.Contains("burstable=<True>"));
            Assert.IsTrue(s.Contains("PayloadHandle Item2"));
            Assert.IsTrue(Regex.Match(s, "Item2 = .*CopyStringToPayload").Success);
        }

        [Test]
        public void TestStructContainingString()
        {
            var testData = @"
class ClassA()
{
    public struct StructContainingString
    {
        public int I0;
        public string S0;
    }

    public void A()
    {
        StructContainingString scs = new StructContainingString{
            I0 = 42,
            S0 = ""Hello, friend""
        };
        Unity.Logging.Log.Info(scs);
    }
}";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);

            Assert.IsTrue(generator.invokeData.IsValid);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);

            Assert.AreEqual(1, generator.structureData.StructTypes.Count);
            Assert.AreEqual(2, generator.structureData.StructTypes[0].FieldData.Count);

            Assert.AreEqual("global::System.Int32", generator.structureData.StructTypes[0].FieldData[0].FieldTypeName);
            Assert.AreEqual("global::System.String", generator.structureData.StructTypes[0].FieldData[1].FieldTypeName);

            var s = generator.typesGenCode.ToString();
            Assert.IsTrue(s.Contains("PayloadHandle S0"));
            Assert.IsTrue(Regex.Match(s, "S0 = .*CopyStringToPayload").Success);
        }

        [Test]
        public void TestLogWithStringFormatBursted()
        {
            var testData = @"
class ClassA
{
     public struct ContextTestStruct1
     {
         public int Field1;

         public override string ToString()
         {
             return $""[{Field1}]:"";
         }
     }


     public struct ContextTestStruct2
     {
         public float Field1;
         public int Field2;

         public override string ToString()
         {
             return $""[{Field1}, {Field2}]"";
         }
     }

    public void A()
    {
        string format = ""{0} Context data at start and end {1}"";
        ContextTestStruct1 cts1 = new ContextTestStruct2{
            Field1 = 5445,
        };
        ContextTestStruct2 cts2 = new ContextTestStruct2{
            Field1 = 1.24f,
            Field2 = 789,
        };

        Unity.Logging.Log.Info(format, cts2, cts1);
    }
}
";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.AreEqual(1, Count(generator.methodsGenCode, "handles.Add(msg);"), "msg should be added only once");
        }

        public static int Count(string s, string substr, StringComparison strComp = StringComparison.Ordinal)
        {
            int count = 0, index = s.IndexOf(substr, strComp);
            while (index != -1)
            {
                count++;
                index = s.IndexOf(substr, index + substr.Length, strComp);
            }
            return count;
        }

        [Test]
        public void TestStructWithRefTypeString()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public string S = ""foo"";
    }

    public void A()
    {
        Unity.Logging.Log.Info(""Next metasyntactic variable is {0}"", new Data().S);
    }
}
";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var gen = CommonUtils.GenerateCode(testData);
        }

        [Test]
        public void TestStructWithRefTypeStringDecorate()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public string S = ""foo"";
    }

    public void A()
    {
        Unity.Logging.Log.Decorate(""Next metasyntactic variable is {0}"", new Data().S);
    }
}
";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Decorate, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);

            var methods = generator.methodsGenCode;

            var indxStart = methods.IndexOf("public static LogDecorateScope Decorate(string msg, string arg0)", StringComparison.Ordinal);
            Assert.IsTrue(indxStart >= 0, "Cannot find Decorate method");

            var indxCallBurstedDecor = methods.IndexOf("_E_E(payloadHandle_msg, payloadHandle_arg0, ref dec);", indxStart, StringComparison.Ordinal);
            Assert.IsTrue(indxCallBurstedDecor >= 0, "Cannot find Decorate method part");

            var n = indxCallBurstedDecor - indxStart;


            var indxCallMsgConversion = methods.IndexOf(" payloadHandle_msg =", indxStart, n, StringComparison.Ordinal);
            Assert.IsTrue(indxCallMsgConversion >= 0, "Cannot find payloadHandle_msg conversion");

            var indxCallConversion = methods.IndexOf(" payloadHandle_arg0 =", indxStart, n, StringComparison.Ordinal);
            Assert.IsTrue(indxCallConversion >= 0, "Cannot find payloadHandle_arg0 conversion");
        }

        [Test]
        public void TestStructWithRefTypeStringDecorateMessage()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        string S = ""foo {0}"";
        Unity.Logging.Log.Decorate(S, 42);
    }
}
";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Decorate, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);

            var methods = generator.methodsGenCode;

            // [BurstCompile(DisableDirectCall = true)]
            // private static void WriteBurstedDecorateuL287IROOjCwnYh9BcsxSQ__(in PayloadHandle msg, in Int32 arg0, ref LogContextWithDecorator handles)
            // {
            //     ref var memManager = ref LogContextWithDecorator.GetMemoryManagerNotThreadSafe(ref handles);
            //
            //     var handle = Unity.Logging.Builder.BuildMessage(msg, ref memManager);
            // /// should be handles.Add(handle);
            //     if (handle.IsValid)
            //         handles.Add(handle);
            //     handle = Unity.Logging.Builder.BuildContextSpecialType(arg0, ref memManager);

            var indxStart = methods.IndexOf("(in PayloadHandle msg, in Int32 arg0, ref LogContextWithDecorator handles)", StringComparison.Ordinal);
            Assert.IsTrue(indxStart >= 0, "Cannot find Decorate method");

            var indxArgConv = methods.IndexOf("handle = Unity.Logging.Builder.BuildContextSpecialType(arg0, ref memManager);", indxStart, StringComparison.Ordinal);
            Assert.IsTrue(indxArgConv >= 0, "Cannot find Decorate method part");

            var n = indxArgConv - indxStart;

            var indxCallConversion = methods.IndexOf("BuildMessage", indxStart, n, StringComparison.Ordinal);
            Assert.IsTrue(indxCallConversion == -1, "BuildMessage shouldn't be called here");
        }

        [Test]
        public void TestStructWithRefTypeStringAndInt()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public string S = ""foo"";
        public int I = 6502;
    }

    public void A()
    {
        Data data = new Data();
        Unity.Logging.Log.Info(""Next metasyntactic variable is {0}, value is {1}"", data.S, data.I);
    }
}
";
            var parser = ParserTests.ParseCode(testData);

            CommonUtils.GenerateCode(testData);
        }

        [Test]
        public void TestLogWithOnlyStringMsg()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public string I = 42;
        public override string ToString() { return $""The value is {I}""};
    }

    public void A()
    {
        Unity.Logging.Log.Info(new Data().ToString());
    }
}
";
            var parser = ParserTests.ParseCode(testData);

            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);
        }

        [Test]
        public void TestLogWithOnlyStringArg()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public string I = 42;
        public override string ToString() { return $""The value is {I}""};
    }

    public void A()
    {
        Unity.Logging.Log.Info(""{0}"", new Data().ToString());
    }
}
";
            var parser = ParserTests.ParseCode(testData);

            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var generator = CommonUtils.GenerateCode(testData);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);
        }

        [Test]
        public void TestStructWithMultipleArgs()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public int I1 = 42;
        public int I2 = 1337;
        public FixedString32 F = ""foobar"";
    }

    public void A()
    {
        Unity.Logging.Log.Info(new Data());
    }
}
";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            CommonUtils.GenerateCode(testData);


        }

/* XXX Disabled for now
        [Test]
        public void TestStructWithRefTypeClassUnsupported()
        {
            var testData = @"
class A
{
    public int I() { return 42; }
}

class B
{
    struct Data
    {
        public A myA
    }

    public void B()
    {
        data = new Data();
        Unity.Logging.Log.Info(myA.I);
    }
}
";
            var parser = ParserTests.ParseCode(testData);

            CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();

            Assert.AreEqual(1, errors.Length, "Expected one warning");
            Assert.IsTrue(errors[0].Id == CompilerMessages.MessageFixedStringError.Item1, $"Expected error: {CompilerMessages.MessageFixedStringError.Item2}");
        }
*/

        [Test]
        public void TestForLiteralVarConcat()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        var ssss = ""{0}"";
        sbyte aaa = 42;

        Unity.Logging.Log.Info(""{0}"" + ssss, aaa);
    }
}
";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var gen = CommonUtils.GenerateCode(testData);

            var methods = gen.methodsGenCode;

            Assert.IsTrue(methods.Contains("static void Info(string msg, in SByte arg0)"));
        }

        [Test]
        public void TestForCallAmbiguousCase()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        FixedString4096Bytes fsss = ""{0}"";
        var ssss = ""{0}"";
        sbyte aaa = 42;

        Unity.Logging.Log.Info(fsss, aaa);          // (1) FixedString4096Bytes, sbyte
        Unity.Logging.Log.Info(ssss, (int)aaa);     // (2) string, int

                                                    //     this generates (FixedString4096Bytes, sbyte)
        Unity.Logging.Log.Info(""{0}"", aaa);       // (3) literal string, sbyte.  string -implicit-> FixedString4096Bytes, but sbyte -implicit-> int. So what to pick?

      //Unity.Logging.Log.Info(ssss, aaa);          // (4) string, sbyte // uncomment to remove the error, the problem is that (3) would use 'string' and this breaks burst.
                                                    //    so we need to issue a warning in (3) to add a cast, or Info(m:""{0}"", aaa); - this 'm:' part
    }
}
";

            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(3, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(3, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var gen = CommonUtils.GenerateCode(testData);

            var methods = gen.methodsGenCode;
            Assert.IsTrue(methods.Contains("static void Info(in FixedString4096Bytes msg, in SByte arg0)"));
            Assert.IsTrue(methods.Contains(@"public static void Info(string msg, in SByte arg0)")); // This is needed for compilation to be successful, but shouldn't be used
            Assert.IsTrue(methods.Contains("static void Info(string msg, in Int32 arg0)"));
        }

        [Test]
        public void TestStructWithFixedString()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public FixedString32Bytes W = ""World"";
    }

    public void A()
    {
        Unity.Logging.Log.Info(""Hello {0}!"", new Data());
    }
}
";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            CommonUtils.GenerateCode(testData);
        }

        [Test]
        public void TestStructWithNonSerializedAttribute()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        [NonSerialized]
        public FixedString32Bytes W1 = ""World A"";
        public FixedString32Bytes W2 = ""World B"";
        [Unity.Logging.NotLogged]
        public FixedString32Bytes W3 = ""World C"";
    }

    public void A()
    {
        Unity.Logging.Log.Info(""Hello {0}!"", new Data());
    }
}
";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var gen = CommonUtils.GenerateCode(testData);

            var types = gen.typesGenCode.ToString();
            Assert.IsTrue(types.Contains("W2"));
            Assert.IsFalse(types.Contains("W1"));
            Assert.IsFalse(types.Contains("W3"));
        }

        [Test]
        public void TestStructWithProperty()
        {
            var testData = @"
class ClassA
{
    struct Data2
    {
        public int aaA;
        public int aaB {get;set}
        public int ignoreC {private get;set}
        private int aaD {public get;set}

        [field: Unity.Logging.NotLogged]
        public int ignoreE { get; set; }

        [Unity.Logging.NotLogged]
        public int ignoreF { get; set; }

        [field: NonSerialized]
        public int ignoreE2 { get; set; }

        [NonSerialized]
        public int ignoreF2 { get; set; }
    }
    struct Data
    {
        public FixedString32Bytes getSetterProperty {get; set;}
        public FixedString32Bytes getProperty {get;}
        public FixedString32Bytes setProperty {set;}
        public Data2 data2 {get;}

        private FixedString32Bytes ignoreProp {get; set;}
        private FixedString32Bytes ignore;
    }

    public void A()
    {
        Unity.Logging.Log.Info(""Hello {0}!"", new Data {getSetterProperty = ""ola""});
    }
}
";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var gen = CommonUtils.GenerateCode(testData);

            var types = gen.typesGenCode.ToString();
            Assert.IsTrue(types.Contains("getSetterProperty"));
            Assert.IsTrue(types.Contains("getProperty"));
            Assert.IsFalse(types.Contains("setProperty"));
            Assert.IsTrue(types.Contains("data2"));

            Assert.IsTrue(types.Contains("aaA"));
            Assert.IsTrue(types.Contains("aaB"));
            Assert.IsTrue(types.Contains("aaD"));


            Assert.IsFalse(types.Contains("gnore"));
        }

        [Test]
        public void TestStructWithPropertyLogWithName()
        {
            var testData = @"
class ClassA
{
    struct Data2
    {
        public int aaA;
        public int aaB {get;set}
        public int ignoreC {private get;set}
        private int aaD {public get;set}

        [field: Unity.Logging.LogWithName(""e"")]
        public int ignoreE { get; set; }

        [Unity.Logging.LogWithName(""d"")]
        public int ignoreF { get; set; }
    }
    struct Data
    {
        public FixedString32Bytes getSetterProperty {get; set;}
        public FixedString32Bytes getProperty {get;}
        [Unity.Logging.LogWithName(""nonsetProperty"")]
        public FixedString32Bytes setPropertyIgnore {set;}
        public Data2 data2 {get;}

        private FixedString32Bytes ignoreProp {get; set;}
        [Unity.Logging.LogWithName(""non"")]
        public FixedString32Bytes ignore;
    }

    public void A()
    {
        Unity.Logging.Log.Info(""Hello {0}!"", new Data {getSetterProperty = ""ola""});
    }
}
";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var gen = CommonUtils.GenerateCode(testData);

            var types = gen.typesGenCode.ToString();

            Assert.IsTrue(types.Contains("// Field name aaA"));
            Assert.IsTrue(types.Contains("// Field name aaB"));
            Assert.IsTrue(types.Contains("// Field name aaD"));
            Assert.IsTrue(types.Contains("// Field name e"));
            Assert.IsTrue(types.Contains("// Field name d"));

            Assert.IsTrue(types.Contains("// Field name getSetterProperty"));
            Assert.IsTrue(types.Contains("// Field name getProperty"));
            Assert.IsTrue(types.Contains("// Field name data2"));
            Assert.IsTrue(types.Contains("// Field name non"));
        }

        [Test]
        public void TestStructWithPropertyLogWithNameConflict()
        {
            var testData = @"
class ClassA
{
    struct Data2
    {
        public int conflict;
        public int aaB {get;set}
        public int ignoreC {private get;set}
        private int aaD {public get;set}

        [field: Unity.Logging.LogWithName(""conflict"")]
        public int ignoreE { get; set; }
    }

    public void A()
    {
        Unity.Logging.Log.Info(""Hello {0}!"", new Data2 {ignoreE = 2});
    }
}
";
            var parser = ParserTests.ParseCode(testData);
            Assert.AreEqual(1, parser.LogCalls.Count, "Cannot detect Log calls");
            Assert.AreEqual(1, parser.LogCallsLevel.Count, "Cannot detect Log calls");
            Assert.AreEqual(LogCallKind.Info, parser.LogCallsLevel[0], "Cannot detect Log calls");

            var gen = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);

            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(CompilerMessages.MessageErrorFieldNameConflict.Item1, diagnostics[0].Id);
            Assert.AreEqual(string.Format(CompilerMessages.MessageErrorFieldNameConflict.Item2, "conflict"), diagnostics[0].GetMessage());
            Assert.AreEqual(DiagnosticSeverity.Error, diagnostics[0].Severity);

            var err = diagnostics[0];
            var loc = err.Location.SourceTree.ToString().Substring(err.Location.SourceSpan.Start, err.Location.SourceSpan.Length);
            Assert.AreEqual("ignoreE", loc);
        }

        [Test]
        public void TestStructWithLong()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public long F1 = 42;
        public int F2 = 24;
    }

    public void A()
    {
        Unity.Logging.Log.Info(""Hello {0}!"", new Data());
        Unity.Logging.Log.Info(""Hello #2 {0}!"", new Data()); // same call signature as the previous one
        Log.Info(""No Using, so shouldn't detect {0}!"", new Data());
    }
}
";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.AreEqual(1, generator.structureData.StructTypes.Count);
            Assert.AreEqual(2, generator.structureData.StructTypes[0].FieldData.Count);
            Assert.AreEqual("F1", generator.structureData.StructTypes[0].FieldData[0].FieldName);
            Assert.AreEqual("F2", generator.structureData.StructTypes[0].FieldData[1].FieldName);
        }

        [Test]
        public void TestStructWithLiteralExpression()
        {
            var testData = @"
class ClassLiteral
{
    public void A()
    {
        Unity.Logging.Log.Info(""Hello {0} {1}!"", 42, ""World"");
    }
}
";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.IsNull(generator.structureData.StructTypes);
            var s = generator.methodsGenCode;

            Assert.IsTrue(s.Contains("_E_E(in PayloadHandle msg, in Int32 arg0, in PayloadHandle arg1"));

            Assert.IsTrue(s.Contains("handles.Add(msg);"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0"));
            Assert.IsTrue(s.Contains("handles.Add(arg1);"));
        }

        [Test]
        public void TestMergeCalls()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        Unity.Logging.Log.Info((FixedString32Bytes)""e92 {0}!"", (long)1);
        Unity.Logging.Log.Info((FixedString128Bytes)""e83 {0}!"", (double)4.401);
        Unity.Logging.Log.Info((FixedString512Bytes)""e74 {0}!"", (int)1);
        Unity.Logging.Log.Info((FixedString128Bytes)""e65 {0}!"", (float)1.3f);
    }
}
";

            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(4, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            foreach (var a in generator.invokeData.InvokeInstances[LogCallKind.Info])
            {
                Assert.AreEqual("FixedString512Bytes", a.MessageData.MessageType);
            }

            // Assert.AreEqual("Int64", generator.invokeData.InvokeInstances[LogLevel.Info][0].ArgumentData[0].ArgumentTypeName);
            Assert.IsTrue(generator.invokeData.IsValid);
        }

        [Test]
        public void TestProperFixedStringForMessage()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public long F1 = 42;
        public int F2 = 24;
    }

    public void A()
    {
        Unity.Logging.Log.Info(""Hello FixedString64BytesFixedString64Bytes {0}!"", new Data()); // same call signature as the previous one
        Unity.Logging.Log.Info(""Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nam et. {0}!"", new Data());
        Unity.Logging.Log.Info(""Hello #2 {0}!"", new Data()); // same call signature as the previous one

        Unity.Logging.Log.Info(""Short one"");

        int intVar = 42;
        Unity.Logging.Log.To(something).Error($""Dollar string with intVar = {intVar}"");
        Unity.Logging.Log.To(something).Error($""Dollar string"");

        Unity.Logging.Log.Info(@""should {1} detect {0}!
Lorem ipsum dolor sit amet, consectetur adipiscing elit. Maecenas elit nisl, lacinia eget ultrices vel,
commodo ac turpis. Donec in justo eget metus aliquam porttitor. In hac habitasse platea dictumst.
Phasellus ut eleifend ligula, a placerat sem. Donec molestie quam vulputate odio consequat, nec dignissim est mattis.
Proin lorem ex, pulvinar eget placerat in, consequat ut tortor. Nam pharetra efficitur lacus ac fermentum.
Donec volutpat fermentum augue, sit amet eleifend lectus commodo at quam.
"", new Data(), new Data());

        Log.Info(@""No Using, so shouldn't detect {0}!
Lorem ipsum dolor sit amet, consectetur adipiscing elit. Maecenas elit nisl, lacinia eget ultrices vel,
commodo ac turpis. Donec in justo eget metus aliquam porttitor. In hac habitasse platea dictumst.
Phasellus ut eleifend ligula, a placerat sem. Donec molestie quam vulputate odio consequat, nec dignissim est mattis.
Proin lorem ex, pulvinar eget placerat in, consequat ut tortor. Nam pharetra efficitur lacus ac fermentum.
Donec volutpat fermentum augue, sit amet eleifend lectus commodo at quam.
"", new Data());
    }
}
";

            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Error].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Error][0].MessageData.MessageType);

            Assert.AreEqual(3, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);

            var infos = generator.invokeData.InvokeInstances[LogCallKind.Info];
            Assert.AreEqual(3, infos.Count(m => m.MessageData.MessageType == "string"));
            Assert.IsTrue(generator.invokeData.IsValid);


            Assert.AreEqual(1, generator.structureData.StructTypes.Count);
            Assert.AreEqual(2, generator.structureData.StructTypes[0].FieldData.Count);
            Assert.AreEqual("F1", generator.structureData.StructTypes[0].FieldData[0].FieldName);
            Assert.AreEqual("F2", generator.structureData.StructTypes[0].FieldData[1].FieldName);
        }

        [Test]
        public void TestFixedString64BytesMessageFixedString32BytesArgument()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        FixedString64Bytes s = ""Fixed {0}"";
        FixedString32Bytes s2 = ""32"";

        Unity.Logging.Log.Info(s, s2);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("FixedString64Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);


            Assert.IsNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode;
            Assert.IsTrue(s.Contains("FixedString32Bytes arg0)")); // FixedString32Bytes is a special type
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0")); // FixedString32Bytes is a special type
        }

        [Test]
        public void TestUnsafeFieldStruct()
        {
            var testData = @"
class ClassA
{
    unsafe struct UnsafeStruct
    {
        public byte* ptr;
    }

    public void A()
    {
        FixedString64Bytes s = ""Fixed {0}"";
        Unity.Logging.Log.Info(s, new UnsafeStruct());
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("FixedString64Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);

            var types = generator.typesGenCode.ToString();
            Assert.IsTrue(types.Contains("public IntPtr ptr;"));
            Assert.IsTrue(types.Contains("internal struct UnsafeStruct_"));

            Assert.IsTrue(types.Contains("public unsafe static implicit operator UnsafeStruct_"));
            Assert.IsTrue(types.Contains("ptr = new IntPtr(arg.ptr)"));
        }

        [Test]
        public void TestUnsafeArgument()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        FixedString64Bytes s = ""Fixed {0}"";
        unsafe {
            long longV = 2134145123;
            Unity.Logging.Log.Info(s, &longV);
        }
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("FixedString64Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.IsTrue(generator.typesGenCode.Length == 0);
            Assert.IsTrue(generator.methodsGenCode.Contains("in FixedString64Bytes msg, IntPtr arg0, ref LogController logController"));
            Assert.IsTrue(generator.methodsGenCode.Contains("public unsafe static void Info(in FixedString64Bytes msg, global::System.Int64* arg0)"));

            Assert.IsFalse(generator.methodsGenCode.Contains("handle = Unity.Logging.Builder.BuildContext(arg0, ref memManager);")); // FALSE
            Assert.IsTrue(generator.methodsGenCode.Contains("handle = Unity.Logging.Builder.BuildContextSpecialType(arg0, ref memManager);"));
        }

        [Test]
        public void TestFixedStringAndStructWithUnblittableBool()
        {
            var testData = @"
class ClassA
{
    struct UnblittableStruct
    {
        public FixedString32Bytes a;
        public bool b;
        public bool c;
        public System.Char d;
    }

    public void A()
    {
        FixedString64Bytes s = ""Fixed {0}"";
        Unity.Logging.Log.Info(s, new UnblittableStruct());

        Unity.Logging.Log.Info(s, true);
        System.Char ch = 'c';
        Unity.Logging.Log.Info(s, ch);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(3, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("FixedString64Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);


            Assert.IsNotNull(generator.structureData.StructTypes);
            Assert.AreEqual(1, generator.structureData.StructTypes.Count);
            Assert.AreEqual(4, generator.structureData.StructTypes[0].FieldData.Count);

            Assert.AreEqual(SpecialType.System_Boolean, generator.structureData.StructTypes[0].FieldData[1].Symbol.Type.SpecialType);
            Assert.AreEqual(SpecialType.System_Boolean, generator.structureData.StructTypes[0].FieldData[2].Symbol.Type.SpecialType);
            Assert.AreEqual(SpecialType.System_Char, generator.structureData.StructTypes[0].FieldData[3].Symbol.Type.SpecialType);

            var s1 = generator.methodsGenCode;
            var s2 = generator.typesGenCode.ToString();

            var linesWithBurstedFuncSignatures = s1.Split('\n').Where(s => s.Contains("static void") && s.Contains("BurstedInfo")).ToList();

            foreach (var s in linesWithBurstedFuncSignatures)
            {
                if (s.Contains("global::System.Char arg"))
                    Assert.Fail();
                if (s.Contains("global::System.Boolean arg"))
                    Assert.Fail();
                if (s.Contains("char arg"))
                    Assert.Fail();
                if (s.Contains("bool arg"))
                    Assert.Fail();
            }

            int indx = 0;

            Assert.IsTrue(s2.Contains("System.Char d") == false);
            Assert.IsTrue(s2.Contains("char d") == false);

            indx = s2.IndexOf("public byte b;", indx, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(indx != -1);
            indx = s2.IndexOf("public byte c;", indx, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(indx != -1);
            indx = s2.IndexOf("public int d;", indx, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(indx != -1);
        }

        [Test]
        public void TestFixedString128BytesMessageStructWithFixedString32BytesArgument()
        {
            var testData = @"
public struct LogComplexC
{
    public FixedString32Bytes C1;
}

public struct LogComplexFS512
{
    public FixedString512Bytes String512;
}
class ClassA
{
    public void A()
    {
        LogComplexC c;
        c.C1 = ""AAAAAA"";
        FixedString128Bytes s = ""Fixed {0}"";
        var ac512 = new LogComplexFS512 {String512 = ""String512""};
        Unity.Logging.Log.Info(s, c);
        Unity.Logging.Log.Info(""T: {0}"", ac512);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(2, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("FixedString128Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][1].MessageData.MessageType);
            // This was disabled, to fix ambiguous issue if we have a lot of Log. calls
            //Assert.AreEqual("FixedString32Bytes", generator.invokeData.InvokeInstances[LogLevel.Info][1].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.AreEqual(2, generator.structureData.StructTypes.Count);

            Assert.AreEqual(1, generator.structureData.StructTypes[0].FieldData.Count);
            Assert.AreEqual("C1", generator.structureData.StructTypes[0].FieldData[0].FieldName);
            Assert.AreEqual(1, generator.structureData.StructTypes[1].FieldData.Count);
            Assert.AreEqual("String512", generator.structureData.StructTypes[1].FieldData[0].FieldName);


            var s = generator.typesGenCode.ToString();
            Assert.IsTrue(s.Contains("::FixedString32Bytes C1;"));
            Assert.IsTrue(s.Contains("::FixedString512Bytes String512;"));
            Assert.IsTrue(s.Contains("formatter.WriteProperty(ref output, \"C1\", C1"));
            Assert.IsTrue(s.Contains("formatter.WriteProperty(ref output, \"String512\", String512"));
        }

        [Test]
        public void TestEmptyStructArg()
        {
            var testData = @"
public struct LogEmpty
{
}
public struct LogComplexEmpty
{
    public LogEmpty a;
    public LogEmpty b;
    public LogEmpty c;
}

class ClassA
{
    public void A()
    {
        LogEmpty e;
        LogComplexEmpty c;
        Unity.Logging.Log.Info(""T: {0}"", e);
        Unity.Logging.Log.Info(""T: {0}"", c);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(2, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.AreEqual(2, generator.structureData.StructTypes.Count);
        }

        [Test]
        public void TestComplexStructA()
        {
            var testData = @"
        public struct LogComplexA
        {
            public struct LogComplexB
            {
                public long A1;
            }
            public struct LogComplexC
            {
                public FixedString32Bytes C1;
            }
            public struct LogComplexD
            {

            }

            public struct LogComplexFS512
            {
                public FixedString512Bytes String512;
            }
            public struct LogComplexF
            {
                public LogComplexB bb;
                public LogComplexC cc;
                public LogComplexD dd;
            }

            public long A1;
            public LogComplexB A2;
            public LogComplexC A3;
            public LogComplexD A4;
            public LogComplexF A5;
        }

class ClassA
{
    public void A()
    {
        var ca = new LogComplexA();
        ca.A1 = 1;
        ca.A2 = new LogComplexA.LogComplexB {A1 = 2};
        ca.A3 = new LogComplexA.LogComplexC {C1 = ""LogComplexA.LogComplexC""};
        ca.A5 = new LogComplexA.LogComplexF
            {bb = new LogComplexA.LogComplexB {A1 = 11}, cc = new LogComplexA.LogComplexC {C1 = ""C1""}};

        Unity.Logging.Log.Info(""T: {0}"", ca);
        Unity.Logging.Log.Info(""T: {0}"", ca);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.AreEqual(5, generator.structureData.StructTypes.Count);

            var s = generator.typesGenCode.ToString();
            Assert.IsTrue(s.Contains("formatter.WriteChild(ref output, \"bb\", ref bb, ref memAllocator, ref currArgSlot, depth + 1)"));
            Assert.IsTrue(s.Contains("formatter.WriteChild(ref output, \"cc\", ref cc, ref memAllocator, ref currArgSlot, depth + 1)"));
            Assert.IsTrue(s.Contains("formatter.WriteChild(ref output, \"dd\", ref dd, ref memAllocator, ref currArgSlot, depth + 1)"));

            Assert.IsTrue(s.Contains("formatter.WriteProperty(ref output, \"A1\", A1"));
            Assert.IsTrue(s.Contains("formatter.WriteChild(ref output, \"A2\", ref A2, ref memAllocator, ref currArgSlot, depth + 1)"));
            Assert.IsTrue(s.Contains("formatter.WriteChild(ref output, \"A3\", ref A3, ref memAllocator, ref currArgSlot, depth + 1)"));
            Assert.IsTrue(s.Contains("formatter.WriteChild(ref output, \"A4\", ref A4, ref memAllocator, ref currArgSlot, depth + 1)"));
            Assert.IsTrue(s.Contains("formatter.WriteChild(ref output, \"A5\", ref A5, ref memAllocator, ref currArgSlot, depth + 1)"));

        }

        [Test]
        public void TestValueTupleA()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        var person = (Id:1, FirstName: (FixedString32Bytes)""dawda"", LastName: (FixedString32Bytes)""dafwsgwegfsa"");

        Unity.Logging.Log.Info(""T: {0}"", person);
        Unity.Logging.Log.Info(""T: {0}"", (a:1, b:2, c:(z:42, y:11)));
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(2, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.AreEqual(3, generator.structureData.StructTypes.Count);

            // public static implicit operator ValueTuple_mNBpORr8KlT8pO3zakjDEw__(in (global::System.Int32,global::FixedString32Bytes,global::FixedString32Bytes) arg)
            // {
            //     return new ValueTuple_mNBpORr8KlT8pO3zakjDEw__
            //     {
            //         __Internal_Unity_TextLogger_Struct_TypeId__ = ValueTuple_mNBpORr8KlT8pO3zakjDEw__.ValueTuple_mNBpORr8KlT8pO3zakjDEw___TypeIdValue,
            //         Id = arg.Id,
            //         FirstName = arg.FirstName,
            //         LastName = arg.LastName,
            //     };

            // public static implicit operator ValueTuple_R4nMzZ4MmHfoBAI4sN_H1A__(in (global::System.Int32,global::System.Int32,(global::System.Int32,global::System.Int32)) arg)
            // {
            //     return new ValueTuple_R4nMzZ4MmHfoBAI4sN_H1A__
            //     {
            //         __Internal_Unity_TextLogger_Struct_TypeId__ = ValueTuple_R4nMzZ4MmHfoBAI4sN_H1A__.ValueTuple_R4nMzZ4MmHfoBAI4sN_H1A___TypeIdValue,
            //         a = arg.a,
            //         b = arg.b,
            //         c = arg.c,
            //     };
            // }

            // public static implicit operator _V6E1XpO_Ly2Lo3_9lG86Yw__(in (global::System.Int32,global::System.Int32) arg)
            // {
            //     return new _V6E1XpO_Ly2Lo3_9lG86Yw__
            //     {
            //         __Internal_Unity_TextLogger_Struct_TypeId__ = _V6E1XpO_Ly2Lo3_9lG86Yw__._V6E1XpO_Ly2Lo3_9lG86Yw___TypeIdValue,
            //         z = arg.z,
            //         y = arg.y,
            //     };
            var typesStr = generator.typesGenCode.ToString();
            Assert.IsTrue(typesStr.Contains("in (global::System.Int32,global::FixedString32Bytes,global::FixedString32Bytes) arg"));
            Assert.IsTrue(typesStr.Contains("Id = arg.Item1"));
            Assert.IsTrue(typesStr.Contains("FirstName = arg.Item2"));
            Assert.IsTrue(typesStr.Contains("LastName = arg.Item3"));

            Assert.IsTrue(typesStr.Contains("a = arg.Item1"));
            Assert.IsTrue(typesStr.Contains("b = arg.Item2"));
            Assert.IsTrue(typesStr.Contains("c = arg.Item3"));

            Assert.IsTrue(typesStr.Contains("in (global::System.Int32,global::System.Int32) arg"));
            Assert.IsTrue(typesStr.Contains("z = arg.Item1"));
            Assert.IsTrue(typesStr.Contains("y = arg.Item2"));
        }

        [Test]
        public void TestStringPermutation()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        Unity.Logging.Log.Info(""{0} {1} {2} {3} {4} {5}"", ""a"", ""b"", ""c"", ""d"", ""e"", ""f"");
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances.Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            {
                var invInfo = generator.invokeData.InvokeInstances[LogCallKind.Info];
                Assert.AreEqual(1, invInfo.Count);

                Assert.AreEqual("string", invInfo[0].MessageData.MessageType);
                Assert.IsTrue(invInfo[0].ArgumentData.All(a => a.ArgumentTypeName == "string"));
            }
        }

        [Test]
        public void TestStringPermutationString1()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        string s = ""c"";
        Unity.Logging.Log.Info(""{0} {1} {2} {3} {4} {5}"", ""a"", ""b"", ""c"", ""d"", ""e"", ""f"");
        Unity.Logging.Log.Info(""{0} {1} {2} {3} {4} {5}"", ""a"", ""b"", s, ""d"", ""e"", ""f"");
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances.Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            {
                var invInfo = generator.invokeData.InvokeInstances[LogCallKind.Info];
                Assert.AreEqual(1, invInfo.Count);

                Assert.AreEqual("string", invInfo[0].MessageData.MessageType);
                Assert.IsTrue(invInfo[0].ArgumentData.All(a => a.ArgumentTypeName == "string"));
            }
        }

        [Test]
        public void TestFixedStringsArg()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        Unity.Logging.Log.Info(""A1{0}"", ""B1""); // DST-384 bug
        Unity.Logging.Log.Info(""A2{0}"", (FixedString32Bytes)""B2"");
        Unity.Logging.Log.Info(""A3{0}"", (FixedString64Bytes)""B3"");
        Unity.Logging.Log.Info(""A4{0}"", (FixedString128Bytes)""B4"");
        Unity.Logging.Log.Info(""A5{0}"", (FixedString512Bytes)""B5"");
        Unity.Logging.Log.Info(""A6{0}"", (FixedString4096Bytes)""B6"");
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances.Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            {
                var invInfo = generator.invokeData.InvokeInstances[LogCallKind.Info];
                Assert.AreEqual(2, invInfo.Count);

                var a = invInfo.First(m => m.MessageData.MessageType == "string" && m.ArgumentData[0].ArgumentTypeName == "FixedString4096Bytes");
                var b = invInfo.First(m => m.MessageData.MessageType == "string" && m.ArgumentData[0].ArgumentTypeName == "string");

                Assert.IsNotNull(a);
                Assert.IsNotNull(b);
            }
        }

        [Test]
        public void TestFixedStringsArg2()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        string s = ""2{0}""
        Unity.Logging.Log.Info(s, ""B1""); // DST-384 bug
        Unity.Logging.Log.Info(s, (FixedString32Bytes)""B2"");
        Unity.Logging.Log.Info(s, (FixedString64Bytes)""B3"");
        Unity.Logging.Log.Info(s, (FixedString128Bytes)""B4"");
        Unity.Logging.Log.Info(s, (FixedString512Bytes)""B5"");
        Unity.Logging.Log.Info(s, 2);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances.Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            // string, fixed
            // string, string,
            // string, int

            var invInfo = generator.invokeData.InvokeInstances[LogCallKind.Info];
            Assert.AreEqual(3, invInfo.Count);

            Assert.IsNotNull(invInfo.First(m => m.MessageData.MessageType == "string" && m.ArgumentData[0].ArgumentTypeName == "FixedString512Bytes"));
            Assert.IsNotNull(invInfo.First(m => m.MessageData.MessageType == "string" && m.ArgumentData[0].ArgumentTypeName == "string"));
            Assert.IsNotNull(invInfo.First(m => m.MessageData.MessageType == "string" && m.ArgumentData[0].ArgumentTypeName == "Int32"));
        }

        [Test]
        public void TestUnsafeStringsMessage()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        var nt = new NativeText(64, Allocator.Persistent);
        var ut = new UnsafeText(64, Allocator.Persistent);

        Unity.Logging.Log.Info(nt, 3, ut);
        Unity.Logging.Log.Info(ut, 4, nt);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(2, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("NativeText", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.AreEqual("UnsafeText", generator.invokeData.InvokeInstances[LogCallKind.Info][1].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.IsNull(generator.structureData.StructTypes);

            var m = generator.methodsGenCode;

            Assert.IsNotEmpty(m);

            // Bursted (in NativeText -- cannot be in the burst entry function - it has dispose sentinel
            Assert.IsTrue(m.Contains("_E_E(in NativeTextBurstWrapper msg,"));
            Assert.IsFalse(m.Contains("_E_E(in NativeText msg,"));
            Assert.IsFalse(m.Contains("_E_E(NativeText msg,"));

            Assert.IsFalse(m.Contains("CopyStringToPayloadBuffer")); // no CopyStringToPayloadBuffer
        }

        [Test]
        public void TestUnsafeStringsArg1()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        var nt = new NativeText(64, Allocator.Persistent);
        var ut = new UnsafeText(64, Allocator.Persistent);
        var s = ""{0}"";
        Unity.Logging.Log.Info(s, nt);
        Unity.Logging.Log.Info(s, ut);
        Unity.Logging.Log.Info(s, (a:(FixedString128Bytes)""B4"", b:nt));
        Unity.Logging.Log.Info(s, (c:ut, d:(FixedString512Bytes)""B5""));
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances.Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            var invInfo = generator.invokeData.InvokeInstances[LogCallKind.Info];
            Assert.AreEqual(4, invInfo.Count);

            Assert.AreEqual(1, invInfo[0].ArgumentData.Count);
            Assert.IsTrue(invInfo[0].ArgumentData[0].IsSpecialSerializableType());
            Assert.AreEqual("NativeText", invInfo[0].ArgumentData[0].ArgumentTypeName); // all strings merge into one, to avoid ambiguity

            Assert.AreEqual(1, invInfo[1].ArgumentData.Count);
            Assert.IsTrue(invInfo[1].ArgumentData[0].IsSpecialSerializableType());
            Assert.AreEqual("UnsafeText", invInfo[1].ArgumentData[0].ArgumentTypeName);

            var methods = generator.methodsGenCode;

            {
                var indxBegin = methods.IndexOf("in PayloadHandle msg, in UnsafeText arg0", StringComparison.Ordinal);
                var indxEnd = methods.IndexOf("CreateDisjointedPayloadBufferFromExistingPayloads", indxBegin, StringComparison.Ordinal);
                var n = indxEnd - indxBegin;

                // if (msg.IsValid)
                //     handles.Add(msg);
                // ...
                // handle = Unity.Logging.Builder.BuildContextSpecialType(arg0, ref memManager);

                var foundA = methods.IndexOf("handles.Add(msg);", indxBegin, n, StringComparison.Ordinal);
                Assert.AreNotEqual(-1, foundA, "handles.Add(msg); not found");

                var foundB = methods.IndexOf("Unity.Logging.Builder.BuildContextSpecialType(arg0", indxBegin, n, StringComparison.Ordinal);
                Assert.AreNotEqual(-1, foundB, "Unity.Logging.Builder.BuildContextSpecialType(arg0 not found");
            }

            var types = generator.typesGenCode.ToString();

            Assert.IsTrue(Regex.Match(types, "c = .*CopyCollectionStringToPayloadBuffer").Success);

            {
                var indxBegin = types.IndexOf("FixedString128Bytes a;", StringComparison.Ordinal);
                var indxEnd = types.IndexOf("return success;", indxBegin, StringComparison.Ordinal);
                var n = indxEnd - indxBegin;

                var foundA = types.IndexOf("formatter.WriteProperty(ref output, \"b\", b, ref memAllocator", indxBegin, n, StringComparison.Ordinal);
                Assert.AreNotEqual(-1, foundA, "formatter.WriteProperty(ref output, \"b\", b, ref memAllocator");

                var foundB = types.IndexOf("PayloadHandle b;", indxBegin, n, StringComparison.Ordinal);
                Assert.AreNotEqual(-1, foundB, "PayloadHandle b;");
            }

            {
                var indxBegin = types.IndexOf("PayloadHandle c", StringComparison.Ordinal);
                var indxEnd = types.IndexOf("return success;", indxBegin, StringComparison.Ordinal);
                var n = indxEnd - indxBegin;

                var foundA = types.IndexOf("formatter.WriteProperty(ref output, \"c\", c, ref memAllocator", indxBegin, n, StringComparison.Ordinal);
                Assert.AreNotEqual(-1, foundA, "formatter.WriteProperty(ref output, \"c\", c, ref memAllocator");
            }
            // ---

            // public PayloadHandle c;
            // public global::FixedString512Bytes d;
            //
            // public bool AppendToUnsafeText(ref UnsafeText output, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot)
            // {
            //     bool success = true;
            //
            // success = output.Append((FixedString32Bytes)"[") == FormatError.None && success;
            // success = output.Append((FixedString32Bytes)"\"") == FormatError.None && success;
            // success = output.Append((FixedString32Bytes)""\"""") == FormatError.None && success;
            // success = Unity.Logging.Builder.AppendStringAsPayloadHandle(ref output, {currField.FieldName}, ref memAllocator) && success;
            // success = output.Append((FixedString32Bytes)""\"""") == FormatError.None && success;
            // success = output.Append((FixedString32Bytes)"\"") == FormatError.None && success;
            // success = output.Append((FixedString32Bytes)", ") == FormatError.None && success;
            // success = output.Append(d) == FormatError.None && success;
            // success = output.Append((FixedString32Bytes)"]") == FormatError.None && success;
            //
            // return success;
            // }

            {
                var indxBegin = types.IndexOf("public PayloadHandle c;", StringComparison.Ordinal);
                var indxEnd = types.IndexOf("return success", indxBegin, StringComparison.Ordinal);
                var n = indxEnd - indxBegin;

                var foundA = types.IndexOf("formatter.WriteProperty(ref output, \"c\", c, ref memAllocator", indxBegin, n, StringComparison.Ordinal);
                var foundB = types.IndexOf("formatter.WriteProperty(ref output, \"d\", d", indxBegin, n, StringComparison.Ordinal);

                Assert.AreNotEqual(-1, foundA, "formatter.WriteProperty(ref output, \"c\", c, ref memAllocator");
                Assert.AreNotEqual(-1, foundB, "formatter.WriteProperty(ref output, \"d\", d");
            }
        }

        [Test]
        public void TestFixedString64BytesMessageIntArgumentLiteral()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        FixedString64Bytes s = ""Fixed {0}"";
        Unity.Logging.Log.Info(s, 43);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("FixedString64Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.IsNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode;
            Assert.IsTrue(s.Contains("Int32 arg0)"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0"));
        }

        [Test]
        public void TestStringBuilder()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        var sb = new System.Text.StringBuilder();

        Unity.Logging.Log.Error(sb.ToString(), sb);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.IsTrue(generator.invokeData.InvokeInstances[LogCallKind.Error][0].MessageData.IsManagedString);
            Assert.IsTrue(generator.invokeData.InvokeInstances[LogCallKind.Error][0].MessageData.IsNonLiteralString);
            Assert.IsTrue(generator.invokeData.InvokeInstances[LogCallKind.Error][0].MessageData.ShouldUsePayloadHandle);

            Assert.IsFalse(generator.invokeData.InvokeInstances[LogCallKind.Error][0].ArgumentData[0].IsManagedString);
            Assert.IsTrue(generator.invokeData.InvokeInstances[LogCallKind.Error][0].ArgumentData[0].IsConvertibleToString);
            Assert.IsTrue(generator.invokeData.InvokeInstances[LogCallKind.Error][0].ArgumentData[0].ShouldUsePayloadHandle);

            var m = generator.methodsGenCode;
            Assert.IsTrue(m.Contains("Error(string msg, in global::System.Text.StringBuilder arg0)"));
        }

        void TestTwoCallsWithDifferentClassesCheck(LoggingSourceGenerator generator)
        {
            Assert.AreEqual(2, generator.invokeData.InvokeInstances[LogCallKind.Error].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Error][0].MessageData.MessageType);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Error][1].MessageData.MessageType);
            Assert.AreEqual("{0}", generator.invokeData.InvokeInstances[LogCallKind.Error][0].MessageData.LiteralValue);
            Assert.AreEqual("{0}", generator.invokeData.InvokeInstances[LogCallKind.Error][1].MessageData.LiteralValue);

            var class1Arg = generator.invokeData.InvokeInstances[LogCallKind.Error][0].ArgumentData[0];
            var struct1Arg = generator.invokeData.InvokeInstances[LogCallKind.Error][1].ArgumentData[0];

            Assert.AreEqual("Class1", class1Arg.ArgumentTypeName);
            Assert.AreEqual("Struct1", struct1Arg.ArgumentTypeName);

            Assert.IsTrue(class1Arg.IsConvertibleToString);
            Assert.IsFalse(struct1Arg.IsConvertibleToString);

            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.IsNotNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode;
            Assert.IsTrue(s.Contains("CopyStringToPayloadBuffer(arg0.ToString()"));

        }

        [Test]
        public void TestTwoCallsWithDifferentClassesOmitMessage()
        {
            var testData = @"
internal class Class1 {}
internal struct Struct1 {}

class ClassA
{
    public void A()
    {
        var c = new Class1();
        Unity.Logging.Log.Error(c);

        var s = new Struct1();
        Unity.Logging.Log.Error(s);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            var m = generator.methodsGenCode;
            Assert.IsTrue(m.Contains("Error(in global::Class1 arg0)"));
            Assert.IsTrue(m.Contains("Error(in global::Struct1"));

            Assert.IsFalse(m.Contains("Error(string msg, in global::Class1 arg0)"));
            Assert.IsFalse(m.Contains("Error(string msg, in global::Unity.Logging.Struct1__"));

            Assert.IsTrue(m.Contains("BuildMessage(\"{0}\", ref memManager);"));

            Assert.IsFalse(m.Contains("payloadHandle_msg"));


            TestTwoCallsWithDifferentClassesCheck(generator);
        }

        [Test]
        public void TestTwoCallsWithDifferentClasses()
        {
            var testData = @"
internal class Class1 {}
internal struct Struct1 {}

class ClassA
{
    public void A()
    {
        var c = new Class1();
        Unity.Logging.Log.Error(""{0}"", c);

        var s = new Struct1();
        Unity.Logging.Log.Error(""{0}"", s);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            var m = generator.methodsGenCode;
            Assert.IsFalse(m.Contains("Error(in global::Class1 arg0)"));
            Assert.IsFalse(m.Contains("Error(in global::Unity.Logging.Struct1__"));

            Assert.IsTrue(m.Contains("Error(string msg, in global::Class1 arg0)"));
            Assert.IsTrue(m.Contains("Error(string msg, in global::Struct1"));

            TestTwoCallsWithDifferentClassesCheck(generator);
        }

        [Test]
        public void TestLoggerAndLoggerHandleCalls()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        // should ignore this
        var a = new LoggerConfig()
                .MinimumLevel.Debug()
                .WriteTo.TestLogger(outputTemplate: ""{Message}"")
                .WriteTo.Console(LogLevel.Verbose)
                .MinimumLevel.Warning();

        SelfLog.Error(""should ignore"");


        FixedString64Bytes s = ""Fixed {0}"";
        Unity.Logging.Logger l;
        var l2 = l;
        AAA.Log.To(l).Info(s, 43);
        Unity.Logging.Log.To(l2).Fatal(""Hello"");
        Unity.Logging.Log.SomeError().To(l2).Nope().Debug(""Hello"");
        Unity.Logging.Log.SomeError.Debug(""Hello {0} {1}"", 1, 2);
        Unity.Logging.Log.To(l2).Debug(""wrong"").HelloWrongLog();
        var l3 = l2;
        Unity.Logging.Log.To(l3.Handler).Error(""123123"");
        AAA.Log.To(l3.Handler).Error(""12312312312312312312312313131231231231231231231231231231231231231231313123123123123123"");
    }
}";
            var generator = CommonUtils.GenerateCodeWithPrefix("using AAA = Unity.Logging;", testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Error].Count);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Fatal].Count);
            Assert.AreEqual(2, generator.invokeData.InvokeInstances[LogCallKind.Debug].Count);


            Assert.AreEqual("FixedString64Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);

            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Error][0].MessageData.MessageType);

            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Fatal][0].MessageData.MessageType);

            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Debug][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.IsNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode;
            Assert.IsTrue(s.Contains("Int32 arg0)"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0"));
        }

        [Test]
        public void TestLoggerHandleAndOmittedMessage()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        Unity.Logging.Logger l3;
        // omitted messages
        Unity.Logging.Log.To(l3.Handler).Verbose(1, 2, 3);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Verbose].Count);

            {
                // Unity.Logging.Log.Verbose(l3.Handler, 1, 2, 3);
                var verboseLog = generator.invokeData.InvokeInstances[LogCallKind.Verbose][0];
                Assert.AreEqual("string", verboseLog.MessageData.MessageType);
                Assert.AreEqual("{0} {1} {2}", verboseLog.MessageData.LiteralValue);

                Assert.AreEqual(3, verboseLog.ArgumentData.Count);
                Assert.AreEqual("1", verboseLog.ArgumentData[0].LiteralValue);
                Assert.AreEqual("2", verboseLog.ArgumentData[1].LiteralValue);
                Assert.AreEqual("3", verboseLog.ArgumentData[2].LiteralValue);
            }

            Assert.IsTrue(generator.invokeData.IsValid);
            Assert.IsNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode;
            Assert.IsTrue(s.Contains("Int32 arg0"));
            Assert.IsTrue(s.Contains("Int32 arg1"));
            Assert.IsTrue(s.Contains("Int32 arg2"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg1"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg2"));
        }

        [Test]
        public void TestLoggerHandle1()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        Unity.Logging.Logger l3;
        Unity.Logging.Log.To(l3).Verbose(""ABC{0}"", ""ZZZ"");
        Unity.Logging.Log.To(l3.Handler).Verbose(""ABC{0}"", ""ZZZ"");
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Verbose].Count);

            {
                // Unity.Logging.Log.Verbose(l3.Handler, str, 1, 2, 3);
                var verboseLog = generator.invokeData.InvokeInstances[LogCallKind.Verbose][0];
                Assert.AreEqual("string", verboseLog.MessageData.MessageType);
                Assert.AreEqual("ABC{0}", verboseLog.MessageData.LiteralValue);

                Assert.AreEqual(1, verboseLog.ArgumentData.Count);
                Assert.AreEqual("ZZZ", verboseLog.ArgumentData[0].LiteralValue);
            }

            Assert.IsTrue(generator.invokeData.IsValid);
        }

        [Test]
        public void TestLoggerMessageAsObject()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        Unity.Logging.Logger l3;
        object a;
        Unity.Logging.Log.To(l3).Verbose(a, ""ZZZ"");
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Verbose].Count);

            // omitted
            {
                var verboseLog = generator.invokeData.InvokeInstances[LogCallKind.Verbose][0];
                Assert.AreEqual("string", verboseLog.MessageData.MessageType);
                Assert.AreEqual("{0} {1}", verboseLog.MessageData.LiteralValue);

                Assert.AreEqual(2, verboseLog.ArgumentData.Count);
                Assert.AreEqual("Object", verboseLog.ArgumentData[0].ArgumentTypeName);

                Assert.AreEqual("string", verboseLog.ArgumentData[1].ArgumentTypeName);
                Assert.AreEqual("ZZZ", verboseLog.ArgumentData[1].LiteralValue);
            }

            Assert.IsTrue(generator.invokeData.IsValid);
        }

        [Test]
        public void TestLoggerMessageSpecialTypeArgs()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        Unity.Logging.Log.Info(""Short message with bool {0}"", true);
        Unity.Logging.Log.Info(""Short message with sbyte {0}"", (sbyte)42);
        Unity.Logging.Log.Info(""Short message with byte {0}"", (byte)42);
        Unity.Logging.Log.Info(""Short message with short {0}"", (short)42);
        Unity.Logging.Log.Info(""Short message with ushort {0}"", (ushort)42);
        Unity.Logging.Log.Info(""Short message with int {0}"", (int)42);
        Unity.Logging.Log.Info(""Short message with uint {0}"", (uint)42);
        Unity.Logging.Log.Info(""Short message with long {0}"", (long)42);
        Unity.Logging.Log.Info(""Short message with ulong {0}"", (ulong)42);
        Unity.Logging.Log.Info(""Short message with char {0}"", (char)42);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(10, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);

            Assert.IsTrue(generator.invokeData.IsValid);
        }

        [Test]
        public void TestLoggerAndLoggerHandleCalls2()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        // should ignore this
        var a = new LoggerConfig()
                .MinimumLevel.Debug()
                .WriteTo.TestLogger(outputTemplate: ""{Message}"")
                .WriteTo.Console(LogLevel.Verbose)
                .MinimumLevel.Warning();

        SelfLog.Error(""should ignore"");


        FixedString64Bytes s = ""Fixed {0}"";
        Unity.Logging.Logger l;
        var l2 = l;
        AAA.Log.To(l).Info(s, 43);
        Unity.Logging.Log.To(l2).Fatal(""Hello"");
        Unity.Logging.Log.SomeError.Debug(""Hello {0} {1}"", 1, 2);
        var l3 = l2;
        Unity.Logging.Log.To(l3.Handler).Error(""123123"");

        // omitted messages
        Unity.Logging.Log.To(l3.Handler).Verbose(1, 2, 3);
        Unity.Logging.Log.Verbose(3, '3');

        AAA.Log.Error(""12312312312312312312312313131231231231231231231231231231231231231231313123123123123123"");
    }
}";
            var generator = CommonUtils.GenerateCodeWithPrefix("using AAA = Unity.Logging;", testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Error].Count);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Fatal].Count);
            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Debug].Count);
            Assert.AreEqual(2, generator.invokeData.InvokeInstances[LogCallKind.Verbose].Count);


            {
                // AAA.Log.Info(l, s, 43);
                var infoLog = generator.invokeData.InvokeInstances[LogCallKind.Info][0];
                Assert.AreEqual("FixedString64Bytes", infoLog.MessageData.MessageType); // s is FixedString64Bytes
                Assert.AreEqual(1, infoLog.ArgumentData.Count);                    // 43
                Assert.AreEqual("43", infoLog.ArgumentData[0].LiteralValue);
                Assert.AreEqual(null, infoLog.MessageData.LiteralValue);
            }

            {
                // AAA.Log.Error(""12312312312312312312312313131231231231231231231231231231231231231231313123123123123123"");
                // Unity.Logging.Log.Error(l3.Handler, ""123123"");
                var errorLogLit = generator.invokeData.InvokeInstances[LogCallKind.Error][0];
                Assert.AreEqual("string", errorLogLit.MessageData.MessageType); // 12312312312312312312312313131231231231231231231231231231231231231231313123123123123123
                Assert.AreEqual(0, errorLogLit.ArgumentData.Count);
            }

            {
                // Unity.Logging.Log.Fatal(l2, ""Hello"");
                var fatalLog = generator.invokeData.InvokeInstances[LogCallKind.Fatal][0];
                Assert.AreEqual("string", fatalLog.MessageData.MessageType);
                Assert.AreEqual(0, fatalLog.ArgumentData.Count);
                Assert.AreEqual("Hello", fatalLog.MessageData.LiteralValue);
            }


            {
                {
                    // Unity.Logging.Log.Verbose(l3.Handler, 1, 2, 3);
                    var verboseLogLit = generator.invokeData.InvokeInstances[LogCallKind.Verbose][0];
                    Assert.AreEqual("string", verboseLogLit.MessageData.MessageType);
                    Assert.AreEqual("{0} {1} {2}", verboseLogLit.MessageData.LiteralValue);

                    Assert.AreEqual(3, verboseLogLit.ArgumentData.Count);
                    Assert.AreEqual("1", verboseLogLit.ArgumentData[0].LiteralValue);
                    Assert.AreEqual("2", verboseLogLit.ArgumentData[1].LiteralValue);
                    Assert.AreEqual("3", verboseLogLit.ArgumentData[2].LiteralValue);
                }
                {
                    var verboseLogStr = generator.invokeData.InvokeInstances[LogCallKind.Verbose][1];
                    Assert.AreEqual("string", verboseLogStr.MessageData.MessageType);
                    Assert.AreEqual("{0} {1}", verboseLogStr.MessageData.LiteralValue);

                    Assert.AreEqual(2, verboseLogStr.ArgumentData.Count);
                    Assert.AreEqual("3", verboseLogStr.ArgumentData[0].LiteralValue);
                    Assert.AreEqual("3", verboseLogStr.ArgumentData[1].LiteralValue);
                }
            }

            Assert.IsTrue(generator.invokeData.IsValid);
            Assert.IsNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode;
            Assert.IsTrue(s.Contains("Int32 arg0)"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0"));
        }

        [Test]
        public void TestFixedString64BytesMessageFuncCallArgument()
        {
            var testData = @"
class ClassA
{
    public int prop {get; set;}
    public long func() { return 100; }
    public void A()
    {
        FixedString64Bytes s = ""Fixed {0} {1}"";
        Unity.Logging.Log.Info(s, prop, func());
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("FixedString64Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);


            Assert.IsNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode;
            Assert.IsTrue(s.Contains("Int32 arg0"));
            Assert.IsTrue(s.Contains("Int64 arg1"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg1"));
        }

        [Test]
        public void TestFixedString64BytesMessageIntArgument()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        FixedString64Bytes s = ""Fixed {0}"";
        int ii = 45;
        Unity.Logging.Log.Info(s, ii);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("FixedString64Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);


            Assert.IsNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode;
            Assert.IsTrue(s.Contains("Int32 arg0)"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0"));
        }

        [Test]
        public void TestFixedString32BytesMessageAnon2Argument()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        Unity.Logging.Log.Info(""T: {0}"", (Id: 10, Position: (1, 2, 3)));
        Unity.Logging.Log.Info(""T: {0}"", (210, (1211, 1232, 3123)));
        Unity.Logging.Log.Info(""T: {0}"", (210, (1211, 1232, 3123, 444444)));
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(2, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Info][1].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);


            Assert.AreEqual(4, generator.structureData.StructTypes.Count);
            Assert.AreEqual(3, generator.structureData.StructTypes[0].FieldData.Count);
            Assert.AreEqual("Item1", generator.structureData.StructTypes[0].FieldData[0].FieldName);
            Assert.AreEqual("Item2", generator.structureData.StructTypes[0].FieldData[1].FieldName);
            Assert.AreEqual("Item3", generator.structureData.StructTypes[0].FieldData[2].FieldName);

            Assert.AreEqual(2, generator.structureData.StructTypes[1].FieldData.Count);
            Assert.AreEqual("Id", generator.structureData.StructTypes[1].FieldData[0].FieldName);
            Assert.AreEqual("Position", generator.structureData.StructTypes[1].FieldData[1].FieldName);

            Assert.AreEqual(4, generator.structureData.StructTypes[2].FieldData.Count);
            Assert.AreEqual("Item1", generator.structureData.StructTypes[2].FieldData[0].FieldName);
            Assert.AreEqual("Item2", generator.structureData.StructTypes[2].FieldData[1].FieldName);
            Assert.AreEqual("Item3", generator.structureData.StructTypes[2].FieldData[2].FieldName);
            Assert.AreEqual("Item4", generator.structureData.StructTypes[2].FieldData[3].FieldName);

            Assert.AreEqual(2, generator.structureData.StructTypes[3].FieldData.Count);
            Assert.AreEqual("Item1", generator.structureData.StructTypes[3].FieldData[0].FieldName);
            Assert.AreEqual("Item2", generator.structureData.StructTypes[3].FieldData[1].FieldName);
        }

        [Test]
        public void TestFixedString64BytesMessageAnonArgument()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        FixedString64Bytes s = ""Fixed {0}"";
        Unity.Logging.Log.Info(s, (some_int:43, another:3.3));
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("FixedString64Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);


            Assert.AreEqual(1, generator.structureData.StructTypes.Count);
            Assert.AreEqual(2, generator.structureData.StructTypes[0].FieldData.Count);
            Assert.AreEqual("some_int", generator.structureData.StructTypes[0].FieldData[0].FieldName);
            Assert.AreEqual("another", generator.structureData.StructTypes[0].FieldData[1].FieldName);
        }

        [Test]
        public void TestGenerateLogEvenIfNoMessages()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        var a = Unity.Logging.Log.Logger;
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.IsTrue(generator.methodsGenCode.Contains("internal static class Log"), "generator.methodsGenCode.Contains('internal static class Log')");
        }

        [Test]
        public void TestGenerateLogEvenIfNothing()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        // nothing
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.IsTrue(generator.methodsGenCode.Contains("internal static class Log"), "log should be generated if no usage detected");
            Assert.IsNull(generator.userTypesGenCode);
        }

        [Test]
        public void TestMirrorCustomFullNamespace()
        {
            var testData = $@"
    public partial struct MyStructSimple : Unity.Logging.ILoggableMirrorStruct<MyStructSimple>
    {{
        public {CustomMirrorStruct.HeaderTypeName} pref;
        public int someInt;

        public MyStructSimple(int i)
        {{
            someInt = i;
        }}

        public override string ToString()
        {{
            return $""[MyStruct ololo = {{someInt}}]"";
        }}


        public bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth)
        {{
            var success = formatter.BeforeObject(ref output);
            success = formatter.WriteProperty(ref output, ""someInt"", someInt, ref currArgSlot) && success;
            success = formatter.AfterObject(ref output) && success;
            return success;
        }}
    }}
";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.IsTrue(generator.methodsGenCode.Contains("internal static class Log"), "log should be generated if no usage detected");
        }

        [Test]
        public void TestSameMirrorCustomUsingLogging()
        {
            var testData = @"

    using Unity.Logging;

    public partial struct MyStructSimple : ILoggableMirrorStruct<MyStructSimple>
    {
        public ulong Type;
        public int someInt;

        public MyStructSimple(int i)
        {
            someInt = i;
        }

        public override string ToString()
        {
            return $""[MyStruct ololo = {someInt}]"";
        }

        public bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth)
        {
            var success = formatter.BeforeObject(ref output);
            success = formatter.WriteProperty(ref output, ""someInt"", someInt, ref currArgSlot) && success;
            success = formatter.AfterObject(ref output) && success;
            return success;
        }
    }
";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.IsTrue(generator.methodsGenCode.Contains("internal static class Log"), "log should be generated if no usage detected");
        }

        [Test]
        public void TestDifferentMirrorCustomUsingLogging()
        {
            var testData = $@"

    using Unity.Logging;

    public struct Check {{ public int a; }}

    namespace CustomNameSpace
    {{
        public partial struct DateTimeWrapper : ILoggableMirrorStruct<DateTime>
        {{
            private {CustomMirrorStruct.HeaderTypeName} FirstField;
            private long ticks;

            public static implicit operator DateTimeWrapper(in DateTime arg)
            {{
                return new DateTimeWrapper
                {{
                    FirstField = {CustomMirrorStruct.HeaderTypeName}.Create(),
                    ticks = arg.Ticks
                }};
            }}

            public bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth)
            {{
                Unity.Logging.Log.Info(""He {{0}}"", new DateTime());
                Unity.Logging.Log.Info(""He {{0}} {{1}}"", new DateTimeWrapper(), new Check()); // don't generate DateTimeWrapper wrapper type

                return formatter.WriteProperty(ref output, "", ticks, ref currArgSlot);
            }}
        }}
    }}
";
            var generator = CommonUtils.GenerateCode(testData);

            var parsers = generator.parserGenCode.ToString();

            Assert.IsTrue(parsers.Contains("typeLength = UnsafeUtility.SizeOf<global::Unity.Logging.Check_"));
            Assert.IsTrue(parsers.Contains("typeLength = UnsafeUtility.SizeOf<global::CustomNameSpace.DateTimeWrapper>()"));
            Assert.IsFalse(parsers.Contains("DateTimeWrapper_"));

            var methods = generator.methodsGenCode;
            Assert.IsTrue(methods.Contains("internal static class Log"), "log should be generated if no usage detected");

            Assert.IsFalse(methods.Contains("DateTimeWrapper_"));
            Assert.IsFalse(methods.Contains("DateTime_"));

            Assert.IsTrue(methods.Contains("_E_E(in PayloadHandle msg, in global::CustomNameSpace.DateTimeWrapper arg0, "));


            Assert.IsNotNull(generator.userTypesGenCode);
            Assert.IsTrue(generator.userTypesGenCode.Contains("partial struct DateTimeWrapper"));

            var autogeneratedTypes = generator.typesGenCode.ToString();
            Assert.IsTrue(autogeneratedTypes.Contains("internal struct Check_"));
        }

        [Test]
        public void TestMirrorComplexCase1()
        {
            var testData = @"
namespace OtherNamespace
{{
    public struct MyStruct
    {{
        public int someInt;
        public string ololo;

        public MyStruct(int i)
        {{
            someInt = i;
            ololo = i.ToString();
        }}

        public override string ToString()
        {{
            return $""i = {someInt} ololo = {ololo}"";
        }}
        static void DoLog() {{
            Unity.Logging.Log.Info(""Hello {0}"", new MyStruct(42));
            Unity.Logging.Log.Info(""Hello ToStr {0}"", new MyStruct(42).ToString());
        }}
    }}
}}

namespace Namespace2
{
    public struct MyStruct
    {
        public int someInt;
        public string ololo;

        public MyStruct(int i)
        {
            someInt = i;
            ololo = i.ToString();
        }
    }

    public partial struct MyStructMirror : ILoggableMirrorStruct<MyStruct>
    {
        public MirrorStructHeader pre;
        public int someInt;
        public FixedString512Bytes s;

        public static implicit operator MyStructMirror(in MyStruct arg)
        {
            return new MyStructMirror
            {
                pre = MirrorStructHeader.Create(),
                someInt = arg.someInt,
                s = arg.ololo
            };
        }

        public bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth)
        {
            var time = DateTime.UtcNow;
            Unity.Logging.Log.Info(""Hello {0}. Time {1}"", new MyStruct(42), time);
            Unity.Logging.Log.Info(""Hello ToStr {0}"", new MyStruct(42).ToString());
            return true;
        }
    }

    public partial struct MyStructSimple : ILoggableMirrorStruct<MyStructSimple>
    {
        public ulong TypeId;
        public int someInt;

        public MyStructSimple(int i)
        {
            TypeId = 1;
            someInt = i;
        }

        public bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth)
        {
            return true;
        }
    }

    public partial struct DateTimeWrapper : ILoggableMirrorStruct<DateTime>
    {
        private MirrorStructHeader FirstField;
        private long ticks;

        public static implicit operator DateTimeWrapper(in DateTime arg)
        {
            return new DateTimeWrapper
            {
                FirstField = Unity.Logging.DateTimeWrapper.MirrorStructHeader.Create(),
                ticks = arg.Ticks
            };
        }

        public bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth)
        {
            return formatter.WriteProperty(ref output, "", ticks, ref currArgSlot);
        }
    }

}";

            var generator = CommonUtils.GenerateCode(testData);

            var parsers = generator.parserGenCode.ToString();

            Assert.IsTrue(parsers.Contains("global::Namespace2.MyStructMirror"));
            Assert.IsTrue(parsers.Contains("global::Namespace2.DateTimeWrapper>()"));
            Assert.IsTrue(parsers.Contains("global::OtherNamespace.MyStruct_"));

            var methods = generator.methodsGenCode;
            Assert.IsTrue(methods.Contains("internal static class Log"), "log should be generated if no usage detected");

            Assert.IsFalse(methods.Contains("DateTimeWrapper_"));
            Assert.IsFalse(methods.Contains("DateTime_"));

            Assert.IsTrue(methods.Contains("in global::Namespace2.MyStructMirror arg0, in global::Namespace2.DateTimeWrapper arg1"));


            var userTypes = generator.userTypesGenCode;
            Assert.IsNotNull(userTypes);
            Assert.IsTrue(userTypes.Contains(@"namespace Namespace2
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    partial struct MyStructMirror"));
            Assert.IsTrue(userTypes.Contains(@"namespace Namespace2
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    partial struct MyStructSimple"));
            Assert.IsTrue(userTypes.Contains(@"namespace Namespace2
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    partial struct DateTimeWrapper"));

            var autogeneratedTypes = generator.typesGenCode.ToString();

            Assert.IsTrue(autogeneratedTypes.Contains(@"namespace OtherNamespace
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MyStruct_"));

            Assert.IsTrue(autogeneratedTypes.Contains("ILoggableMirrorStruct<global::OtherNamespace.MyStruct>"));
        }

        [Test]
        public void TestMirrorNoImplicit()
        {
            var testData = $@"

    using Unity.Logging;

    public partial struct DateTimeWrapper : ILoggableMirrorStruct<DateTime>
    {{
        private {CustomMirrorStruct.HeaderTypeName} FirstField;
        private long ticks;

        public static implicit operator DateTimeWrapper(in long arg)
        {{
            return new DateTimeWrapper
            {{
                FirstField = {CustomMirrorStruct.HeaderTypeName}.Create(),
                ticks = arg
            }};
        }}

        public bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth)
        {{
            return formatter.WriteProperty(ref output, "", ticks, ref currArgSlot);
        }}
    }}
";
            var generator = CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);

            Assert.AreEqual(1, diagnostics.Length);
            Assert.AreEqual(DiagnosticSeverity.Error, diagnostics[0].Severity);
            Assert.AreEqual("LMS0005", diagnostics[0].Id);

            Assert.IsTrue(generator.methodsGenCode.Contains("internal static class Log"), "log should be generated if no usage detected");
        }

        [Test]
        public void TestMirrorIsStructItself()
        {
            var testData = $@"

    using Unity.Logging;

    public partial struct MyStructSimple : ILoggableMirrorStruct<MyStructSimple>
    {{
        public MirrorStructHeader pre;
        public int someInt;

        public MyStructSimple(int i)
        {{
            pre = MirrorStructHeader.Create();
            someInt = i;
        }}

        public override string ToString()
        {{
            Unity.Logging.Log.To(log).Info(""Hello Simple {{0}}"", new MyStructSimple(1241));
            return $""[MyStruct val = {{someInt}}]"";
        }}

        public bool AppendToUnsafeText(ref UnsafeText output, ref FormatterStruct formatter, ref LogMemoryManager memAllocator, ref ArgumentInfo currArgSlot, int depth)
        {{
            var success = formatter.BeforeObject(ref output);
            success = formatter.WriteProperty(ref output, ""userMirror"", someInt, ref currArgSlot) && success;
            success = formatter.AfterObject(ref output) && success;

            return success;
        }}
    }}
";
            var generator = CommonUtils.GenerateCode(testData);

            var parsers = generator.parserGenCode.ToString();
            Assert.IsTrue(parsers.Contains("// user type"));

            var methods = generator.methodsGenCode;
            Assert.IsTrue(methods.Contains("MyStructSimple"));
            Assert.IsFalse(methods.Contains("global::Unity.Logging.MyStructSimple"));
            var countGlobal = CommonUtils.StringOccurrencesCount(methods, "in global::MyStructSimple arg0", StringComparison.Ordinal);
            var countStructMention = CommonUtils.StringOccurrencesCount(methods, "MyStructSimple arg0", StringComparison.Ordinal);
            Assert.AreEqual(countGlobal, countStructMention, "MyStructSimple is probably used in wrong namespace");

            var userTypes = generator.userTypesGenCode;
            Assert.IsTrue(userTypes.Contains("partial struct MyStructSimple"));

            var autogeneratedTypes = generator.typesGenCode.ToString();
            Assert.IsFalse(autogeneratedTypes.Contains("struct"));
        }
    }
}
