#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES // C# 9 support, unmanaged delegates - gc alloc free way to call
#endif

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;

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

        private SpinLockReadWrite SinksLock;
        private UnsafeList<SinkStruct> Sinks;

        /// <summary>
        /// True if any sink requested stack trace. If this is false - logging would work faster.
        /// </summary>
        public bool NeedsStackTrace => NeedsStackTraceByte != 0;

        internal ThreadSafeFuncList<LoggerManager.OutputWriterDecorateHandler> OutputWriterDecorateHandlers;

        // Constant decoration
        private ThreadSafeList4096<PayloadHandle> DecoratePayloadHandles;

        /// <summary>
        /// Current synchronization mode
        /// </summary>
        public SyncMode SyncMode;

        /// <summary>
        /// Function that is called by the logging codegeneration. <para />
        /// Used to populate Decorate array. See <see cref="LogDecorateScope"/> documentation for more details <para />
        /// This function is called before decoration, <see cref="EndEditDecoratePayloadHandles"/> must be called after
        /// <seealso cref="LogDecorateScope"/>
        /// </summary>
        /// <param name="lock"><see cref="LogControllerScopedLock"/> that controls LogController</param>
        /// <param name="nBefore">Current count of Decorations - PayloadHandles at the moment</param>
        /// <returns><see cref="LogContextWithDecorator"/> that controls array of Decorations - PayloadHandles</returns>
        public static LogContextWithDecorator BeginEditDecoratePayloadHandles(in LogControllerScopedLock @lock, out int nBefore)
        {
            ref var lc = ref @lock.GetLogController();

            nBefore = lc.DecoratePayloadHandles.BeginWrite();
            ref var list = ref ThreadSafeList4096<PayloadHandle>.GetReferenceNotThreadSafe(ref lc.DecoratePayloadHandles);

            unsafe
            {
                fixed (FixedList4096Bytes<PayloadHandle>* ptr = &list)
                {
                    return new LogContextWithDecorator(ptr, @lock);
                }
            }
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
            SyncMode = SyncMode.FatalIsSync;

            Sinks = new UnsafeList<SinkStruct>(8, Allocator.Persistent);
            SinksLock = new SpinLockReadWrite(Allocator.Persistent);

            OutputWriterDecorateHandlers = new ThreadSafeFuncList<LoggerManager.OutputWriterDecorateHandler>();
            DecoratePayloadHandles = new ThreadSafeList4096<PayloadHandle>();

            MemoryManager.Initialize(memoryParameters);
#if !UNITY_DOTSRUNTIME && !NET_DOTS
            ManagedOperations.Initialize();
#endif
        }

        /// <summary>
        /// Returns true if the LogLevel is supported by at least one <see cref="SinkSystemBase"/> in this <see cref="LogController"/>
        /// </summary>
        /// <param name="level">LogLevel enum</param>
        /// <returns>Returns true if the LogLevel is supported by at least one <see cref="SinkSystemBase"/> in this <see cref="LogController"/></returns>
        public bool HasSinksFor(LogLevel level) => HasSink.Has(level);

        /// <summary>
        /// True if Logging system has been initialized.
        /// </summary>
        public bool IsCreated => MemoryManager.IsInitialized;

        /// <summary>
        /// Checks that IsCreated == true. Throws otherwise.
        /// </summary>
        /// <exception cref="Exception">Throws if IsCreated == false</exception>
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
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
            if (SinksLock.IsCreated)
            {
                using (var _ = new SpinLockReadWrite.ScopedExclusiveLock(SinksLock))
                {
                    if (Sinks.IsCreated)
                    {
                        foreach (var sinkStruct in Sinks)
                            sinkStruct.Dispose();
                        Sinks.Dispose();
                    }
                }
                SinksLock.Dispose();
            }

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
        /// <param name="payload">PayloadHandle of the binary data associated with the message</param>
        /// <param name="stacktraceId">Id of the stacktrace connected to the LogMessage, 0 is none</param>
        /// <param name="logLevel">LogLevel of the LogMessage</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DispatchMessage(PayloadHandle payload, long stacktraceId, LogLevel logLevel)
        {
            DispatchQueue.Enqueue(payload, stacktraceId, logLevel);

            if (SyncMode == SyncMode.FullSync || (SyncMode == SyncMode.FatalIsSync && logLevel == LogLevel.Fatal))
                 FlushSync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void DispatchMessage(PayloadHandle payload, long timestamp, long stacktraceId, LogLevel logLevel)
        {
            DispatchQueue.Enqueue(payload, timestamp, stacktraceId, logLevel);

            if (SyncMode == SyncMode.FullSync || (SyncMode == SyncMode.FatalIsSync && logLevel == LogLevel.Fatal))
                 FlushSync();
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
        /// Burst-friendly way to immediately and synchronously Update/Flush the DispatchQueue into sinks. This is a slower alternative to LoggerManager.ScheduleUpdate but can be called from Burst / not main thread.
        /// </summary>
        [BurstCompile]
        public void FlushSync()
        {
            Handle.MustBeValid();
            LogControllerWrapper.MustBeReadLocked(Handle);

            var allocator = JobsUtility.IsExecutingJob ? Allocator.Temp : Allocator.TempJob;  // Allocator.Temp can be used in case of main thread / jobs. But this code can be in a managed thread

            var messageBuffer = new UnsafeText(1024, allocator);

            try
            {
                var sinks = BeginUsingSinks();
                for (var si = 0; si < sinks; si++)
                {
                    ref var logger = ref GetSinkLogger(si);
                    if (logger.OnBeforeSink.IsCreated)
                        logger.OnBeforeSink.Invoke(logger.UserData);
                }

                try
                {
                    DispatchQueue.LockAndSortForSyncAccess(out var olderMessages, out var newerMessages);
                    ProcessLogElements(ref olderMessages, ref messageBuffer, sinks, allocator);
                    ProcessLogElements(ref newerMessages, ref messageBuffer, sinks, allocator);
                }
                finally
                {
                    DispatchQueue.EndLockAfterSyncAccess();
                }

                for (var si = 0; si < sinks; si++)
                {
                    ref var logger = ref GetSinkLogger(si);
                    if (logger.OnAfterSink.IsCreated)
                        logger.OnAfterSink.Invoke(logger.UserData);
                }
            }
            finally
            {
                EndUsingSinks();
            }

            messageBuffer.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int BeginUsingSinks()
        {
            SinksLock.LockRead();

            return Sinks.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EndUsingSinks()
        {
            SinksLock.UnlockRead();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessLogElements(ref UnsafeList<LogMessage> list, ref UnsafeText messageBuffer, int sinks, Allocator allocator)
        {
            unsafe
            {
                fixed (LogMemoryManager* memManagerPtr = &MemoryManager)
                {
                    var memManager = new IntPtr(memManagerPtr);

                    var n = list.Length;
                    for (var position = 0; position < n; ++position)
                    {
                        ref var elem = ref list.ElementAt(position);

                        for (var si = 0; si < sinks; si++)
                        {
                            ref var sink = ref GetSinkLogger(si);

                            if (sink.IsInterestedIn(ref elem))
                            {
                                sink.Process(ref elem, ref sink.Formatter, ref messageBuffer, memManager, allocator);
                            }
                        }

                        Logger.CleanupUpdateJob.ReleaseLogMessage(ref MemoryManager, ref elem);
                    }
                }
            }
        }

        /// <summary>
        /// Burst-friendly way to represent a sink
        /// </summary>
        public struct SinkStruct : IDisposable
        {
            /// <summary>
            /// Output template that this sink should use
            /// </summary>
            public FixedString512Bytes OutputTemplate;
            /// <summary>
            /// Minimal level that this sink is interested in
            /// </summary>
            public LogLevel MinimalLevel;
            /// <summary>
            /// If non-zero - it captures stacktraces
            /// </summary>
            public int CaptureStackTracesBytes;
            /// <summary>
            /// Last timestamp that this sink processed. Sink will ignore all timestamps less/equal that this
            /// </summary>
            public long LastTimestamp;

            /// <summary>
            /// User data
            /// </summary>
            public IntPtr UserData;

            /// <summary>
            /// Formatter that is used with this sink
            /// </summary>
            public FormatterStruct Formatter;

            /// <summary>
            /// Delegate called before the sink
            /// </summary>
            public OnBeforeSinkDelegate OnBeforeSink;

            /// <summary>
            /// Delegate called on message emit for this sink
            /// </summary>
            public OnLogMessageEmitDelegate OnLogMessageEmit;

            /// <summary>
            /// Delegate called after the sink
            /// </summary>
            public OnAfterSinkDelegate OnAfterSink;

            /// <summary>
            /// Delegate called on dispose of this sink
            /// </summary>
            public OnDisposeDelegate OnDispose;

            /// <summary>
            /// True if sink needs stacktraces
            /// </summary>
            public bool CaptureStackTraces => CaptureStackTracesBytes != 0;

            /// <summary>
            /// True if the sink was created
            /// </summary>
            public bool IsCreated => OnLogMessageEmit.IsCreated;

            /// <summary>
            /// User's data attached
            /// </summary>
            /// <returns>User data</returns>
            public IntPtr GetUserData() => UserData;

            /// <summary> Checks if the <see cref="LogMessage"/>'s Timestamp and Level fits (see 'remarks') the sink. Updates sink's 'LastTimestamp' if method returns true. </summary>
            /// <remarks>To return true '<see cref="LogMessage"/>'s Timestamp should be less than sink's LastTimestamp (means newer than last processed message. And its Level should be >= than sink's MinimalLevel.</remarks>>
            /// <param name="elem">Log message to check</param>
            /// <returns>True if this <see cref="LogMessage"/> is going to be processed by the sink</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsInterestedIn(ref LogMessage elem)
            {
                if (LastTimestamp >= elem.Timestamp)
                    return false;
                // Timestamp is tracked to make sure we never process the same logging message in this sink twice
                // This actually can happen in case of Fatal is sync, everything else is async
                Interlocked.Exchange(ref LastTimestamp, elem.Timestamp);
                return elem.Level >= MinimalLevel;
            }

            /// <summary>
            /// Dispose the sink
            /// </summary>
            public void Dispose()
            {
                if (OnDispose.IsCreated)
                    OnDispose.Invoke(UserData);
                UserData = IntPtr.Zero;
            }

            /// <summary>
            /// Processes the LogMessage.
            /// </summary>
            /// <param name="elem">Log message to process</param>
            /// <param name="formatter">Formatter</param>
            /// <param name="messageBuffer">Message buffer that is used as a temporary storage</param>
            /// <param name="memManager">Memory manager where LogMessage's data is stored</param>
            /// <param name="allocator">Allocator to use in case of temporary allocation needed during the processing</param>
            public void Process(ref LogMessage elem, ref FormatterStruct formatter, ref UnsafeText messageBuffer, IntPtr memManager, Allocator allocator)
            {
                if (Formatter.OnFormatMessage.IsCreated)
                {
                    var length = Formatter.OnFormatMessage.Invoke(in elem, ref formatter, ref OutputTemplate, ref messageBuffer, memManager, UserData, allocator);
                    if (length > 0)
                    {
                        OnLogMessageEmit.Invoke(in elem, ref OutputTemplate, ref messageBuffer, memManager, UserData, allocator);
                    }
                }
                else
                {
                    OnLogMessageEmit.Invoke(in elem, ref OutputTemplate, ref messageBuffer, memManager, UserData, allocator);
                }
            }
        }

        internal ref SinkStruct GetSinkLogger(int si)
        {
            return ref Sinks.ElementAt(si);
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
        internal ushort ExecuteDecorateHandlers(ref LogContextWithDecorator handles)
        {
            Handle.MustBeValid();

            var lengthBefore = handles.Length;

            try
            {
                unsafe
                {
                    var n = OutputWriterDecorateHandlers.BeginRead();
                    for (var i = 0; i < n; i++)
                    {
                        ref var func = ref OutputWriterDecorateHandlers.ElementAt(i);
#if LOGGING_USE_UNMANAGED_DELEGATES
                        ((delegate * unmanaged[Cdecl] <ref LogContextWithDecorator, void>)func.Value)(ref handles);
#else
                        func.Invoke(ref handles);
#endif
                    }
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

        /// <summary>
        /// Creates new sink using <see cref="SinkSystemBase"/>'s ToSinkStruct method
        /// </summary>
        /// <param name="sink">SinkSystemBase object</param>
        public void AddSinkStruct(SinkSystemBase sink)
        {
            try
            {
                SinksLock.Lock();
                var s = sink.ToSinkStruct();

                if (s.IsCreated)
                {
                    sink.SinkId = Sinks.Length;
                    Assert.IsTrue(s.OnLogMessageEmit.IsCreated, "OnLogMessageEmit must be assigned");
                    Sinks.Add(s);
                }
                else
                {
                    sink.SinkId = -1;
                }
            }
            finally
            {
                SinksLock.Unlock();
            }
        }

        /// <summary>
        /// Changes the minimal level of the sink
        /// </summary>
        /// <param name="sinkId">Id that was returned by <see cref="AddSinkStruct"/></param>
        /// <param name="minimalLevel">New minimal log level for the sink</param>
        public void SetMinimalLogLevelForSink(int sinkId, LogLevel minimalLevel)
        {
            if (sinkId < 0) return;

            try
            {
                BeginUsingSinks();
                Sinks.ElementAt(sinkId).MinimalLevel = minimalLevel;
            }
            finally
            {
                EndUsingSinks();
            }
        }
    }
}
