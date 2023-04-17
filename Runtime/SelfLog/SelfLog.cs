using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Logging.Sinks;

namespace Unity.Logging.Internal.Debug
{
    /// <summary>
    /// Static class for Self-Logging mechanism in Unity Logging. Use this to debug Sinks / Logging.
    /// </summary>
    public static class SelfLog
    {
        /// <summary>
        /// SelfLog Mode
        /// </summary>
        public enum Mode
        {
            /// <summary>
            /// Disable SelfLog
            /// </summary>
            Disabled = 0,
            /// <summary>
            /// Messages will be added to in-memory Assert storage
            /// </summary>
            EnabledInMemory,
            /// <summary>
            /// Messages will be added to in-memory Assert storage and redirected to UnityEngine.Debug.LogError
            /// </summary>
            EnabledInUnityEngineDebugLogError
        }

        /// <summary>
        /// Current SelfLog <see cref="Mode"/>
        /// </summary>
        private static readonly SharedStatic<Mode> CurrentMode = SharedStatic<Mode>.GetOrCreate<Mode>(16);

        /// <summary>
        /// True if <see cref="Mode"/> is not <see cref="Mode.Disabled"/>
        /// </summary>
        public static bool IsEnabled => CurrentMode.Data != Mode.Disabled;

        /// <summary>
        /// Sets current SelfLog <see cref="Mode"/>
        /// </summary>
        /// <param name="modeToSet">New mode to set</param>
        public static void SetMode(Mode modeToSet)
        {
            CurrentMode.Data = modeToSet;
        }

        /// <summary>
        /// On Fatal Error in the Sink
        /// </summary>
        /// <param name="sinkSystem">Sink that caused the error</param>
        /// <param name="reason">Description of the error</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnSinkFatalError(SinkSystemBase sinkSystem, FixedString512Bytes reason)
        {
            WriteToSelfLog(reason);
        }

        /// <summary>
        /// On Failure in <see cref="LogMemoryManager"/>
        /// </summary>
        /// <param name="stateReport">Debug report from <see cref="LogMemoryManager.DebugStateString"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnFailedToAllocateMemory(FixedString4096Bytes stateReport)
        {
            WriteToSelfLog(stateReport);
        }

        /// <summary>
        /// On Failure connected to disjointed buffer allocation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnFailedToCreateDisjointedBuffer()
        {
            WriteToSelfLog(Errors.FailedToCreateDisjointedBuffer);
        }

        /// <summary>
        /// On Failure in LogMessage parsing
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnFailedToParseMessage()
        {
            WriteToSelfLog(Errors.FailedToParseMessage);
        }

        /// <summary>
        /// On Failure if requested size is outside [MinimumPayloadSize, MaximumPayloadSize]
        /// </summary>
        /// <param name="requestedSize">size that is not correct</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnFailedToAllocatePayloadBecauseOfItsSize(uint requestedSize)
        {
            var msg = Errors.FailedToAllocatePayloadBecauseOfItsSize;
            msg.Append(requestedSize);
            WriteToSelfLog(msg);
        }

        /// <summary>
        /// Template is empty - so nothing will be logged
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnEmptyTemplate()
        {
            WriteToSelfLog(Errors.EmptyTemplateForTextLogger);
        }

        /// <summary>
        /// Payload had a TypeId wasn't parsed because the list of parsers was empty for some reason
        /// </summary>
        /// <param name="typeId">TypeId that is unknown</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnUnknownTypeIdBecauseOfEmptyHandlers(ulong typeId)
        {
            var msg = Errors.UnknownTypeIdBecauseOfEmptyHandlers;
            msg.Append(typeId);
            WriteToSelfLog(msg);
        }

        /// <summary>
        /// Payload had a TypeId that no parser know how to parse
        /// </summary>
        /// <param name="typeId">TypeId that is unknown</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnUnknownTypeId(ulong typeId)
        {
            var msg = Errors.UnknownTypeId;
            msg.Append(typeId);
            WriteToSelfLog(msg);
        }

        /// <summary>
        /// Write error to SelfLog
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        public static void Error(FixedString512Bytes errorMessage)
        {
            WriteToSelfLog(errorMessage);
        }

