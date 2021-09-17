using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.Logging.Tests
{
    internal class TemplateTestPattern1
    {
        public static TemplateParsedMessage PatternParse(in string s)
        {
            const string prefix = "[1]@";
            const string postfix = "!";

            if (s.StartsWith(prefix) == false)
                throw new Exception("Wrong string");
            if (s.EndsWith(postfix) == false)
                throw new Exception("Wrong string");

            try
            {
                var w = s.Substring(0, s.Length - postfix.Length).Substring(prefix.Length);

                string timestampString;
                {
                    var splitStrings = w.Split('*');
                    timestampString = splitStrings[0];
                    w = splitStrings[1];
                }

                string levelString;
                {
                    var splitStrings = w.Split('^');
                    levelString = splitStrings[0];
                    w = splitStrings[1];
                }

                string message;
                {
                    var splitStrings = w.Split('|');
                    message = splitStrings[0];
                    w = splitStrings[1];
                }

                {
                    var splitStrings = w.Split('/');
                    var messageAgain = splitStrings[0];
                    if (messageAgain != message)
                        throw new Exception("Wrong string");
                    w = splitStrings[1];
                }

                {
                    var splitStrings = w.Split('\\');
                    var levelAgain = splitStrings[0];
                    if (levelAgain != levelString)
                        throw new Exception("Wrong string");
                    var timestampAgain = splitStrings[1];
                    if (timestampAgain != timestampString)
                        throw new Exception("Wrong string");
                }

                var timestamp = long.Parse(timestampString);
                var level = LogLevelUtilsBurstCompatible.Parse(levelString);

                return new TemplateParsedMessage(timestamp, level, message);
            }
            catch (Exception e)
            {
                Debug.LogError($"For <{s}> have an exception: {e}");

                return default;
            }
        }

        public static FixedString512Bytes PatternString => "[1]@{Timestamp}*{Level}^{Message}|{Message}/{Level}\\{Timestamp}!";
    }
}
