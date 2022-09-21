using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging.Internal;

namespace Unity.Logging.Sinks
{
    /// <summary>
    /// General SinkJob structure that calls OnLogMessage on every LogMessage for this sink
    /// </summary>
    [BurstCompile]
    internal struct SinkJob : IJob
    {
        [ReadOnly]
        public bool unblittableWorkaroundForMarshalBug;

        /// <summary>
        /// <see cref="LogControllerScopedLock"/> to access LogController
        /// </summary>
        public LogControllerScopedLock Lock;

        /// <summary>
        /// Sink id in the LogController
        /// </summary>
        public int SinkId;

        public void Execute()
        {
            ref var logController = ref Lock.GetLogController();
            ref var sink = ref logController.GetSinkLogger(SinkId);

            const Allocator allocator = Allocator.Temp; // temp because we're in the job
            var messageBuffer = new UnsafeText(1024, allocator);

            unsafe
            {
                fixed (LogMemoryManager* memManagerPtr = &logController.MemoryManager)
                {
                    var memManager = new IntPtr(memManagerPtr);

                    if (sink.OnBeforeSink.IsCreated)
                        sink.OnBeforeSink.Invoke(sink.UserData);

                    try
                    {
                        var reader = logController.DispatchQueue.BeginRead();

                        var n = reader.Length;
                        for (var position = 0; position < n; ++position)
                        {
                            var elem = UnsafeUtility.ReadArrayElement<LogMessage>(reader.Ptr, position);

                            if (sink.IsInterestedIn(ref elem))
                            {
                                sink.Process(ref elem, ref sink.Formatter, ref messageBuffer, memManager, allocator);
                            }
                        }
                    }
                    finally
                    {
                        logController.DispatchQueue.EndRead();
                    }
                }
            }

            messageBuffer.Dispose();

            if (sink.OnAfterSink.IsCreated)
                sink.OnAfterSink.Invoke(sink.UserData);
        }
    }
}
