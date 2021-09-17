using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.Logging.Tests
{
    internal class TemplateTestPattern2
    {
        public static TemplateParsedMessage PatternParse(in string s)
        {
            const string prefix = "[2]@";
            const string postfix = "_end";

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

                string message;
                string levelString;
                {
                    var splitStrings = w.Split('^');
                    levelString = splitStrings[0];
                    message = splitStrings[1];
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

        public static FixedString512Bytes PatternString => "[2]@{Timestamp}*{Level}^{Message}_end";
    }
}
