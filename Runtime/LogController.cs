using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging.Internal;

namespace Unity.Logging
{
    /// <summary>
    /// Burst-friendly backend for <see cref="Logger"/> that contains <see cref="LoggerHandle"/>, <see cref="LogMemoryManager"/>, <see cref="DispatchQueue"/>
    /// and is able to answer if this particular <see cref="LogLevel"/> is supported by this Logger.
    /// </summary>
    public struct LogController
    {
        /// <summary>
        /// Unique id of the <see cref="Logger"/>
        /// </summary>
        public LoggerHandle Handle;

        /// <summary>
        /// Memory manager that stores binary representation of structured logging. <seealso cref="LogMemoryManager"/>
        /// </summary>
        public LogMemoryManager MemoryManager;

        /// <summary>
        /// Double-buffered queue that is used for dispatching <see cref="LogMessage"/>s. <seealso cref="DispatchQueue"/>
        /// </summary>
        public DispatchQueue DispatchQueue;

        /// <summary>
        /// Burst-friendly structure that can answer question like 'is this LogLevel supported?'
        /// </summary>
        internal HasSinkStruct HasSink;

        internal byte NeedsStackTraceByte;

        /// <summary>
        /// True if any sink requested stack trace. If this is false - logging would work faster.
        /// </summary>
        public bool NeedsStackTrace => NeedsStackTraceByte != 0;

        internal ThreadSafeFuncList<LoggerManager.OutputWriterDecorateHandler> OutputWriterDecorateHandlers;

        // Constant decoration
        private ThreadSafeList4096<PayloadHandle> DecoratePayloadHandles;

        /// <summary>
        /// Function that is called by the logging codegeneration. <para />
        /// Used to populate Decorate array. See <see cref="LogDecorateScope"/> documentation for more details <para />
        /// This function is called before decoration, <see cref="EndEditDecoratePayloadHandles"/> must be called after
        /// <seealso cref="LogDecorateScope"/>
        /// </summary>
        /// <param name="lc"><see cref="LogController"/> to add decorations to</param>
        /// <param name="nBefore">Current count of Decorations - PayloadHandles at the moment</param>
        /// <returns>Array of Decorations - PayloadHandles</returns>
        public static ref FixedList4096Bytes<PayloadHandle> BeginEditDecoratePayloadHandles(ref LogController lc, out int nBefore)
        {
            nBefore = lc.DecoratePayloadHandles.BeginWrite();
            return ref ThreadSafeList4096<PayloadHandle>.GetReferenceNotThreadSafe(ref lc.DecoratePayloadHandles);
        }

        /// <summary>
        /// Function that is called by the logging codegeneration. <para />
        /// Used to populate Decorate array. See <see cref="LogDecorateScope"/> documentation for more details <para />
        /// This function is called after decoration, <see cref="BeginEditDecoratePayloadHandles"/> must be called before
        /// <seealso cref="LogDecorateScope"/>
        /// </summary>
        /// <param name="lc"><see cref="LogController"/> to add decorations to</param>
        /// <param name="nBefore">Same variable that was returned in <see cref="BeginEditDecoratePayloadHandles"/> call</param>
        /// <returns>Array of just added Decorations - PayloadHandles</returns>
        public static FixedList64Bytes<PayloadHandle> EndEditDecoratePayloadHandles(ref LogController lc, int nBefore)
        {
            var nAfter = lc.DecoratePayloadHandles.Length;

            var payloadHandles = new FixedList64Bytes<PayloadHandle>();
            for (var i = nBefore; i < nAfter; ++i)
                payloadHandles.Add(lc.DecoratePayloadHandles.ElementAt(i));

            lc.DecoratePayloadHandles.EndWrite();

            return payloadHandles;
        }

        internal void RemoveDecoratePayloadHandles(FixedList64Bytes<PayloadHandle> payloadHandles)
        {
            DecoratePayloadHandles.Remove(payloadHandles);
        }

        /// <summary>
        /// Constructor that initializes <see cref="MemoryManager"/> and <see cref="DispatchQueue"/>. Uses <see cref="LoggerHandle"/> provided by the caller.
        /// </summary>
        /// <param name="loggerHandle">Unique id of the parent <see cref="Logger"/></param>
        /// <param name="memoryParameters">Parameters used to initialize <see cref="MemoryManager"/>. Could be used to specify particular size of its buffers.</param>
        public LogController(LoggerHandle loggerHandle, in LogMemoryManagerParameters memoryParameters)
        {
            Handle = loggerHandle;
            MemoryManager = new LogMemoryManager();
            DispatchQueue = new DispatchQueue(memoryParameters.DispatchQueueSize);
            HasSink = default;
            NeedsStackTraceByte = 0;

            OutputWriterDecorateHandlers = new ThreadSafeFuncList<LoggerManager.OutputWriterDecorateHandler>();
            DecoratePayloadHandles = new ThreadSafeList4096<PayloadHandle>();

            MemoryManager.Initialize(memoryParameters);
#if !UNITY_DOTSRUNTIME && !NET_DOTS
            ManagedOperations.Initialize();
#endif
        }

        /// <summary>
        /// Returns true if <see cref="level"/> is supported by at least one <see cref="SinkSystemBase{TLoggerImpl}"/> in this <see cref="LogController"/>
        /// </summary>
        /// <param name="level">LogLevel enum</param>
        /// <returns>Returns true if <see cref="level"/> is supported by at least one <see cref="SinkSystemBase{TLoggerImpl}"/> in this <see cref="LogController"/></returns>
        public bool HasSinksFor(LogLevel level) => HasSink.Has(level);

        /// <summary>
        /// True if Logging system has been initialized.
        /// </summary>
        public bool IsCreated => MemoryManager.IsInitialized;

