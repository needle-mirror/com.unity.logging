using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.Assertions;

namespace Unity.Logging.Tests
{
    public class JsonLogValidator
    {
        class JsonLogValidatorScope : IDisposable
        {
            private JsonLogValidator m_parent;
            private string m_key;

            public JsonLogValidatorScope(JsonLogValidator parent, string key, string value = null)
            {
                m_parent = parent;
                m_key = key;

                Assert.IsFalse(m_parent.m_DecorDict.ContainsKey(key));
                m_parent.m_DecorDict.Add(key, value);
            }

            public void Dispose()
            {
                m_parent.m_DecorDict.Remove(m_key);
            }
        }

        public struct NamedArgument
        {
            public string Name;
            public object Obj;
        }

        class ExpectedMessage
        {
            public readonly string Message;
            public readonly Dictionary<string, string> Parameters;

            public ExpectedMessage(string message, object[] args, Dictionary<string, string> decorDict)
            {
                Message = message;
                Parameters = new Dictionary<string, string>(decorDict);

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] is NamedArgument namedArgument)
                        Parameters[namedArgument.Name] = namedArgument.Obj.ToString();
                    else
                        Parameters["arg" + i] = args[i].ToString();
                }
            }
        }

        private readonly bool m_RequireStackTrace;
        private readonly Logger m_Log;
        private readonly Dictionary<string, string> m_DecorDict = new Dictionary<string, string>();
        private readonly List<ExpectedMessage> m_ExpectedMessages = new List<ExpectedMessage>();

        public JsonLogValidator(Logger log, bool requireStackTrace)
        {
            m_RequireStackTrace = requireStackTrace;
            m_Log = log;
        }

        public void Validate(JArray arr)
        {
            AssertJsonTimestampSorted(arr);
            Assert.AreEqual(m_ExpectedMessages.Count, arr.Count, m_ExpectedMessages.Count > arr.Count ? "Some messages are missing!" : "There were more messages than expected");

            for (int i = 0; i < m_ExpectedMessages.Count; i++)
            {
                var elem = arr[i];
                var expElem = m_ExpectedMessages[i];

                if (m_RequireStackTrace)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(elem["Stacktrace"].Value<string>()), $"Stacktrace is null/empty: {elem}");
                }

                Assert.AreEqual(expElem.Message, elem["Message"].Value<string>(), "Wrong message");
                Assert.AreEqual("INFO", elem["Level"].Value<string>(), "Wrong level");

                foreach (var keyValue in expElem.Parameters)
                {
                    var v = elem["Properties"][keyValue.Key];
                    if (keyValue.Value == null)
                        Assert.IsNotNull(v, $"'{keyValue.Key}' is missing in '{expElem.Message}' log message");
                    else
                        Assert.AreEqual(keyValue.Value, v != null ? v.Value<string>() : null, v == null ? $"'{keyValue.Key}' is missing in '{expElem.Message}' log message" : $"'{keyValue.Key}' is wrong in '{expElem.Message}' log message");
                }
                Assert.AreEqual(expElem.Parameters.Count, elem["Properties"].Count(), expElem.Parameters.Count > elem["Properties"].Count() ? $"Some properties are missing! for '{expElem.Message}'" : $"There were more properties than expected for '{expElem.Message}'");
            }
        }

        public IDisposable RegisterDecoratorStart(string key)
        {
            return new JsonLogValidatorScope(this, key);
        }

        public IDisposable RegisterDecoratorStart(string key, int value)
        {
            return new JsonLogValidatorScope(this, key, value.ToString());
        }

        public IDisposable RegisterDecoratorStart(string key, string value)
        {
            return new JsonLogValidatorScope(this, key, value);
        }

        public void Info(string message, params object[] args)
        {
            m_ExpectedMessages.Add(new ExpectedMessage(message, args, m_DecorDict));
        }

        private static void AssertJsonTimestampSorted(JArray arr)
        {
            for (var i = 1; i < arr.Count; i++)
            {
                var prev = arr[i - 1]["Timestamp"].Value<long>();
                var now = arr[i]["Timestamp"].Value<long>();
                Assert.IsTrue(now >= prev, "timestamps are not sorted");
            }
        }
    }
}
