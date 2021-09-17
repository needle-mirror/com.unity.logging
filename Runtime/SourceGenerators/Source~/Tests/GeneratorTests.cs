using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using SourceGenerator.Logging;

namespace Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class GeneratorTests
    {
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
            var s = generator.methodsGenCode.ToString();
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg1"));
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

            Assert.AreEqual(2, generator.invokeData.InvokeInstances[LogCallKind.Error].Count);
            Assert.AreEqual("string", generator.invokeData.InvokeInstances[LogCallKind.Error][0].MessageData.MessageType);
            Assert.AreEqual("FixedString32Bytes", generator.invokeData.InvokeInstances[LogCallKind.Error][1].MessageData.MessageType);

            Assert.AreEqual(3, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.AreEqual("FixedString128Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.AreEqual("FixedString32Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][1].MessageData.MessageType);
            Assert.AreEqual("FixedString4096Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][2].MessageData.MessageType);
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

            var s = generator.methodsGenCode.ToString();
            Assert.IsTrue(s.Contains("FixedString32Bytes arg0)")); // FixedString32Bytes is a special type
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0")); // FixedString32Bytes is a special type
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

            var s1 = generator.methodsGenCode.ToString();
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

            indx = s2.IndexOf("[MarshalAs(UnmanagedType.U1)]", indx, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(indx != -1);
            indx = s2.IndexOf("public global::System.Boolean b;", indx, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(indx != -1);
            indx = s2.IndexOf("[MarshalAs(UnmanagedType.U1)]", indx, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(indx != -1);
            indx = s2.IndexOf("public global::System.Boolean c;", indx, StringComparison.OrdinalIgnoreCase);
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
            Assert.AreEqual("FixedString128Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][1].MessageData.MessageType);
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
            Assert.IsTrue(s.Contains("output.Append(C1)"));
            Assert.IsTrue(s.Contains("output.Append(String512)"));
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
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Info].Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.AreEqual(5, generator.structureData.StructTypes.Count);

            var s = generator.typesGenCode.ToString();
            Assert.IsTrue(s.Contains("bb.WriteFormattedOutput"));
            Assert.IsTrue(s.Contains("cc.WriteFormattedOutput"));
            Assert.IsTrue(s.Contains("dd.WriteFormattedOutput"));

            Assert.IsTrue(s.Contains("output.Append(A1)"));
            Assert.IsTrue(s.Contains("A2.WriteFormattedOutput"));
            Assert.IsTrue(s.Contains("A3.WriteFormattedOutput"));
            Assert.IsTrue(s.Contains("A4.WriteFormattedOutput"));
            Assert.IsTrue(s.Contains("A5.WriteFormattedOutput"));
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
        public void TestFixedStringsArg()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        Unity.Logging.Log.Info(""A{0}"", ""B1""); // DST-384 bug
        Unity.Logging.Log.Info(""A{0}"", (FixedString32Bytes)""B2"");
        Unity.Logging.Log.Info(""A{0}"", (FixedString64Bytes)""B3"");
        Unity.Logging.Log.Info(""A{0}"", (FixedString128Bytes)""B4"");
        Unity.Logging.Log.Info(""A{0}"", (FixedString512Bytes)""B5"");
        Unity.Logging.Log.Info(""A{0}"", (FixedString4096Bytes)""B6"");
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances.Count);
            Assert.IsTrue(generator.invokeData.IsValid);

            var invInfo = generator.invokeData.InvokeInstances[LogCallKind.Info];
            Assert.AreEqual(1, invInfo.Count);

            Assert.AreEqual(1, invInfo[0].ArgumentData.Count);
            Assert.IsTrue(invInfo[0].ArgumentData[0].IsSpecialSerializableType());
            Assert.IsTrue(invInfo[0].ArgumentData[0].ArgumentTypeName == "FixedString4096Bytes"); // all must merge into one, to avoid ambiguity
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

            var invInfo = generator.invokeData.InvokeInstances[LogCallKind.Info];
            Assert.AreEqual(2, invInfo.Count);

            Assert.AreEqual(1, invInfo[0].ArgumentData.Count);
            Assert.IsTrue(invInfo[0].ArgumentData[0].IsSpecialSerializableType());
            Assert.AreEqual("FixedString512Bytes", invInfo[0].ArgumentData[0].ArgumentTypeName); // all strings merge into one, to avoid ambiguity

            Assert.AreEqual(1, invInfo[1].ArgumentData.Count);
            Assert.IsTrue(invInfo[1].ArgumentData[0].IsSpecialSerializableType());
            Assert.AreEqual("Int32", invInfo[1].ArgumentData[0].ArgumentTypeName);
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

            var s = generator.methodsGenCode.ToString();
            Assert.IsTrue(s.Contains("Int32 arg0)"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0"));
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
            Assert.AreEqual("FixedString128Bytes", generator.invokeData.InvokeInstances[LogCallKind.Error][0].MessageData.MessageType);
            Assert.AreEqual("FixedString32Bytes", generator.invokeData.InvokeInstances[LogCallKind.Fatal][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);

            Assert.IsNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode.ToString();
            Assert.IsTrue(s.Contains("Int32 arg0)"));
            Assert.IsTrue(s.Contains("BuildContextSpecialType(arg0"));
        }

        [Test]
        public void TestLoggerHandleAndOmmitedMessage()
        {
            var testData = @"
class ClassA
{
    public void A()
    {
        Unity.Logging.Logger l3;
        // ommited messages
        Unity.Logging.Log.To(l3.Handler).Verbose(1, 2, 3);
    }
}";
            var generator = CommonUtils.GenerateCode(testData);

            Assert.AreEqual(1, generator.invokeData.InvokeInstances[LogCallKind.Verbose].Count);

            {
                // Unity.Logging.Log.Verbose(l3.Handler, 1, 2, 3);
                var verboseLog = generator.invokeData.InvokeInstances[LogCallKind.Verbose][0];
                Assert.AreEqual("FixedString32Bytes", verboseLog.MessageData.MessageType);
                Assert.AreEqual("{0} {1} {2}", verboseLog.MessageData.LiteralValue);

                Assert.AreEqual(3, verboseLog.ArgumentData.Count);
                Assert.AreEqual("1", verboseLog.ArgumentData[0].LiteralValue);
                Assert.AreEqual("2", verboseLog.ArgumentData[1].LiteralValue);
                Assert.AreEqual("3", verboseLog.ArgumentData[2].LiteralValue);
            }

            Assert.IsTrue(generator.invokeData.IsValid);
            Assert.IsNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode.ToString();
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
                Assert.AreEqual("FixedString32Bytes", verboseLog.MessageData.MessageType);
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

            {
                // Unity.Logging.Log.Verbose(l3.Handler, str, 1, 2, 3);
                var verboseLog = generator.invokeData.InvokeInstances[LogCallKind.Verbose][0];
                Assert.AreEqual("object", verboseLog.MessageData.MessageType);

                Assert.AreEqual(1, verboseLog.ArgumentData.Count);
                Assert.AreEqual("ZZZ", verboseLog.ArgumentData[0].LiteralValue);
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

        // ommited messages
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
                // Unity.Logging.Log.Error(l3.Handler, ""123123""); is merged-in and ignored as a shorter version
                var errorLog = generator.invokeData.InvokeInstances[LogCallKind.Error][0];
                Assert.AreEqual("FixedString128Bytes", errorLog.MessageData.MessageType); // 12312312312312312312312313131231231231231231231231231231231231231231313123123123123123
                Assert.AreEqual(0, errorLog.ArgumentData.Count);
                Assert.AreEqual("12312312312312312312312313131231231231231231231231231231231231231231313123123123123123", errorLog.MessageData.LiteralValue);
            }

            {
                // Unity.Logging.Log.Fatal(l2, ""Hello"");
                var fatalLog = generator.invokeData.InvokeInstances[LogCallKind.Fatal][0];
                Assert.AreEqual("FixedString32Bytes", fatalLog.MessageData.MessageType);
                Assert.AreEqual(0, fatalLog.ArgumentData.Count);
                Assert.AreEqual("Hello", fatalLog.MessageData.LiteralValue);
            }


            {
                // Unity.Logging.Log.Verbose(l3.Handler, 1, 2, 3);
                var verboseLog = generator.invokeData.InvokeInstances[LogCallKind.Verbose][0];
                Assert.AreEqual("FixedString32Bytes", verboseLog.MessageData.MessageType);
                Assert.AreEqual("{0} {1} {2}", verboseLog.MessageData.LiteralValue);

                Assert.AreEqual(3, verboseLog.ArgumentData.Count);
                Assert.AreEqual("1", verboseLog.ArgumentData[0].LiteralValue);
                Assert.AreEqual("2", verboseLog.ArgumentData[1].LiteralValue);
                Assert.AreEqual("3", verboseLog.ArgumentData[2].LiteralValue);
            }

            {
                // Unity.Logging.Log.Verbose(3, '3');
                var verboseLog = generator.invokeData.InvokeInstances[LogCallKind.Verbose][1];
                Assert.AreEqual("FixedString32Bytes", verboseLog.MessageData.MessageType);
                Assert.AreEqual("{0} {1}", verboseLog.MessageData.LiteralValue);

                Assert.AreEqual(2, verboseLog.ArgumentData.Count);
                Assert.AreEqual("3", verboseLog.ArgumentData[0].LiteralValue);
                Assert.AreEqual("3", verboseLog.ArgumentData[1].LiteralValue);
            }

            Assert.IsTrue(generator.invokeData.IsValid);
            Assert.IsNull(generator.structureData.StructTypes);

            var s = generator.methodsGenCode.ToString();
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

            var s = generator.methodsGenCode.ToString();
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

            var s = generator.methodsGenCode.ToString();
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
            Assert.AreEqual("FixedString32Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.AreEqual("FixedString32Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][1].MessageData.MessageType);
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
    }
}
