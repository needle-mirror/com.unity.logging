using System.Linq;
using System.Text;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Logging.Sinks;

namespace Unity.Logging.Tests
{
    public class LogConfigTests : LoggingTestFixture
    {
        [Test]
        [TestCase(false, false, TestName = "LogLevelFiltering_WithoutUpdatesBetween_OrderIsImportant")]
        [TestCase(true, false, TestName = "LogLevelFiltering_WithUpdatesBetween_OrderIsImportant")]
        [TestCase(false, true, TestName = "LogLevelFiltering_WithoutUpdatesBetween_IgnoreOrder")]
        [TestCase(true, true, TestName = "LogLevelFiltering_WithUpdatesBetween_IgnoreOrder")]
        public void LogLevelFiltering(bool interUpdate, bool sort)
        {
            StringSink.AssertNoSinks();

            var indx = 0;

            var log1 = Log.Logger = new LoggerConfig()
                .MinimumLevel.Debug()                               // default = debug
                .WriteTo.StringLogger(outputTemplate: "{Message}")    // log level = default = debug
                .WriteTo.Console(minLevel:LogLevel.Verbose)                  // log level = verbose
                .MinimumLevel.Warning()                             // default after this. ignored
                .CreateLogger();

            AssertNoLogDispatchedExpected();
            AssertExpectedToHaveSink<StringSink>(log1);

            LogAll(ref indx);


            // for TestLogger
            var expected1 = new StringBuilder();
            //expected1.AppendLine("#1 VERBOSE");
            expected1.AppendLine("#2 DEBUG");
            expected1.AppendLine("#3 INFO");
            expected1.AppendLine("#4 WARNING");
            expected1.AppendLine("#5 ERROR");
            expected1.AppendLine("#6 FATAL");

            if (interUpdate)
            {
                Update();
                UpdateComplete();
            }

            var log2 = Log.Logger = new LoggerConfig()
                .WriteTo.StringLogger(outputTemplate: "{Message}", minLevel: LogLevel.Debug)  // log level = debug
                .WriteTo.Console(minLevel: LogLevel.Debug)                                                            // log level = debug
                .MinimumLevel.Warning()                                                                     // default after this. ignored
                .CreateLogger();

            AssertExpectedToHaveSink<StringSink>(log2);

            if (interUpdate)
            {
                Update();
                UpdateComplete();
                AssertNoLogDispatchedExpected();
            }

            LogAll(ref indx);

            AssertExpectedToHaveSink<StringSink>(Log.Logger);

            // for TestLogger
            var expected2 = new StringBuilder();
            // expected2.AppendLine("#7 VERBOSE");
            expected2.AppendLine("#8 DEBUG");
            expected2.AppendLine("#9 INFO");
            expected2.AppendLine("#10 WARNING");
            expected2.AppendLine("#11 ERROR");
            expected2.AppendLine("#12 FATAL");

            if (interUpdate)
            {
                Update();
                UpdateComplete();
                AssertNoLogDispatchedExpected();
            }

            var log3 = Log.Logger = new LoggerConfig()
                .MinimumLevel.Set(LogLevel.Warning)
                .WriteTo.StringLogger(outputTemplate: "{Message}", minLevel: LogLevel.Error) // log level = error
                .WriteTo.Console()                                                                         // log level = warning = default
                .CreateLogger();

            AssertExpectedToHaveSink<StringSink>(log3);

            LogAll(ref indx);

            var expected3 = new StringBuilder();
            // expected3.AppendLine("#13 VERBOSE");
            // expected3.AppendLine("#14 DEBUG");
            // expected3.AppendLine("#15 INFO");
            // expected3.AppendLine("#16 WARNING");
            expected3.AppendLine("#17 ERROR");
            expected3.AppendLine("#18 FATAL");

            Update();
            UpdateComplete();

            AssertNoLogDispatchedExpected();

            var output1 = GetStringFromFirstTestSink(log1);
            var output2 = GetStringFromFirstTestSink(log2);
            var output3 = GetStringFromFirstTestSink(log3);

            {
                var a = SortLines(expected1.ToString());
                var b = SortLines(output1);
                Assert.AreEqual(a, b);
            }

            {
                var a = SortLines(expected2.ToString());
                var b = SortLines(output2);
                Assert.AreEqual(a, b);
            }

            {
                var a = SortLines(expected3.ToString());
                var b = SortLines(output3);
                Assert.AreEqual(a, b);
            }

            string SortLines(string input)
            {
                var lines = input
                    .Split('\n')
                    .Where(s => string.IsNullOrWhiteSpace(s) == false)
                    .Select(s => s.Trim('\n', '\r'));

                if (sort)
                    lines = lines.OrderBy(s => s);

                return string.Join("\r\n", lines);
            }

            void LogAll(ref int indx)
            {
                Log.Verbose("#{0} {Level}", ++indx);
                Log.Debug("#{0} {Level}", ++indx);
                Log.Info("#{0} {Level}", ++indx);
                Log.Warning("#{0} {Level}", ++indx);
                Log.Error("#{0} {Level}", ++indx);
                Log.Fatal("#{0} {Level}", ++indx);
            }
        }

