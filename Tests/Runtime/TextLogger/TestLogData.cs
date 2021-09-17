using System;
using UnityEngine.Assertions;
using Unity.Collections;
using UnityEngine;

namespace Unity.Logging.Tests
{
    public struct TestLogData
    {
        public static readonly LogLevel[] AllLevels = (LogLevel[])Enum.GetValues(typeof(LogLevel));
        public static readonly LogDataType[] AllLogDataTypes = (LogDataType[])Enum.GetValues(typeof(LogDataType));

        public enum LogDataType
        {
            JustMessage,
            MessageAndInt,
            MessageAndComplexType
        }

        public struct ComplexType
        {
            public bool unblittableBool;
            public char unblittableChar;
            public ulong someULong;

            public static ComplexType Random()
            {
                return new ComplexType
                {
                    unblittableBool = UnityEngine.Random.Range(0, 1000) > 500,
                    //unblittableChar = (char)UnityEngine.Random.Range(31, char.MaxValue),
                    unblittableChar = (char)UnityEngine.Random.Range('a', 'z'),
                    someULong = 2 * (ulong)UnityEngine.Random.Range(0, int.MaxValue)
                };
            }

            public static ComplexType GenerateFrom(int i)
            {
                FixedString512Bytes rndChars = "$%#@!*abcdefghijklmnopqrstuvwxyz1234567890?;:ABCDEFGHIJKLMNOPQRSTUVWXYZ^&";
                return new ComplexType
                {
                    unblittableBool = i % 2 == 0,
                    unblittableChar = (char)rndChars[Math.Abs(i) % rndChars.Length],
                    someULong = (ulong)i
                };
            }

            public override string ToString()
            {
                return $"[{unblittableBool}, {unblittableChar}, {someULong}]";
            }
        }


        public LogLevel level;
        public FixedString512Bytes messageWithPrefix;
        public LogDataType dataType;

        public int integer;

        public ComplexType complex => ComplexType.GenerateFrom(integer);

        public delegate void ValidateParsePatternDelegate(in string line, out long timestamp, out LogLevel level, out string message);

        public long Validate(in TemplateParsedMessage obj)
        {
            Assert.AreEqual(level, obj.level, "Validate string failed - level is wrong");
            Assert.AreEqual(ToString(), obj.message, "Validate string failed - message is wrong");
            Assert.IsTrue(obj.timestamp > 0, "Validate string failed - Timestamp is 0 or negative");
            return obj.timestamp;
        }

        public long Validate(JsonEntryElement obj)
        {
            Assert.AreEqual(level.ToString().ToLowerInvariant(), obj.Level.ToLowerInvariant(), "Validate json failed - level is wrong");
            Assert.AreEqual(messageWithPrefix, obj.Message, "Validate json failed - message is wrong");
            Assert.IsTrue(obj.Timestamp > 0, "Validate json failed - Timestamp is 0 or negative");

            switch (dataType)
            {
                case LogDataType.JustMessage:
                    break;
                case LogDataType.MessageAndInt:
                    Assert.AreEqual(integer.ToString(), obj.Properties.arg0, "Json validation failed. MessageAndInt");
                    break;
                case LogDataType.MessageAndComplexType:
                    Assert.AreEqual(complex.ToString(), obj.Properties.arg0, "Json validation failed. MessageAndComplexType");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            return (long)obj.Timestamp;
        }

        public override string ToString()
        {
            switch (dataType)
            {
                case LogDataType.JustMessage:
                    return messageWithPrefix.ToString();
                case LogDataType.MessageAndInt:
                    return string.Format(messageWithPrefix.ToString(), integer);
                case LogDataType.MessageAndComplexType:
                    return string.Format(messageWithPrefix.ToString(), complex);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void CallNewLog(LoggerHandle loggerHandle = default)
        {
            // I could do
            // if (loggerHandle.IsValid == false)
            //    loggerHandle = Log.Logger.Handle;
            // but I have to test API - with .To(loggerHandle) and without, not write a nice looking code

            if (loggerHandle.IsValid)
            {
                switch (level)
                {
                    case LogLevel.Verbose:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.To(loggerHandle).Verbose(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.To(loggerHandle).Verbose(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.To(loggerHandle).Verbose(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    case LogLevel.Debug:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.To(loggerHandle).Debug(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.To(loggerHandle).Debug(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.To(loggerHandle).Debug(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    case LogLevel.Info:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.To(loggerHandle).Info(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.To(loggerHandle).Info(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.To(loggerHandle).Info(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    case LogLevel.Warning:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.To(loggerHandle).Warning(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.To(loggerHandle).Warning(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.To(loggerHandle).Warning(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    case LogLevel.Error:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.To(loggerHandle).Error(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.To(loggerHandle).Error(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.To(loggerHandle).Error(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    case LogLevel.Fatal:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.To(loggerHandle).Fatal(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.To(loggerHandle).Fatal(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.To(loggerHandle).Fatal(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                switch (level)
                {
                    case LogLevel.Verbose:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.Verbose(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.Verbose(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.Verbose(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    case LogLevel.Debug:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.Debug(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.Debug(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.Debug(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    case LogLevel.Info:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.Info(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.Info(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.Info(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    case LogLevel.Warning:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.Warning(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.Warning(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.Warning(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    case LogLevel.Error:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.Error(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.Error(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.Error(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    case LogLevel.Fatal:
                        switch (dataType)
                        {
                            case LogDataType.JustMessage:
                                Log.Fatal(messageWithPrefix);

                                break;
                            case LogDataType.MessageAndInt:
                                Log.Fatal(messageWithPrefix, integer);

                                break;
                            case LogDataType.MessageAndComplexType:
                                Log.Fatal(messageWithPrefix, complex);

                                break;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public LogType ToLogType()
        {
            switch (level)
            {
                case LogLevel.Warning:
                    return LogType.Warning;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    return LogType.Error;
                default:
                    return LogType.Log;
            }
        }
    }
}
