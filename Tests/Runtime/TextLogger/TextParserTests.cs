using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Logging.Tests
{
    [TestFixture]
    public unsafe class TextParserTests
    {
        const string testString1 = "The quick brown fox jumps over the lazy dog.";
        const string testString2 = "\u05DE\u05D3\u05D5\u05E8\u05D9\u05DD \u05DE\u05D1\u05D5\u05E7\u05E9\u05D9\u05DD";
        const string testString3 = "\u0E41\u0E1C\u0E48\u0E19\u0E14\u0E34\u0E19\u0E2E\u0E31\u0E48\u0E19\u0E40\u0E2A\u0E37\u0E48\u0E2D\u0E21\u0E42\u0E17\u0E23";
        const string testString4 = "Quizdeltagerne spiste jordbær med fløde, mens cirkusklovnen Wolther spillede på xylofon.";
        const string testString5 = "\u0414\u0435\u0441\u044F\u0442\u0443\u044E \u041C\u0435\u0436\u0434\u0443\u043D\u0430\u0440\u043E\u0434\u043D\u0443\u044E";
        const string testString6 = "Portez ce vieux whisky au juge blond qui fume sur son île intérieure, à\n" +
            "côté de l'alcôve ovoïde, où les bûches se consument dans l'âtre, ce\n" +
            "qui lui permet de penser à la cænogenèse de l'être dont il est question\n" +
            "dans la cause ambiguë entendue à Moÿ, dans un capharnaüm qui,\n" +
            "pense-t - il, diminue çà et là la qualité de son uvre.";

        const string testString7 = "The {0} brown fox jumps {1} the lazy {2}.";
        const string testString8 = "Portez {2} vieux whisky au juge blond {1} fume sur son île intérieure, à";
        const string testString9 = "{0} world: {1,4} verify {1} brace {1:C} positioning {2}";
        const string testStringA = "Testing {{1}} {{{1}}} {{2 3}} {{";
        const string testStringB = "More {0} Testing {1}";

        const string testStringC = "{{{2}}}Quizdeltagerne {1} spiste {{jordbær{0}spillede}} på xylofon.}}";

        const string errorString1 = "Missing context argument: {2}";

        [SetUp]
        public void Initialize()
        {
            TextParserWrapper.Initialize();
        }

        [TearDown]
        public void Shutdown()
        {
            TextParserWrapper.Shutdown();
        }

        // See https://messagetemplates.org/

        [TestCase("")]                  // illegal name - empty
        [TestCase("{}")]                // illegal name - empty
        [TestCase("{ Hi! }")]           // illegal name - space
        [TestCase("{0 space}")]         // illegal name - space
        [TestCase("{w@rld}")]           // illegal name
        [TestCase("{H&llo}")]           // illegal name
        [TestCase("{-1}")]              // invalid numeric - parsed as text
        [TestCase("{-0}")]              // invalid numeric - parsed as text
        [TestCase("{3.1415}")]          // invalid numeric - parsed as text
        [TestCase("{Hello,0}")]         // zero alignment - parsed as text
        [TestCase("{Hello,-0}")]        // zero alignment - parsed as text
        [TestCase("{Hello,-aa}")]       // non number alignment - parsed as text
        [TestCase("{Hello,aa}")]        // non number alignment - parsed as text
        [TestCase("{Hello,-10-1}")]     // non number alignment - parsed as text
        [TestCase("{Hello,10-1}")]      // non number alignment - parsed as text
        [TestCase("{Hello,}")]          // empty alignment - parsed as text
        [TestCase("{Hello,:format}")]   // empty alignment - parsed as text
        [TestCase("{@$}")]              // empty name - parsed as text
        [TestCase("{$@}")]              // empty name - parsed as text
        [TestCase("{@}")]               // empty name - parsed as text
        [TestCase("{$}")]               // empty name - parsed as text
        public void ParserIllegalArgumentTest(string input)
        {
            var template = (FixedString512Bytes)input;

            var rawMsgBuffer = template.GetUnsafePtr();
            var currMsgSegment = new TextLoggerParser.ParseSegment
            {
                Offset = 0,
                Length = template.Length
            };

            var argumentInfo = TextLoggerParser.ParseArgument(rawMsgBuffer, template.Length, currMsgSegment);

            Assert.IsFalse(argumentInfo.IsValid);
        }

        [TestCase("{0_}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "0_", -1, 0, "")]
        [TestCase("{0}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "", 0, 0, "")]
        [TestCase("{3}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "", 3, 0, "")]
        [TestCase("{3,-5}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "", 3, -5, "")]
        [TestCase("{4,-50:000}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "", 4, -50, "000")]
        [TestCase("{@4,-50:000}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "", 4, -50, "000")]
        [TestCase("{@AF,-50:##.0;-##.0;zero}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "AF", -1, -50, "##.0;-##.0;zero")]
        [TestCase("{23:000,50}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "", 23, 0, "000,50")]
        [TestCase("{Time:hh:mm}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Time", -1, 0, "hh:mm")]
        [TestCase("{Hello,-5}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, -5, "")]
        [TestCase("{Hello,-50}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, -50, "")]
        [TestCase("{Hello,5}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 5, "")]
        [TestCase("{Hello,50}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 50, "")]
        [TestCase("{Hello,-5:000}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, -5, "000")]
        [TestCase("{Hello,-50:000}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, -50, "000")]
        [TestCase("{Hello,5:000}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 5, "000")]
        [TestCase("{Hello,50:000}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 50, "000")]
        [TestCase("{Hello:000,-5}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 0, "000,-5")]   // 000,-5 is format
        [TestCase("{Hello:000,-50}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 0, "000,-50")] // 000,-50 is format
        [TestCase("{Hello:000,5}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 0, "000,5")]     // 000,5 is format
        [TestCase("{Hello:000,50}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 0, "000,50")]   // 000,50 is format
        [TestCase("{@Hello}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "Hello", -1, 0, "")]
        [TestCase("{$Hello}", TextLoggerParser.ArgumentInfo.DestructingType.Stringify, "Hello", -1, 0, "")]
        [TestCase("{_123_Hello}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "_123_Hello", -1, 0, "")]                        // _ are valid
        [TestCase("{Number:##.0;-##.0;zero}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Number", -1, 0, "##.0;-##.0;zero")] // format can contain multiple sections
        [TestCase("{Number:+##.0}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Number", -1, 0, "+##.0")]                     // format can contain plus

        [TestCase("{Привет}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Привет", -1, 0, "")]
        [TestCase("{@Привет}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "Привет", -1, 0, "")]
        [TestCase("{@Привет:##.0;При!#}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "Привет", -1, 0, "##.0;При!#")]
        [TestCase("{@Привет,-50:##.0;При!#}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "Привет", -1, -50, "##.0;При!#")]
        [TestCase("{@їє_v_при:##.0;При!#}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "їє_v_при", -1, 0, "##.0;При!#")]
        [TestCase("{ඖaĔĔaĔඖĔ:+aǊǊǊ𒀖Ǒඖ𒀖}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "ඖaĔĔaĔඖĔ", -1, 0, "+aǊǊǊ𒀖Ǒඖ𒀖")]

        [TestCase("{@їє_v_при,-50:##.0;При!#}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "їє_v_при", -1, -50, "##.0;При!#")]
        [TestCase("{ඖaĔĔaĔඖĔ,42:+aǊǊǊ𒀖Ǒඖ𒀖}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "ඖaĔĔaĔඖĔ", -1, 42, "+aǊǊǊ𒀖Ǒඖ𒀖")]

        // 3 bytes: ॵ ઔඖ
        // 4 bytes: 𒁂𒀂𒀖
        public void ParserArgumentTest(string input, TextLoggerParser.ArgumentInfo.DestructingType expectedDestructing, string expectedName, int expectedIndex, int expectedAlignment, string expectedFormat)
        {
            var template = (FixedString512Bytes)input;

            var rawMsgBuffer = template.GetUnsafePtr();
            var currMsgSegment = new TextLoggerParser.ParseSegment
            {
                Offset = 0,
                Length = template.Length
            };

            var argumentInfo = TextLoggerParser.ParseArgument(rawMsgBuffer, template.Length, currMsgSegment);

            Assert.IsTrue(argumentInfo.IsValid);

            Assert.AreEqual(expectedAlignment, argumentInfo.Alignment);
            Assert.AreEqual(expectedDestructing, argumentInfo.Destructing);
            Assert.AreEqual(expectedFormat, argumentInfo.Format);
            Assert.AreEqual(expectedIndex, argumentInfo.Index);
            Assert.AreEqual(expectedName, argumentInfo.Name);

            //Assert.AreEqual(template, argumentInfo.RawString);
        }

        [TestCase("{0_}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "0_", -1, 0, "")]
        [TestCase("{0}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "", 0, 0, "")]
        [TestCase("{3}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "", 3, 0, "")]
        [TestCase("{3,-5}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "", 3, -5, "")]
        [TestCase("{4,-50:000}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "", 4, -50, "000")]
        [TestCase("{@4,-50:000}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "", 4, -50, "000")]
        [TestCase("{@AF,-50:##.0;-##.0;zero}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "AF", -1, -50, "##.0;-##.0;zero")]
        [TestCase("{23:000,50}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "", 23, 0, "000,50")]
        [TestCase("{Time:hh:mm}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Time", -1, 0, "hh:mm")]
        [TestCase("{Hello,-5}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, -5, "")]
        [TestCase("{Hello,-50}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, -50, "")]
        [TestCase("{Hello,5}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 5, "")]
        [TestCase("{Hello,50}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 50, "")]
        [TestCase("{Hello,-5:000}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, -5, "000")]
        [TestCase("{Hello,-50:000}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, -50, "000")]
        [TestCase("{Hello,5:000}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 5, "000")]
        [TestCase("{Hello,50:000}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 50, "000")]
        [TestCase("{Hello:000,-5}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 0, "000,-5")]   // 000,-5 is format
        [TestCase("{Hello:000,-50}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 0, "000,-50")] // 000,-50 is format
        [TestCase("{Hello:000,5}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 0, "000,5")]     // 000,5 is format
        [TestCase("{Hello:000,50}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Hello", -1, 0, "000,50")]   // 000,50 is format
        [TestCase("{@Hello}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "Hello", -1, 0, "")]
        [TestCase("{$Hello}", TextLoggerParser.ArgumentInfo.DestructingType.Stringify, "Hello", -1, 0, "")]
        [TestCase("{_123_Hello}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "_123_Hello", -1, 0, "")]                        // _ are valid
        [TestCase("{Number:##.0;-##.0;zero}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Number", -1, 0, "##.0;-##.0;zero")] // format can contain multiple sections
        [TestCase("{Number:+##.0}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Number", -1, 0, "+##.0")]                     // format can contain plus

        [TestCase("{Привет}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "Привет", -1, 0, "")]
        [TestCase("{@Привет}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "Привет", -1, 0, "")]
        [TestCase("{@Привет:##.0;При!#}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "Привет", -1, 0, "##.0;При!#")]
        [TestCase("{@Привет,-50:##.0;При!#}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "Привет", -1, -50, "##.0;При!#")]
        [TestCase("{@їє_v_при:##.0;При!#}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "їє_v_при", -1, 0, "##.0;При!#")]
        [TestCase("{ඖaĔĔaĔඖĔ:+aǊǊǊ𒀖Ǒඖ𒀖}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "ඖaĔĔaĔඖĔ", -1, 0, "+aǊǊǊ𒀖Ǒඖ𒀖")]

        [TestCase("{@їє_v_при,-50:##.0;При!#}", TextLoggerParser.ArgumentInfo.DestructingType.Destructure, "їє_v_при", -1, -50, "##.0;При!#")]
        [TestCase("{ඖaĔĔaĔඖĔ,42:+aǊǊǊ𒀖Ǒඖ𒀖}", TextLoggerParser.ArgumentInfo.DestructingType.Default, "ඖaĔĔaĔඖĔ", -1, 42, "+aǊǊǊ𒀖Ǒඖ𒀖")]

        // 3 bytes: ॵ ઔඖ
        // 4 bytes: 𒁂𒀂𒀖
        public void ParserArgumentTestNonZeroOffset(string input, TextLoggerParser.ArgumentInfo.DestructingType expectedDestructing, string expectedName, int expectedIndex, int expectedAlignment, string expectedFormat)
        {
            const string prefix = "{{@їadiǊǊjwaoid";
            var template = (FixedString512Bytes)(prefix + input + input);

            var rawMsgBuffer = template.GetUnsafePtr();
            var currMsgSegment = new TextLoggerParser.ParseSegment
            {
                Offset = ((FixedString512Bytes)prefix).Length,
                Length = ((FixedString512Bytes)input).Length
            };

            var argumentInfo = TextLoggerParser.ParseArgument(rawMsgBuffer, template.Length, currMsgSegment);

            Assert.IsTrue(argumentInfo.IsValid);

            Assert.AreEqual(expectedAlignment, argumentInfo.Alignment);
            Assert.AreEqual(expectedDestructing, argumentInfo.Destructing);
            Assert.AreEqual(expectedFormat, argumentInfo.Format);
            Assert.AreEqual(expectedIndex, argumentInfo.Index);
            Assert.AreEqual(expectedName, argumentInfo.Name);

            //Assert.AreEqual(input, argumentInfo.RawString);
        }

        [TestCase("", "")]
        [TestCase("{}", "{}")]
        [TestCase("{{ Hi! }", "{ Hi! }")]
        [TestCase("Well, {{ Hi!", "Well, { Hi!")]
        [TestCase("Hello, {{worl@d}!", "Hello, {worl@d}!")]
        [TestCase("Nice }}-: mo", "Nice }-: mo")]
        [TestCase("{World}}!", "}!")]
        [TestCase("{Hello", "{Hello")]
        [TestCase("Hello, {World}}!", "Hello, }!")]
        [TestCase("{{Hi}}", "{Hi}")]
        [TestCase("Hello, {{worl@d}}!", "Hello, {worl@d}!")]
        [TestCase("{0 space}", "{0 space}")]
        [TestCase("{0 space", "{0 space")]
        [TestCase("{0_space", "{0_space")]
        [TestCase("{0_{{space}", "{0_{{space}")]
        [TestCase("{0_{{space", "{0_{{space")]
        [TestCase("{0_}}space}", "}space}")]
        [TestCase("Hello, {w@rld}", "Hello, {w@rld}")]

        // malformed property names
        [TestCase("Hello, {w@rld", "Hello, {w@rld")]
        [TestCase("Hello, {w{{rld", "Hello, {w{{rld")]
        [TestCase("Hello{{, {w{{rld", "Hello{, {w{{rld")]
        [TestCase("Hello, {w@rld}, HI!", "Hello, {w@rld}, HI!")]
        [TestCase("{w@rld} Hi!", "{w@rld} Hi!")]
        [TestCase("{H&llo}, {w@rld}", "{H&llo}, {w@rld}")]

        [TestCase("{0}", "")]
        [TestCase("{0}, {1}, {2}", ", , ")]
        [TestCase("{-1}{-0}{0}{1}{3.1415}", "{-1}{-0}{3.1415}")] // invalid numeric - parsed as text
        [TestCase("{Time:hh:mm}", "")]
        [TestCase("{Hello,-5}", "")]
        [TestCase("{Hello,-50}", "")]
        [TestCase("{Hello,5}", "")]
        [TestCase("{Hello,50}", "")]
        [TestCase("{Hello,-5:000}", "")]
        [TestCase("{Hello,-50:000}", "")]
        [TestCase("{Hello,5:000}", "")]
        [TestCase("{Hello,50:000}", "")]
        [TestCase("{Hello:000,-5}", "")]                 // 000,-5 is format
        [TestCase("{Hello:000,-50}", "")]                // 000,-50 is format
        [TestCase("{Hello:000,5}", "")]                  // 000,5 is format
        [TestCase("{Hello:000,50}", "")]                 // 000,50 is format
        [TestCase("{Hello,0}", "{Hello,0}")]             // zero alignment - parsed as text
        [TestCase("{Hello,-0}", "{Hello,-0}")]           // zero alignment - parsed as text
        [TestCase("{Hello,-aa}", "{Hello,-aa}")]         // non number alignment - parsed as text
        [TestCase("{Hello,aa}", "{Hello,aa}")]           // non number alignment - parsed as text
        [TestCase("{Hello,-10-1}", "{Hello,-10-1}")]     // non number alignment - parsed as text
        [TestCase("{Hello,10-1}", "{Hello,10-1}")]       // non number alignment - parsed as text
        [TestCase("{Hello,}", "{Hello,}")]               // empty alignment - parsed as text
        [TestCase("{Hello,:format}", "{Hello,:format}")] // empty alignment - parsed as text
        [TestCase("{@Hello}", "")]
        [TestCase("{$Hello}", "")]
        [TestCase("{@$}", "{@$}")]                 // empty name - parsed as text
        [TestCase("{$@}", "{$@}")]                 // empty name - parsed as text
        [TestCase("{@}", "{@}")]                   // empty name - parsed as text
        [TestCase("{$}", "{$}")]                   // empty name - parsed as text
        [TestCase("{_123_Hello}", "")]             // _ are valid
        [TestCase("{Number:##.0;-##.0;zero}", "")] // format can contain multiple sections
        [TestCase("{Number:+##.0}", "")]           // format can contain plus
        public void ParserTest(string input, string expected)
        {
            var template = (FixedString512Bytes)input;

            byte* rawMsgBuffer = template.GetUnsafePtr();
            var currMsgSegment = new TextLoggerParser.ParseSegment();

            var messageOutput = new NativeText(Allocator.Temp);

            var done = false;
            var success = true;
            do
            {
                var result = TextLoggerParser.FindNextParseStringSegment(rawMsgBuffer, template.Length, ref currMsgSegment, out var currArgSlot);

                success = messageOutput.Append(&rawMsgBuffer[currMsgSegment.Offset], currMsgSegment.Length) == FormatError.None && success;

                switch (result)
                {
                    case TextLoggerParser.ParseContextResult.EscOpenBrace:
                    {
                        success = messageOutput.Append('{') == FormatError.None && success;
                        break;
                    }
                    case TextLoggerParser.ParseContextResult.EscCloseBrace:
                    {
                        success = messageOutput.Append('}') == FormatError.None && success;
                        break;
                    }
                    case TextLoggerParser.ParseContextResult.NormalArg:
                    {
                        var argumentInfo = TextLoggerParser.ParseArgument(rawMsgBuffer, template.Length, currArgSlot);
                        if (argumentInfo.IsValid)
                        {
                        }
                        else
                        {
                            success = messageOutput.Append(&rawMsgBuffer[currArgSlot.Offset], currArgSlot.Length) == FormatError.None && success;
                        }

                        break;
                    }
                    case TextLoggerParser.ParseContextResult.NoArgs:
                        done = true;
                        break;
                }

                currMsgSegment.Offset = currArgSlot.OffsetEnd;
                if (currMsgSegment.Offset >= template.Length)
                    done = true;
            }
            while (!done);

            UnityEngine.Assertions.Assert.AreEqual(expected, messageOutput.ToString());
        }

        [TestCase(testString1)]
        [TestCase(testString2)]
        [TestCase(testString3)]
        [TestCase(testString4)]
        [TestCase(testString5)]
        [TestCase(testString6)]
        public void ParseMessagesWithoutContexts(string message)
        {
            var d = MessageData.Create(message);
            Assert.IsTrue(TextParserWrapper.WriteTextMessage(ref d, out var messageOutput, out var errorMessage), $"WriteTextMessage failed: '{errorMessage}'");
            Assert.IsEmpty(errorMessage, $"WriteTextMessage returned an error message: '{errorMessage}'");
            Assert.AreEqual(message, messageOutput, $"Output message doesn't match expected string: `{message}`");
        }

        [TestCase(testString7)]
        [TestCase(testString8)]
        [TestCase(testString9)]
        [TestCase(testStringA)]
        [TestCase(testStringB)]
        [TestCase(testStringC)]
        public void ParseMessageWithContexts(string message)
        {
            // Shuffle the order of the context data
            var contexts = new List<IContextStruct>(3);
            contexts.Add(BasicContextData1.Create());
            contexts.Add(BasicContextData2.Create());
            contexts.Add(BasicContextData3.Create());

            var rand = new System.Random();
            var shuffledContexts = contexts.OrderBy(x => rand.Next()).ToArray();

            var data = MessageData.Create(message, shuffledContexts);
            Assert.IsTrue(TextParserWrapper.WriteTextMessage(ref data, out var messageOutput, out var errorMessage), $"WriteTextMessage failed: '{errorMessage}'");
            Assert.IsEmpty(errorMessage, $"WriteTextMessage returned an error message: '{errorMessage}'");

            var expMessage = string.Format(message, shuffledContexts[0].GetFormattedFields(), shuffledContexts[1].GetFormattedFields(), shuffledContexts[2].GetFormattedFields());
            Assert.AreEqual(expMessage, messageOutput, $"Output message doesn't match expected string: `{expMessage}`");
        }

        [TestCase(errorString1)]
        public void ParseMessageFailsWithMissingContextArguments(string message)
        {
            Internal.Debug.SelfLog.SetMode(Internal.Debug.SelfLog.Mode.EnabledInMemory);
            using (var scope = new Internal.Debug.SelfLog.Assert.TestScope(Allocator.Persistent))
            {
                scope.ExpectErrorThatContains("argument index");

                var data = MessageData.Create(message, BasicContextData1.Create(), BasicContextData2.Create());
                Assert.IsFalse(TextParserWrapper.WriteTextMessage(ref data, out _, out var errorMessage));
                Assert.IsTrue(errorMessage.ToLower().Contains("argument index"));
            }
        }
    }
}
