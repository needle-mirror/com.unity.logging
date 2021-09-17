using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
using UnityEngine;

namespace Unity.Logging.Tests
{
    [TestFixture]
    public class RollingFileLogTests : LoggingTestFixture
    {
        [Test]
        public void LogRollingSizeWithStackTraceInJson()
        {
            LogRollingSize(true);
        }

        [Test]
        public void LogRollingSizeWithoutStackTrace()
        {
            LogRollingSize(false);
        }

        public void LogRollingSize(bool requireStackTrace)
        {
            if (requireStackTrace)
            {
                ManagedStackTraceWrapper.AssertNoAllocatedResources();
            }
            const string func_name = "LogRollingSize";
            const string file_name = "RollingFileLogTests.cs";

            using var tmpFile1 = new TempFileGenerator("txt");
            using var tmpFile2 = new TempFileGenerator("json");

            Log.Logger = new LoggerConfig().WriteTo.File(tmpFile1.FilePath, maxFileSizeBytes: 128, maxRoll: 3)
                                           .WriteTo.JsonFile(tmpFile2.FilePath, maxFileSizeBytes: 128, maxRoll: 3, captureStackTrace: requireStackTrace)
                                           .CreateLogger();

            Log.Info("1 {0} end1", (1, (FixedString32Bytes)"adawdawd"));
            Log.Info("2 {0} end2", (1, (FixedString32Bytes)"adawdawd"));

            Log.Info("3awmdoawjdoaiwjdioawjd {0} end3", ((FixedString128Bytes)"adwnipdnaiwpndipawndpainwdpianwdianwdinawdi", (FixedString64Bytes)"adawdawddamwdoijawJD@(*EY&)!Y@EH"));
            var x_line_number = GetLineInfo() - 1;

            Log.Info("4awi89y43921441j41jwjd {0} end4", ((FixedString128Bytes)"adwnipdnaiwpndipawndpainwdpianwdianwdinawdi", (FixedString64Bytes)"ZdawdawddamwdoijawJD@(*EY&)!Y@EH"));
            var y_line_number = GetLineInfo() - 1;

            Log.Info("5 this should override first file end5");
            var z_line_number = GetLineInfo() - 1;

            LoggerManager.DeleteAllLoggers();

            var a = tmpFile1.RollingFileAllText(roll: 0);
            var b = tmpFile1.RollingFileAllText(roll: 1);
            var c = tmpFile1.RollingFileAllText(roll: 2);

            Assert.IsTrue(a.Trim().EndsWith(" | INFO | 5 this should override first file end5"));
            Assert.IsTrue(b.Trim().EndsWith(" | INFO | 3awmdoawjdoaiwjdioawjd [adwnipdnaiwpndipawndpainwdpianwdianwdinawdi, adawdawddamwdoijawJD@(*EY&)!Y@EH] end3"));
            Assert.IsTrue(c.Trim().EndsWith(" | INFO | 4awi89y43921441j41jwjd [adwnipdnaiwpndipawndpainwdpianwdianwdinawdi, ZdawdawddamwdoijawJD@(*EY&)!Y@EH] end4"));


            var x = tmpFile2.RollingFileAllText(roll: 0);
            var y = tmpFile2.RollingFileAllText(roll: 1);
            var z = tmpFile2.RollingFileAllText(roll: 2);

            if (requireStackTrace)
            {
                // json are much larger (because of stacktrace, so rolling will be different - 1 message per txt)
                Assert.IsTrue(x.Contains("\"Level\":\"INFO\",\"Message\":\"3awmdoawjdoaiwjdioawjd {0} end3\",\"Stacktrace\":\""), x);
                Assert.IsTrue(y.Contains("\"Level\":\"INFO\",\"Message\":\"4awi89y43921441j41jwjd {0} end4\",\"Stacktrace\":\""), y);
                Assert.IsTrue(z.Contains("\"Level\":\"INFO\",\"Message\":\"5 this should override first file end5\",\"Stacktrace\":\""), z);

                AssertStackTrace(x, file_name, func_name, x_line_number);
                AssertStackTrace(y, file_name, func_name, y_line_number);
                AssertStackTrace(z, file_name, func_name, z_line_number);
            }
            else
            {
                Assert.IsTrue(x.Trim().EndsWith("\"Level\":\"INFO\",\"Message\":\"4awi89y43921441j41jwjd {0} end4\",\"Properties\":{\"arg0\":\"[adwnipdnaiwpndipawndpainwdpianwdianwdinawdi, ZdawdawddamwdoijawJD@(*EY&)!Y@EH]\"}}" + Environment.NewLine + "]"));
                Assert.IsTrue(y.Trim().EndsWith("\"Level\":\"INFO\",\"Message\":\"5 this should override first file end5\",\"Properties\":{}}" + Environment.NewLine + "]"));
                Assert.IsTrue(z.Trim().EndsWith("\"Level\":\"INFO\",\"Message\":\"3awmdoawjdoaiwjdioawjd {0} end3\",\"Properties\":{\"arg0\":\"[adwnipdnaiwpndipawndpainwdpianwdianwdinawdi, adawdawddamwdoijawJD@(*EY&)!Y@EH]\"}}" + Environment.NewLine + "]"));
            }
        }

