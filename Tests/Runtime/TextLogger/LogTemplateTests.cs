using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Logging.Sinks;

namespace Unity.Logging.Tests
{
    public class LogTemplateTests : LoggingTestFixture
    {
        [Test]
        public void LogTemplateTest1()
        {
            StringSink.AssertNoSinks();
            var log1 = Log.Logger = new LoggerConfig()
                                    .WriteTo.StringLogger(outputTemplate: "{Timestamp} ^ {Level} |{Message}")
                                    .CreateLogger();

            AssertNoLogDispatchedExpected();

            Log.To(log1.Handle).Info("Info {0}", (1, (FixedString32Bytes)"adawdawd"));
            var expectedMessage = $"Info {(1, (FixedString32Bytes) "adawdawd")}{Environment.NewLine}".Replace('(', '[').Replace(')', ']');

            AssertExpectedToHaveLogDispatched(log1.Handle, 1);

            Update();
            UpdateComplete();

            AssertNoLogDispatchedExpected(log1.Handle);

            var output = GetStringFromFirstTestSink(log1);

            Assert.IsTrue(output.Contains(" ^ "), output, $"Output should contain < ^ > but was {output}");
            Assert.IsTrue(output.Contains(" |"), output, $"Output should contain < |> but was {output}");
            Assert.IsTrue(output.Contains("^ INFO |"), output, $"Output should contain <^ INFO |> but was {output}");
            var mess = output.Split('|')[1];
            Assert.AreEqual(expectedMessage, mess);
        }

        [Test]
        public void LogTemplateTest2()
        {
            StringSink.AssertNoSinks();

            var log1 = Log.Logger = new LoggerConfig()
                .OutputTemplate("{Message}*{Message}*{Message}*{Message}^{Timestamp},{Level}{Level}{Level}")
                .WriteTo.StringLogger()
                .OutputTemplate("{Timestamp}")                              // should be ignored
                .CreateLogger();

            AssertNoLogDispatchedExpected(log1.Handle);

            Log.To(log1).Fatal("ABC_{0}", "CBA");

            AssertExpectedToHaveLogDispatched(log1.Handle, 1);

            Update();
            UpdateComplete();

            AssertNoLogDispatchedExpected(log1.Handle);

            var output = GetStringFromFirstTestSink(log1);

            Assert.IsTrue(output.Contains("^"), output, output, $"Output should contain <^> but was {output}");
            Assert.IsTrue(output.EndsWith($"FATALFATALFATAL{Environment.NewLine}"), output, $"Output should ends with <FATALFATALFATAL\\n> but was {output}");
            Assert.IsTrue(output.StartsWith("ABC_CBA*ABC_CBA*ABC_CBA*ABC_CBA^"), output, $"Output should starts with <ABC_CBA*ABC_CBA*ABC_CBA*ABC_CBA^> but was {output}");
        }

        [Test]
        public void LogTemplateTest3()
        {
            StringSink.AssertNoSinks();
            var log1 = Log.Logger = new LoggerConfig()
                                    .OutputTemplate("Some log")
                                    .WriteTo.StringLogger()
                                    .CreateLogger();

            AssertNoLogDispatchedExpected(log1.Handle);

            Log.To(log1).Fatal("ABC_{0}", "CBA");

            AssertExpectedToHaveLogDispatched(log1.Handle, 1);

            Update();
            UpdateComplete();

            AssertNoLogDispatchedExpected(log1.Handle);

            var output = GetStringFromFirstTestSink(log1);

            Assert.AreEqual($"Some log{Environment.NewLine}", output);
        }

        [Test]
        public void LogLoggerCallTest1()
        {
            StringSink.AssertNoSinks();
            var log1 = new LoggerConfig()
                       .OutputTemplate("{Message}")
                       .WriteTo.StringLogger()
                       .CreateLogger();

            AssertNoLogDispatchedExpected(log1.Handle);

            Log.To(log1).Info("This {0} should be {1} detected {2}", 1, 2, 3);
            //Log.Info("Should be ignored");

            AssertExpectedToHaveLogDispatched(log1.Handle, 1);

            Update();
            UpdateComplete();

            AssertNoLogDispatchedExpected(log1.Handle);

            var output = GetStringFromFirstTestSink(log1);

            Assert.AreEqual($"This 1 should be 2 detected 3{Environment.NewLine}", output);
        }
    }
}
