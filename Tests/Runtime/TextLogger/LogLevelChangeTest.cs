using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Logging.Tests
{
    public class LogLevelChangeTest : LoggingTestFixture
    {
        [Test]
        public void LogLevelCanBeChanged()
        {
            TimeStampWrapper.SetHandlerForTimestamp(RawGetTimestampHandlerAsCounter, true); // warmup: 1
            TextLoggerParser.SetOutputHandlerForTimestamp(RawTimestampHandler, true);

            Log.Logger = new LoggerConfig()
                         .MinimumLevel.Warning()
                         .WriteTo.StringLogger(outputTemplate: "{Timestamp} ^ {Level} |{Message}")
                         .CreateLogger();

            Log.Fatal("-- Warning --"); // 2

            Log.Verbose("1");
            Log.Info("2");
            Log.Warning("3"); // 3
            Log.Error("4"); // 4
            Log.Fatal("5"); // 5

            Log.Fatal("-- Info --"); // 6
            LoggerManager.FlushAll();
            Log.Logger.SetMinimalLogLevelAcrossAllSinks(LogLevel.Info);

            Log.Verbose("6");
            Log.Info("7"); //7
            Log.Warning("8"); //8
            Log.Error("9"); //9
            Log.Fatal("10");// 10

            Log.Fatal("-- Fatal --"); // 11
            LoggerManager.FlushAll();
            Log.Logger.SetMinimalLogLevelAcrossAllSinks(LogLevel.Fatal);

            Log.Verbose("11");
            Log.Info("12");
            Log.Warning("13");
            Log.Error("14");
            Log.Fatal("15"); // 12

            Log.Fatal("-- Verbose --"); // 13
            LoggerManager.FlushAll();
            Log.Logger.SetMinimalLogLevelAcrossAllSinks(LogLevel.Verbose);

            Log.Verbose("16"); //14
            Log.Info("17"); //15
            Log.Warning("18"); //16
            Log.Error("19"); // 17
            Log.Fatal("20"); // 18

            LoggerManager.FlushAll();

            var output1 = GetStringFromFirstTestSink(Log.Logger);

            Debug.Log(output1);

            var expected = @"2 ^ FATAL |-- Warning --
3 ^ WARNING |3
4 ^ ERROR |4
5 ^ FATAL |5
6 ^ FATAL |-- Info --
7 ^ INFO |7
8 ^ WARNING |8
9 ^ ERROR |9
10 ^ FATAL |10
11 ^ FATAL |-- Fatal --
12 ^ FATAL |15
13 ^ FATAL |-- Verbose --
14 ^ VERBOSE |16
15 ^ INFO |17
16 ^ WARNING |18
17 ^ ERROR |19
18 ^ FATAL |20
";


            output1 = output1.Replace("\r", "");
            expected = expected.Replace("\r", "");

            Assert.AreEqual(expected, output1);

            LoggerManager.DeleteAllLoggers();
        }
    }
}
