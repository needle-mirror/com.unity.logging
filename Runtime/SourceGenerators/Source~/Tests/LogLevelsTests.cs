using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using SourceGenerator.Logging;
using SourceGenerator.Logging.Declarations;

namespace Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class LogLevelsTests
    {
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
        Unity.Logging.Log.Warning(""Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nam et. {0}!"", new Data());
        Unity.Logging.Log.Info(""Hello #2 {0}!"", new Data()); // same call signature as the previous one

        Unity.Logging.Log.Fatal(""Short Fatal"");

        Unity.Logging.Log.Info(@""{1} detect {0}!
Lorem ipsum dolor sit amet, consectetur adipiscing elit. Maecenas elit nisl, lacinia eget ultrices vel,
commodo ac turpis. Donec in justo eget metus aliquam porttitor. In hac habitasse platea dictumst.
Phasellus ut eleifend ligula, a placerat sem. Donec molestie quam vulputate odio consequat, nec dignissim est mattis.
Proin lorem ex, pulvinar eget placerat in, consequat ut tortor. Nam pharetra efficitur lacus ac fermentum.
Donec volutpat fermentum augue, sit amet eleifend lectus commodo at quam.
"", new Data(), new Data());

        Log.Verbose(@""No Using, so shouldn't detect {0}!
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

            Assert.AreEqual(3, generator.invokeData.InvokeInstances.Count);
            Assert.AreEqual("FixedString64Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][0].MessageData.MessageType);
            Assert.AreEqual("FixedString4096Bytes", generator.invokeData.InvokeInstances[LogCallKind.Info][1].MessageData.MessageType);
            Assert.AreEqual("FixedString128Bytes", generator.invokeData.InvokeInstances[LogCallKind.Warning][0].MessageData.MessageType);
            Assert.AreEqual("FixedString32Bytes", generator.invokeData.InvokeInstances[LogCallKind.Fatal][0].MessageData.MessageType);
            Assert.IsTrue(generator.invokeData.IsValid);


            Assert.AreEqual(1, generator.structureData.StructTypes.Count);
            Assert.AreEqual(2, generator.structureData.StructTypes[0].FieldData.Count);
            Assert.AreEqual("F1", generator.structureData.StructTypes[0].FieldData[0].FieldName);
            Assert.AreEqual("F2", generator.structureData.StructTypes[0].FieldData[1].FieldName);
        }

        [Test]
        public void TestDecorateErrorNoMessage()
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
        Unity.Logging.Log.Decorate(new Data());
    }
}
";

            CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

            Assert.AreEqual(1, errors.Length);
            Assert.IsTrue(errors[0].Id == CompilerMessages.MissingDecoratePropertyName.Item1, $"There was an error, but type is wrong. Full error message: <{errors[0]}>");
        }

        [Test]
        public void TestDecorateErrorTooMuchArgsMessage()
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
        Unity.Logging.Log.Decorate(""Hello FixedString64BytesFixedString64Bytes {0}!"", new Data(), 42, 51, 1251);
    }
}
";

            CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

            Assert.AreEqual(1, errors.Length);
            Assert.IsTrue(errors[0].Id == CompilerMessages.TooMuchDecorateArguments.Item1, $"There was an error, but type is wrong. Full error message: <{errors[0]}>");
        }

        [Test]
        public void TestDecorateErrorMissingDecorateArguments()
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
        Unity.Logging.Log.Decorate(""Hello FixedString64BytesFixedString64Bytes {0}!"");
    }
}
";

            CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

            Assert.AreEqual(1, errors.Length);
            Assert.IsTrue(errors[0].Id == CompilerMessages.MissingDecorateArguments.Item1, $"There was an error, but type is wrong. Full error message: <{errors[0]}>");
        }

        [Test]
        public void TestDecorateErrorExpectedBoolIn3rdDecorateArgument()
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
        Unity.Logging.Log.Decorate(""Hello FixedString64BytesFixedString64Bytes {0}!"", A, 42);
    }
}
";

            CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

            Assert.AreEqual(1, errors.Length);
            Assert.IsTrue(errors[0].Id == CompilerMessages.ExpectedBoolIn3rdDecorateArgument.Item1, $"There was an error, but type is wrong. Full error message: <{errors[0]}>");
        }

        [Test]
        public void TestDecorateErrorVoidFunction()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public long F1 = 42;
        public int F2 = 24;
    }

    public static void VoidFunc()
    {
    }

    public void A()
    {
        Unity.Logging.Log.Decorate(""Excess () {0}!"", VoidFunc(), false);
    }
}
";

            CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

            Assert.IsTrue(errors[0].Id == CompilerMessages.CannotBeVoidError.Item1, $"There was an error, but type is wrong. Full error message: <{errors[0]}>");
        }

        [Test]
        public void TestDecorateErrorVoidFunction2()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public long F1 = 42;
        public int F2 = 24;
    }

    public static void VoidFunc()
    {
    }

    public void A()
    {
        Unity.Logging.Log.Decorate(""Void!"", VoidFunc());
    }
}
";

            CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

            Assert.AreEqual(1, errors.Length);
            Assert.IsTrue(errors[0].Id == CompilerMessages.CannotBeVoidError.Item1, $"There was an error, but type is wrong. Full error message: <{errors[0]}>");
        }

        [Test]
        public void TestInfoErrorVoidFunction()
        {
            var testData = @"
class ClassA
{
    struct Data
    {
        public long F1 = 42;
        public int F2 = 24;
    }

    public static void VoidFunc()
    {
    }

    public void A()
    {
        Unity.Logging.Log.Info(""Void! {0}"", VoidFunc());
    }
}
";

            CommonUtils.GenerateCodeExpectErrors(testData, out var diagnostics);

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

            Assert.AreEqual(1, errors.Length);
            Assert.IsTrue(errors[0].Id == CompilerMessages.CannotBeVoidError.Item1, $"There was an error, but type is wrong. Full error message: <{errors[0]}>");
        }

        [Test]
        public void TestDecorateCorrect()
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
        Unity.Logging.Log.Decorate(""1Hello32 {0}!"", A, true); // not detected
        Unity.Logging.Log.Decorate(""2Hello32 {0} int!"", 42);
        Unity.Logging.Log.Decorate(""3Hello32 {0}!"", new Data());
        Unity.Logging.Log.Decorate(""4Hello FixedString64BytesFixedString64Bytes {0}"", new Data());

        Unity.Logging.Log.Info(""TEST FixedString64BytesFixedString64Bytes {0}"", new Data());
    }
}
";

            var generator = CommonUtils.GenerateCode(testData);

            var decor = generator.invokeData.InvokeInstances[LogCallKind.Decorate];
            var infoCall = generator.invokeData.InvokeInstances[LogCallKind.Info];


            Assert.IsTrue(generator.invokeData.IsValid);
            Assert.AreEqual(2, generator.invokeData.InvokeInstances.Count);
            Assert.AreEqual(2, decor.Count);

            var dec1 = decor[0];
            var dec2 = decor[1];

            Assert.AreEqual("FixedString64Bytes", dec1.MessageData.MessageType);
            Assert.AreEqual("FixedString64Bytes", dec2.MessageData.MessageType);
            Assert.AreEqual(1, dec1.ArgumentData.Count);
            Assert.AreEqual(1, dec2.ArgumentData.Count);

            Assert.AreEqual("Int32", dec1.ArgumentData[0].ArgumentTypeName);
            Assert.AreEqual("Data", dec2.ArgumentData[0].ArgumentTypeName);
        }

        [Test]
        public void TestStringToFixedString()
        {
            var testData = @"
class ClassA
{
    public static void DecoratorThatIsCalledForJob(in LogContextWithDecorator d)
    {
        Unity.Logging.Log.To(d).Decorate(""Job"", ""FromJobOnly"");
        Unity.Logging.Log.Info(""Job"", ""FromJobOnly"");
    }
}
";
            var generator = CommonUtils.GenerateCode(testData);

            var decor = generator.invokeData.InvokeInstances[LogCallKind.Decorate];
            var info = generator.invokeData.InvokeInstances[LogCallKind.Info];

            Assert.IsTrue(decor[0].IsBurstable);
            Assert.IsFalse(generator.methodsGenCode.ToString().Contains("System.String"));
        }
    }
}