        [Test]
        public void LogConfigSameSinkTypeTwice()
        {
            try
            {
                var logger = Log.Logger = new LoggerConfig()
                    .WriteTo.Console()
                    .WriteTo.StringLogger()
                    .WriteTo.Console()
                    .CreateLogger();
            }
            catch
            {
                Assert.Fail("Exception is not expected");
            }
        }

        [Test]
        public void LogNoLogger()
        {
            Log.Logger = null;
            string managedCall = "managed";
            FixedString32Bytes burstable = "burst";
            Log.Debug("literal convertable into fixedString");
            Log.Debug(managedCall);
            Log.Debug(burstable);
        }

        [Test]
        public void LogConfigCreate()
        {
            Logger log1 = null;
            Logger log2 = null;
            Logger log3 = null;
            Logger log4 = null;

            using var tmpFile1 = new TempFileGenerator();
            using var tmpFile2 = new TempFileGenerator();

            var allSystems = new[] {typeof(ConsoleSinkSystem), typeof(FileSinkSystem), typeof(StringSink)};
            try
            {
                log1 = Log.Logger = new Logger(new LoggerConfig()
                    .WriteTo.Console()
                    .WriteTo.StringLogger());
                AssertExpectedToHaveSink<ConsoleSinkSystem>(log1);
                AssertExpectedToHaveSink<StringSink>(log1);

                Assert.AreEqual(2, log1.SinksCount);

                log2 = Log.Logger = new Logger(new LoggerConfig()
                    .WriteTo.File(tmpFile2.FilePath));

                Assert.AreEqual(2, log1.SinksCount);

                AssertExpectedToNotHaveSink<ConsoleSinkSystem>(log2);
                AssertExpectedToNotHaveSink<StringSink>(log2);
                AssertExpectedToHaveSink<FileSinkSystem>(log2);

                Assert.AreEqual(2, log1.SinksCount);
                Assert.AreEqual(1, log2.SinksCount);


                log3 = Log.Logger = new Logger(new LoggerConfig()
                    .WriteTo.Console());
                AssertExpectedToHaveSink<ConsoleSinkSystem>(log3);

                Assert.AreEqual(1, log3.SinksCount);


                log4 = Log.Logger = new Logger(new LoggerConfig()
                    .WriteTo.File(tmpFile1.FilePath)
                    .WriteTo.StringLogger());
                AssertExpectedToHaveSink<FileSinkSystem>(log4);
                AssertExpectedToHaveSink<StringSink>(log4);

                Assert.AreEqual(2, log4.SinksCount);
            }
            finally
            {
                log1?.Dispose();
                log2?.Dispose();
                log3?.Dispose();
                log4?.Dispose();
            }
        }
    }
}