        /// <summary>
        /// Write error to SelfLog
        /// </summary>
        /// <param name="message">Error message</param>
        private static void WriteToSelfLog(FixedString4096Bytes message)
        {
            if (IsEnabled)
            {
                Assert.CheckMessage(message);

                if (CurrentMode.Data == Mode.EnabledInUnityEngineDebugLogError)
#if UNITY_DOTSRUNTIME
                    UnityEngine.Debug.LogError(message);
#else
                    UnityLogRedirectorManager.UnityLogError(ref message);
#endif
            }
        }

        /// <summary>
        /// Assert mechanism that can be used in testing of Logging system. Uses <see cref="SelfLog"/>
        /// </summary>
        public static class Assert
        {
            private static readonly List<TestScope> Scopes = new List<TestScope>(32);

            [BurstDiscard]
            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
            internal static void CheckMessage(FixedString4096Bytes message)
            {
                if (Scopes.Count == 0) return;
                var current = Scopes[Scopes.Count - 1];
                if (current.IsCreated)
                    current.CheckMessage(message);
            }

            /// <summary>
            /// Test scope that can be used to set expectations of what could/must be in <see cref="SelfLog"/>
            /// </summary>
            public class TestScope : IDisposable
            {
                private UnsafeList<FixedString4096Bytes> m_ExpectedMessages;

                /// <summary>
                /// True if created
                /// </summary>
                public bool IsCreated => m_ExpectedMessages.IsCreated;

                /// <summary>
                /// Constructor of the test scope. SelfLog must be enabled
                /// </summary>
                /// <param name="allocator">Allocator for internal expected message list</param>
                public TestScope(Allocator allocator)
                {
                    UnityEngine.Assertions.Assert.IsTrue(SelfLog.IsEnabled, "Please enable SelfLog at least to 'in memory' mode, otherwise this test will always fail");
                    m_ExpectedMessages = new UnsafeList<FixedString4096Bytes>(64, allocator);
                    Scopes.Add(this);
                }

                /// <summary>
                /// Expecting to have a message
                /// </summary>
                /// <param name="expected">Message that is expected in <see cref="SelfLog"/></param>
                [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
                public void ExpectErrorThatContains(FixedString4096Bytes expected)
                {
                    UnityEngine.Assertions.Assert.IsTrue(m_ExpectedMessages.IsCreated, "m_ExpectedMessages.IsCreated is false. Forgot to create TestScope?");
                    m_ExpectedMessages.Add(expected);
                }

                /// <summary>
                /// Out of memory message is expected
                /// </summary>
                [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
                public void ExpectingOutOfMemory()
                {
                    ExpectErrorThatContains("Failed to allocate memory");
                }

                /// <summary>
                /// Checks if this message is expected
                /// </summary>
                /// <param name="message">The message to check</param>
                [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
                public void CheckMessage(FixedString4096Bytes message)
                {
                    UnityEngine.Assertions.Assert.IsTrue(m_ExpectedMessages.IsCreated, "m_ExpectedMessages.IsCreated is false. Forgot to create TestScope?");

                    for (var i = 0; i < m_ExpectedMessages.Length; i++)
                    {
                        var expectedMessage = m_ExpectedMessages[i];

                        if (message.Contains(expectedMessage))
                        {
                            m_ExpectedMessages.RemoveAtSwapBack(i);

                            return;
                        }
                    }
                }

                /// <summary>
                /// End of the scope, checks all the expected messages. See <see cref="IDisposable"/>
                /// </summary>
                public void Dispose()
                {
                    UnityEngine.Assertions.Assert.IsTrue(m_ExpectedMessages.IsCreated, "m_ExpectedMessages.IsCreated is false. Forgot to create TestScope?");

                    var s = "";
                    for (var i = 0; i < m_ExpectedMessages.Length; i++)
                    {
                        s += $"'{m_ExpectedMessages[i]}' was expected, but wasn't logged into Unity.Logger.SelfLog";
                    }

                    UnityEngine.Assertions.Assert.IsTrue(string.IsNullOrEmpty(s), s);

                    m_ExpectedMessages.Dispose();

                    var sc = Scopes;

                    var p = sc[sc.Count - 1];

                    unsafe
                    {
                        UnityEngine.Assertions.Assert.IsTrue(m_ExpectedMessages.Ptr == p.m_ExpectedMessages.Ptr, "Disposing TestScope and it must be at the top of stack, but it is not");
                    }

                    sc.RemoveAt(sc.Count - 1);
                }
            }
        }
    }
}