        private void AssertStackTrace(string stackTrace, string file_name, string func_name, int x_line_number)
        {
            var fullTrace = GetStackTrace(stackTrace);
            var n = fullTrace.Length;

            var globalFuncFound = false;
            var globalFileFound = false;
            var globalLineFound = false;

            var sb = new StringBuilder(1024);

            sb.AppendLine($"[AssertStackTrace] Checking for <{file_name}> function <{func_name}:{x_line_number}>");
            var success = false;

            sb.AppendLine($"Stacktrace:");
            foreach (var line in fullTrace)
            {
                sb.AppendLine($" {line}");
            }
            sb.AppendLine($"Stacktrace end");

            var hasLineInfo = true;
            for (var skipExpected = 0; skipExpected < n; skipExpected++)
            {
                var line = fullTrace[skipExpected];

                if (line.Contains(">:0"))
                    hasLineInfo = false;

                var funcFound = string.IsNullOrEmpty(func_name) || line.Contains(func_name);
                var fileFound = line.Contains($"{file_name}:");
                var lineFound = line.Contains($":{x_line_number}");

                globalFuncFound = globalFuncFound || funcFound;
                globalFileFound = globalFileFound || fileFound;
                globalLineFound = globalLineFound || lineFound;

                var found = funcFound;
                if (hasLineInfo)
                {
                    found = found && fileFound && lineFound;
                }

                if (found)
                {
                    if (skipExpected <= 1)
                    {
                        success = true;
                        sb.AppendLine($"[AssertStackTrace] Check successful");
                    }
                    else
                        sb.AppendLine($"[AssertStackTrace] Stack frame was found, but SkipFrames should be += {skipExpected}");

                    break;
                }
            }

            sb.AppendLine(hasLineInfo ? "Stacktrace has file and line info" : "No file/line info detected, not checking for them");

            if (success == false)
            {
                if (globalFuncFound)
                    sb.AppendLine($"[AssertStackTrace] func {func_name} was found");

                if (hasLineInfo)
                {
                    if (globalFileFound)
                        sb.AppendLine($"[AssertStackTrace] filename {file_name} was found");
                    if (globalLineFound)
                        sb.AppendLine($"[AssertStackTrace] line {x_line_number} was found");
                }

                UnityEngine.Debug.LogError($"[AssertStackTrace] Check Failed! {sb}");
                Assert.IsFalse(true, $"[AssertStackTrace] Check Failed! {sb}");
            }
        }

        private int GetLineInfo([CallerLineNumber] int lineNumber = 0)
        {
            return lineNumber;
        }

        private string[] GetStackTrace(string x)
        {
            const string startString = "\"Stacktrace\":\"";
            var i1 = x.IndexOf(startString, StringComparison.Ordinal) + startString.Length;
            var i2 = x.IndexOf("\"", i1 + 1, StringComparison.Ordinal);
            var stacktrace = x.Substring(i1, i2 - i1).Replace("\\r", "").Replace("\\n", "\n");
            return stacktrace.Split('\n')
                             .Where(l => string.IsNullOrEmpty(l) == false)
                             .Select(l => l.Trim())
                             .ToArray();
        }
    }
}