        /// <summary>
        /// Checks that IsCreated == true. Throws otherwise.
        /// </summary>
        /// <exception cref="Exception">Throws if IsCreated == false</exception>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void MustBeValid()
        {
            if (IsCreated == false)
                throw new Exception("LogController must be valid, but it is not!");
        }

        /// <summary>
        /// Stops Logging and releases the memory and destroys systems created by LogController.
        /// </summary>
        /// <remarks>
        /// This must be called from the main thread (do not call from a Job) after all Logging calls have completed; any
        /// log messages "in flight" will be discarded.
        ///
        /// IMPORTANT: Accessing the Payload buffer of a log message when Shutdown is called, e.g. during an asynchronous I/O operation,
        /// will result in undefined behavior.
        /// </remarks>
        public void Shutdown()
        {
            if (DispatchQueue.IsCreated)
                DispatchQueue.Dispose();
            if (MemoryManager.IsInitialized)
                MemoryManager.Shutdown();
        }

        /// <summary>
        /// Dispatches a <see cref="LogMessage"/>
        /// </summary>
        /// <remarks>
        /// This method is thread-safe and can be called from any thread context or Job.
        /// If successful, the Logging system will take over ownership of the message data and ensure the memory buffer is released
        /// after the message has been processed.
        /// </remarks>
        /// <param name="message">Message structure</param>
        public void DispatchMessage(ref LogMessage message)
        {
            DispatchQueue.Enqueue(ref message);
        }

        /// <summary>
        /// Count of log messages dispatched and waiting to be processed
        /// </summary>
        /// <returns>Count of log messages dispatched and waiting to be processed</returns>
        public int LogDispatched()
        {
            if (IsCreated == false)
                return 0;
            return DispatchQueue.TotalLength;
        }

        /// <summary>
        /// The function that can add function based decorator.
        /// Internally used by <see cref="LogDecorateHandlerScope"/>, better to use that instead.
        /// <seealso cref="RemoveDecorateHandler"/>
        /// </summary>
        /// <param name="func">Function based decorator to add</param>
        public void AddDecorateHandler(FunctionPointer<LoggerManager.OutputWriterDecorateHandler> func)
        {
            OutputWriterDecorateHandlers.Add(func);
        }

        /// <summary>
        /// The function that remove function based decorator that was added by <see cref="AddDecorateHandler"/>
        /// Internally used by <see cref="LogDecorateHandlerScope"/>, better to use that instead.
        /// <seealso cref="AddDecorateHandler"/>
        /// </summary>
        /// <param name="func">Function based decorator to remove</param>
        public void RemoveDecorateHandler(FunctionPointer<LoggerManager.OutputWriterDecorateHandler> func)
        {
            OutputWriterDecorateHandlers.Remove(func.Value);
        }

        /// <summary>
        /// Returns count of constant Decorators (that you can add with Log.Decorate("name", value))
        /// They will be copied to new log message payload each time log message is created
        /// </summary>
        /// <returns>Count of constant Decorators</returns>
        public int DecoratePayloadsCount()
        {
            return DecoratePayloadHandles.Length;
        }

        /// <summary>
        /// Returns count of Function-based Decorators (that you can add with Log.Decorate(function) or <see cref="AddDecorateHandler"/> and remove with <see cref="RemoveDecorateHandler"/>)
        /// They will be executed each time log message is created
        /// </summary>
        /// <returns>Count of Function-based Decorators</returns>
        public int DecorateHandlerCount()
        {
            return OutputWriterDecorateHandlers.Length;
        }

        /// <summary>
        /// Executes all function based decorator handlers of this <see cref="LogController"/> that were added with with Log.To(logger).Decorate(function) or <see cref="AddDecorateHandler"/> and remove with <see cref="RemoveDecorateHandler"/>
        /// </summary>
        /// <param name="handles">Where to add decorations</param>
        /// <returns>Count of added decorations</returns>
        internal ushort ExecuteDecorateHandlers(LogContextWithDecorator handles)
        {
            Handle.MustBeValid();

            var lengthBefore = handles.Length;

            var n = OutputWriterDecorateHandlers.BeginRead();
            try
            {
                for (var i = 0; i < n; i++)
                {
                    OutputWriterDecorateHandlers.ElementAt(i).Invoke(handles);
                }
            }
            finally
            {
                OutputWriterDecorateHandlers.EndRead();
            }

            return (ushort)(handles.Length - lengthBefore);
        }

        /// <summary>
        /// Adds all const decorator handlers of this <see cref="LogController"/> that were added with with Log.To(logger).Decorate("name", value)
        /// </summary>
        /// <param name="handles">Where to add decorations</param>
        /// <returns>Count of added decorations</returns>
        internal ushort AddConstDecorateHandlers(LogContextWithDecorator handles)
        {
            Handle.MustBeValid();

            try
            {
                var n = DecoratePayloadHandles.BeginRead();
                for (var i = 0; i < n; i++)
                {
                    var handleCopy = MemoryManager.Copy(DecoratePayloadHandles.ElementAt(i));
                    handles.Add(handleCopy);
                }

                return (ushort)n;
            }
            finally
            {
                DecoratePayloadHandles.EndRead();
            }
        }

        internal void DisposeAllDecorators()
        {
            Handle.MustBeValid();

            try
            {
                var n = DecoratePayloadHandles.BeginWrite();
                for (var i = 0; i < n; i++)
                    MemoryManager.ReleasePayloadBuffer(DecoratePayloadHandles.ElementAt(i), out _, true);
                ThreadSafeList4096<PayloadHandle>.GetReferenceNotThreadSafe(ref DecoratePayloadHandles).Clear();
            }
            finally
            {
                DecoratePayloadHandles.EndWrite();
            }
        }
    }
}
