using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.Logging.Tests
{
    internal static class TemplateTestPattern3
    {
        public static TemplateParsedMessage PatternParse(in string s)
        {
            var w = s;

            try
            {
                string timestampString;
                {
                    var splitStrings = w.Split('@');
                    timestampString = splitStrings[1];
                    w = splitStrings[0];
                }

                string message;
                string levelString;
                {
                    var splitStrings = w.Split('|');
                    levelString = splitStrings[1];
                    message = splitStrings[0];
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

        public static FixedString512Bytes PatternString => "{Message}|{Level}@{Timestamp}";
    }
}
