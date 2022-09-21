//#define LOGGING_MEM_DEBUG

using System;
using System.Diagnostics;
using UnityEngine.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Logging.Internal;

namespace Unity.Logging
{
    /// <summary>
    /// Disposable struct that adds Decorations to the logs in the scope, or to some particular log
    /// </summary>
    /// <remarks>
    /// Example:
    /// <code>
    ///    using LogDecorateScope decorConst1 = Log.To(log).Decorate('ConstantExampleLog1', 999999);
    /// </code>
    /// or
    /// <code>
    ///    using LogDecorateScope decorConstAll = Log.Decorate('ToAll', 42);
    /// </code>
    ///
    /// It will Dispose (unlock) m_Lock that is passed in and was created in .To(log2)
    /// The way it works internally is:
    ///
    /// <code>
    /// ref var dec = ref LogController.BeginEditDecoratePayloadHandles(ref ctx.Lock, out var nBefore);
    ///
    ///   -- Adds decoration to(ref dec); --
    ///
    /// var payloadHandles = LogController.EndEditDecoratePayloadHandles(ref logController, nBefore);
    ///
    /// return new LogDecorateScope(logController.Handle, payloadHandles);
    /// </code>
    /// </remarks>
    [BurstCompile]
    public readonly struct LogDecorateScope : IDisposable
    {
        private readonly LoggerHandle m_Handle;
        private readonly FixedList64Bytes<PayloadHandle> m_PayloadHandles;

        /// <summary>
        /// Creates the <see cref="LogDecorateScope"/>
        /// <seealso cref="LoggerManager.ReleaseDecoratePayloadBufferDeferred"/>
        /// </summary>
        /// <remarks>Warning: the lock is disposed because it can't be held for the whole scope.</remarks>
        /// <param name="lock"><see cref="LogControllerScopedLock"/> that is a default if global, or lock that holds some <see cref="LogController"/></param>
        /// <param name="payloadHandles">Payload handles that will be deferred-released when Dispose is called.</param>
        public LogDecorateScope(LogControllerScopedLock @lock, FixedList64Bytes<PayloadHandle> payloadHandles)
        {
            Assert.IsTrue(payloadHandles.Length > 0);
            m_Handle = @lock.Handle;
            @lock.Dispose(); // cannot hold lock, need to memorize handle and create another lock on Dispose of this struct
            m_PayloadHandles = payloadHandles;

            DebugMemLogDecorateScopeAlloc(m_Handle, payloadHandles);
        }

        /// <summary>
        /// Payload handles will be deferred-released in this call.
        /// </summary>
        public void Dispose()
        {
            DebugMemLogDecorateScopeFree(m_Handle, m_PayloadHandles);

            LoggerManager.ReleaseDecoratePayloadBufferDeferred(m_Handle, m_PayloadHandles);
        }

        [Conditional("LOGGING_MEM_DEBUG")]
        private static void DebugMemLogDecorateScopeAlloc(LoggerHandle handle, FixedList64Bytes<PayloadHandle> payloadHandles)
        {
            var message = new FixedString4096Bytes();
            message.Append((FixedString64Bytes)"LogDecorateScope Alloc: ");

            message.Append(handle.Value);
            message.Append((FixedString64Bytes)" with ");
            message.Append(payloadHandles.Length);
            message.Append((FixedString64Bytes)" handles:\n");

            for (var i = 0; i < payloadHandles.Length; i++)
            {
                message.Append((FixedString64Bytes)"   -");
                message.Append(payloadHandles[i].m_Value);
                message.Append('\n');
            }

            UnityEngine.Debug.Log(message);
        }

        [Conditional("LOGGING_MEM_DEBUG")]
        private static void DebugMemLogDecorateScopeFree(LoggerHandle handle, FixedList64Bytes<PayloadHandle> payloadHandles)
        {
            var message = new FixedString4096Bytes();
            message.Append((FixedString64Bytes)"LogDecorateScope Free: ");

            message.Append(handle.Value);
            message.Append((FixedString64Bytes)" with ");
            message.Append(payloadHandles.Length);
            message.Append((FixedString64Bytes)" handles:\n");

            for (var i = 0; i < payloadHandles.Length; i++)
            {
                message.Append((FixedString64Bytes)"   -");
                message.Append(payloadHandles[i].m_Value);
                message.Append('\n');
            }

            UnityEngine.Debug.Log(message);
        }
    }
}
