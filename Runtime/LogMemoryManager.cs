//#define LOGGING_MEM_DEBUG

using PayloadLockHashMap = Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMap<Unity.Logging.PayloadHandle, Unity.Logging.PayloadBufferLockData>;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging
{
    /// <summary>
    /// Initialization parameters for <see cref="LogMemoryManager"/>.
    /// </summary>
    [BurstCompile]
    [GenerateTestsForBurstCompatibility]
    public struct LogMemoryManagerParameters
    {
        /// <summary>
        /// Starting size of the default Payload container.
        /// </summary>
        public uint InitialBufferCapacity;

        /// <summary>
        /// Default value for <see cref="InitialBufferCapacity"/>
        /// </summary>
        public const uint DefaultBufferCapacity = 1024 * 64;

        /// <summary>
        /// Number of samples used for computing moving average of default Payload container capacity.
        /// </summary>
        /// <remarks>
        /// Setting this to 0 will disable all dynamic resizing operations.
        /// </remarks>
        public uint BufferSampleCount;

        /// <summary>
        /// Default value for <see cref="BufferSampleCount"/>
        /// </summary>
        public const uint DefaultBufferSampleCount = 60;

        /// <summary>
        /// Default Payload container capacity will increase in size when the average usage goes over this ratio.
        /// </summary>
        /// <remarks>
        /// Setting to 0 will disable dynamic "growing" operations; Payload containers may
        /// shrink in size but won't automatically grow.
        /// </remarks>
        public float BufferGrowThreshold;

        /// <summary>
        /// Default value for <see cref="BufferGrowThreshold"/>
        /// </summary>
        public const float DefaultBufferGrowThreshold = 0.70f;

        /// <summary>
        /// Default Payload container capacity will decrease in size when the average usage goes below this ratio.
        /// </summary>
        /// <remarks>
        /// Setting to 0 will disable dynamic "shrinking" operations; Payload containers may
        /// grow in size but won't automatically shrink.
        /// </remarks>
        public float BufferShrinkThreshold;

        /// <summary>
        /// Default value for <see cref="BufferShrinkThreshold"/>
        /// </summary>
        public const float DefaultBufferShrinkThreshold = 0.0f;

        /// <summary>
        /// Factor by which the default Payload container increases in size, when growth threshold is reached.
        /// </summary>
        public float BufferGrowFactor;

        /// <summary>
        /// Default value for <see cref="BufferGrowFactor"/>
        /// </summary>
        public const float DefaultBufferGrowFactor = 2.0f;

        /// <summary>
        /// Factor by which the default Payload container decreases in size, when shrink threshold is reached.
        /// </summary>
        public float BufferShrinkFactor;

        /// <summary>
        /// Default value for <see cref="BufferShrinkFactor"/>
        /// </summary>
        public const float DefaultBufferShrinkFactor = 0.85f;

        /// <summary>
        /// Size of the "overflow" buffer, which is another <see cref="UnsafePayloadRingBuffer"/> instance, to hold allocations if the default buffer becomes full.
        /// </summary>
        /// <remarks>
        /// The overflow buffer is never resized and always holds this allocated memory throughout MemoryManager's lifetime,
        /// regardless if it's used or not. The purpose is to safeguard against the default buffer filling up before it can
        /// be resized. Set this value to 0 to disable use of the overflow buffer.
        /// </remarks>
        public uint OverflowBufferSize;

        /// <summary>
        /// Default value for <see cref="OverflowBufferSize"/>
        /// </summary>
        public const uint DefaultOverflowBufferSize = 1024 * 10 * 16;

        /// <summary>
        /// Max count of Log messages between updates. <see cref="DispatchQueue"/>
        /// </summary>
        public int DispatchQueueSize;

        /// <summary>
        /// Default value for <see cref="DispatchQueueSize"/>
        /// </summary>
        public const int DefaultDispatchQueueSize = 1024 * 16;

        /// <summary>
        /// Returns if automatic Payload container resizing is enabled at all.
        /// </summary>
        public bool IsAutomaticBufferResizeEnabled => BufferSampleCount != 0;

        /// <summary>
        /// Returns if automatic Payload container growth is specifically enabled.
        /// </summary>
        public bool IsAutomaticBufferGrowthEnabled => IsAutomaticBufferResizeEnabled && BufferGrowThreshold > 0;

        /// <summary>
        /// Returns if automatic Payload container shrink is specifically enabled.
        /// </summary>
        public bool IsAutomaticBufferShrinkEnabled => IsAutomaticBufferResizeEnabled && BufferShrinkThreshold > 0;

        /// <summary>
        /// True if Overflow Buffer is used (its size > 0). <seealso cref="OverflowBufferSize"/>
        /// </summary>
        public bool IsOverflowBufferEnabled => OverflowBufferSize > 0;

        /// <summary>
        /// Get default settings
        /// </summary>
        public static LogMemoryManagerParameters Default
        {
            get
            {
                GetDefaultParameters(out var result);
                return result;
            }
        }

        /// <summary>
        /// Creates a <see cref="LogMemoryManagerParameters"/> instance holding default values.
        /// </summary>
        /// <param name="defaultParams">Returned instance</param>
        public static void GetDefaultParameters(out LogMemoryManagerParameters defaultParams)
        {
            defaultParams = new LogMemoryManagerParameters
            {
                InitialBufferCapacity = DefaultBufferCapacity,
                BufferSampleCount = DefaultBufferSampleCount,
                BufferGrowThreshold = DefaultBufferGrowThreshold,
                BufferShrinkThreshold = DefaultBufferShrinkThreshold,
                BufferGrowFactor = DefaultBufferGrowFactor,
                BufferShrinkFactor = DefaultBufferShrinkFactor,
                OverflowBufferSize = DefaultOverflowBufferSize,
                DispatchQueueSize = DefaultDispatchQueueSize
            };
        }

        /// <summary>
        /// Get heavy load settings
        /// </summary>
        public static LogMemoryManagerParameters HeavyLoad
        {
            get
            {
                GetHeavyLoadParameters(out var result);
                return result;
            }
        }

        /// <summary>
        /// Creates a <see cref="LogMemoryManagerParameters"/> instance holding heavy-load-case values.
        /// </summary>
        /// <param name="defaultParams">Returned instance</param>
        public static void GetHeavyLoadParameters(out LogMemoryManagerParameters defaultParams)
        {
            defaultParams = new LogMemoryManagerParameters
            {
                InitialBufferCapacity = DefaultBufferCapacity * 64,
                BufferSampleCount = DefaultBufferSampleCount,
                BufferGrowThreshold = DefaultBufferGrowThreshold,
                BufferShrinkThreshold = DefaultBufferShrinkThreshold,
                BufferGrowFactor = DefaultBufferGrowFactor,
                BufferShrinkFactor = DefaultBufferShrinkFactor,
                OverflowBufferSize = DefaultOverflowBufferSize * 32,
                DispatchQueueSize = DefaultDispatchQueueSize * 32
            };
        }
    }

    /// <summary>
    /// Result value returned when releasing Payload buffers.
    /// </summary>
    /// <remarks>
    /// Returned by <see cref="LogMemoryManager.ReleasePayloadBuffer(PayloadHandle, out PayloadReleaseResult, bool)"/>.
    /// </remarks>
    public enum PayloadReleaseResult
    {
        /// <summary>
        /// Payload buffer was successfully released.
        /// </summary>
        Success,

        /// <summary>
        /// Payload buffer is forced to release even if it's locked or a Disjointed payload
        /// railed to release.
        /// </summary>
        ForcedRelease,

        /// <summary>
        /// Release failed because the PayloadBuffer has been locked.
        /// </summary>
        BufferLocked,

        /// <summary>
        /// Release failed because the passed in PayloadHandle doesn't reference a valid Payload buffer.
        /// </summary>
        /// <remarks>
        /// Typically this means the Payload buffer has already been released.
        /// </remarks>
        InvalidHandle,

        /// <summary>
        /// The <see cref="LogMemoryManager"/> instance isn't initialized.
        /// </summary>
        /// <remarks>
        /// This implies the <see cref="PayloadHandle"/> is also invalid because the memory it may have referenced
        /// has been released.
        /// </remarks>
        NotInitialized,

        /// <summary>
        /// One or more of the Payloads referenced by a Disjointed buffer failed to release.
        /// </summary>
        /// <remarks>
        /// In this case all valid Payloads are released but the Disjointed "head" Payload is not
        /// released and the Disjointed buffer handle will remain active. To actually free the Disjointed
        /// buffer, <see cref="LogMemoryManager.ReleasePayloadBuffer(PayloadHandle, out PayloadReleaseResult, bool)"/> must
        /// be called with "force" release parameter set.
        /// </remarks>
        DisjointedPayloadReleaseFailed,
    }

    /// <summary>
    /// Interface for allocating and managing Payload buffers for <see cref="LogMessage"/>.
    /// </summary>
    /// <remarks>
    /// A Payload buffer is allocated when creating a new log message, the actual message data, i.e. the "Payload", is serialized
    /// into this buffer by the log producer. A <see cref="PayloadHandle"/> references the allocated memory and is later used by
    /// a Listener to access the buffer and de-serialize the message data. After the message has been processed by a Listener,
    /// the Payload buffer must be released, typically performed automatically by <see cref="LogController"/>.
    /// </remarks>
    [BurstCompile]
    [GenerateTestsForBurstCompatibility]
    public struct LogMemoryManager
    {
        /// <summary>
        /// Minimum value for <see cref="LogMemoryManagerParameters.BufferGrowThreshold"/>
        /// </summary>
        public const float MinimumRingBufferGrowThreshold = 0.0f;

        /// <summary>
        /// Maximum value for <see cref="LogMemoryManagerParameters.BufferGrowThreshold"/>
        /// </summary>
        public const float MaximumRingBufferGrowThreshold = 1.0f;


        /// <summary>
        /// Minimum value for <see cref="LogMemoryManagerParameters.BufferShrinkThreshold"/>
        /// </summary>
        public const float MinimumRingBufferShrinkThreshold = 0.0f;

        /// <summary>
        /// Maximum value for <see cref="LogMemoryManagerParameters.BufferShrinkThreshold"/>
        /// </summary>
        public const float MaximumRingBufferShrinkThreshold = 1.0f;


        /// <summary>
        /// Minimum value for <see cref="LogMemoryManagerParameters.BufferGrowFactor"/>
        /// </summary>
        public const float MinimumRingBufferGrowFactor = 1.0f;

        /// <summary>
        /// Maximum value for <see cref="LogMemoryManagerParameters.BufferGrowFactor"/>
        /// </summary>
        public const float MaximumRingBufferGrowFactor = 1000.0f;

        /// <summary>
        /// Minimum value for <see cref="LogMemoryManagerParameters.BufferShrinkFactor"/>
        /// </summary>
        public const float MinimumRingBufferShrinkFactor = 0.01f;

        /// <summary>
        /// Maximum value for <see cref="LogMemoryManagerParameters.BufferShrinkFactor"/>
        /// </summary>
        public const float MaximumRingBufferShrinkFactor = 1.0f;

        /// <summary>
        /// Maximum value for <see cref="LogMemoryManagerParameters.BufferSampleCount"/>
        /// </summary>
        public const uint MaximumRingBufferSampleCount = 10000;

        internal const byte DefaultBufferAId = 1;
        internal const byte DefaultBufferBId = 2;
        internal const byte OverflowBufferId = 10;
        internal const int InitialBufferLockMapCapacity = 40;

        /// <summary>
        /// Parameter values provided to <see cref="Initialize(LogMemoryManagerParameters)"/>.
        /// </summary>
        public LogMemoryManagerParameters Parameters => m_BufferParams;

        /// <summary>
        /// Returns if this <see cref="LogMemoryManager"/> instance is currently initialized.
        /// </summary>
        public bool IsInitialized => m_Initialized;

        /// <summary>
        /// Initializes MemoryManager using default parameters.
        /// </summary>
        /// <remarks>
        /// See <see cref="LogMemoryManager.Initialize(LogMemoryManagerParameters)"/>.
        /// </remarks>
        public void Initialize()
        {
            if (IsInitialized)
                return;

            LogMemoryManagerParameters.GetDefaultParameters(out var defaultParams);

            Initialize(defaultParams);
        }

        /// <summary>
        /// Initializes MemoryManager with the specified set of parameters.
        /// </summary>
        /// <remarks>
        /// Parameter values must fall within the specified minimum/maximum ranges, and invalid
        /// parameters are replaced with their corresponding default value.
        /// </remarks>
        /// <param name="parameters"><see cref="LogMemoryManagerParameters"/> structure that contains specified parameters</param>
        public void Initialize(LogMemoryManagerParameters parameters)
        {
            if (IsInitialized)
                return;

            // NOTE: Must be called from the main thread; not thread safe

            // Sanity check parameters and replace invalid values with defaults
            if (parameters.InitialBufferCapacity < UnsafePayloadRingBuffer.MinimumCapacity || parameters.InitialBufferCapacity > UnsafePayloadRingBuffer.MaximumCapacity)
            {
                parameters.InitialBufferCapacity = LogMemoryManagerParameters.DefaultBufferCapacity;
            }

            // Validate grow/shrink thresholds values and reset to defaults if either are invalid
            if (parameters.BufferGrowThreshold < MinimumRingBufferGrowThreshold || parameters.BufferGrowThreshold > MaximumRingBufferGrowThreshold)
            {
                parameters.BufferGrowThreshold = LogMemoryManagerParameters.DefaultBufferGrowThreshold;
            }
            if (parameters.BufferShrinkThreshold < MinimumRingBufferShrinkThreshold || parameters.BufferShrinkThreshold > MaximumRingBufferShrinkThreshold)
            {
                parameters.BufferShrinkThreshold = LogMemoryManagerParameters.DefaultBufferShrinkThreshold;
            }

            if (parameters.BufferGrowFactor < MinimumRingBufferGrowFactor || parameters.BufferGrowFactor > MaximumRingBufferGrowFactor)
            {
                parameters.BufferGrowFactor = LogMemoryManagerParameters.DefaultBufferGrowFactor;
            }

            if (parameters.BufferShrinkFactor < MinimumRingBufferShrinkFactor || parameters.BufferShrinkFactor > MaximumRingBufferShrinkFactor)
            {
                parameters.BufferShrinkFactor = LogMemoryManagerParameters.DefaultBufferShrinkFactor;
            }

            if (parameters.BufferSampleCount > MaximumRingBufferSampleCount)
            {
                parameters.BufferSampleCount = LogMemoryManagerParameters.DefaultBufferSampleCount;
            }

            // If Overflow buffer is enabled, ensure the specified size is within RingBuffer's allowed range
            if (parameters.OverflowBufferSize != 0)
            {
                if (parameters.OverflowBufferSize < UnsafePayloadRingBuffer.MinimumCapacity || parameters.OverflowBufferSize > UnsafePayloadRingBuffer.MaximumCapacity)
                {
                    parameters.OverflowBufferSize = LogMemoryManagerParameters.DefaultOverflowBufferSize;
                }
            }

            // If grow and shrink thresholds are both 0, this effectively disables automatic resizing
            // Set SampleCount parameter to 0 so we don't allocate MovingAverage data
            if (parameters.BufferGrowThreshold == 0 && parameters.BufferShrinkThreshold == 0)
            {
                parameters.BufferSampleCount = 0;
            }

            // Initialize the default 'A' buffer only, buffer 'B' is only used if/when we need to resize
            m_DefaultBufferA = new UnsafePayloadRingBuffer(parameters.InitialBufferCapacity, DefaultBufferAId, Allocator.Persistent);
            m_DefaultBufferB = new UnsafePayloadRingBuffer();
            m_UseBufferA = true;

            m_PayloadHandlesToReleaseX = new UnsafeList<PayloadHandle>(128, Allocator.Persistent);
            m_PayloadHandlesToReleaseY = new UnsafeList<PayloadHandle>(128, Allocator.Persistent);

            if (parameters.IsOverflowBufferEnabled)
            {
                m_OverflowBuffer = new UnsafePayloadRingBuffer(parameters.OverflowBufferSize, OverflowBufferId, Allocator.Persistent);
            }

            // Initialize for tracking automatic buffer resizing (via a moving average) unless
            // feature has been disabled
            if (parameters.BufferSampleCount != 0)
            {
                m_MovingAverage = new SimpleMovingAverage(parameters.BufferSampleCount);
            }

            m_UpdateLock = new SpinLockReadWrite(Allocator.Persistent);

            // Hashmap Capacity is the number of individual Payload buffers that must be locked simultaneously
            m_LockedPayloads = new PayloadLockHashMap(InitialBufferLockMapCapacity, Allocator.Persistent);
            m_PayloadLockSync = new SpinLockReadWrite(Allocator.Persistent);
            m_PayloadReleaseDeferredListLockSync = new SpinLockExclusive(Allocator.Persistent);

#if LOGGING_MEM_DEBUG
            m_AllocatedPayloads = new UnsafeParallelHashSet<PayloadHandle>(512, Allocator.Persistent);
#endif

            m_BufferParams = parameters;
            m_Initialized = true;
        }

        /// <summary>
        /// Releases all allocated memory and returns MemoryManager to an uninitialized state.
        /// </summary>
        /// <remarks>
        /// Do not call this directly; shutdown is performed through <see cref="LogController.Shutdown"/>.
        /// </remarks>
        public void Shutdown()
        {
            // NOTE: Must be called from the main thread; not thread safe

            if (m_LockedPayloads.IsCreated)
                m_LockedPayloads.Dispose();

            m_MovingAverage.Dispose();
            m_DefaultBufferA.Dispose();
            m_DefaultBufferB.Dispose();
            m_OverflowBuffer.Dispose();

            m_PayloadHandlesToReleaseX.Dispose();
            m_PayloadHandlesToReleaseY.Dispose();

            m_PayloadLockSync.Dispose();
            m_PayloadReleaseDeferredListLockSync.Dispose();

#if LOGGING_MEM_DEBUG
            if (m_AllocatedPayloads.IsCreated)
                m_AllocatedPayloads.Dispose();
#endif

            m_UpdateLock.Dispose();

            // Since memory manager can be initialized/shutdown multiple times (depending on
            // logging activity) it's important to completely reset all the fields.
            m_BufferParams = new LogMemoryManagerParameters();
            m_UseBufferA = false;
            m_Resizing = false;
            m_Initialized = false;
        }

        /// <summary>
        /// Gathers the internal statistics of this <see cref="LogMemoryManager"/>
        /// </summary>
        /// <param name="name">Optional name of the log memory manager</param>
        /// <returns>FixedString4096Bytes that contains debug internal state of this manager</returns>
        public FixedString4096Bytes DebugStateString(FixedString128Bytes name = default)
        {
            var result = new FixedString4096Bytes();

            if (IsInitialized)
            {
                result.Append((FixedString32Bytes)"[LogMemoryAllocator] ");
                result.Append(name);
                result.Append((FixedString32Bytes)" state: \n");

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                result.Append((FixedString32Bytes)"  Update Count: ");
                result.Append(m_UpdateCounter);
                result.Append('\n');
#endif

#if LOGGING_MEM_DEBUG
                result.Append((FixedString32Bytes)"  Allocated Handles: ");
                result.Append(m_AllocatedPayloads.Count());
                result.Append('\n');
                foreach (var h in m_AllocatedPayloads)
                {
                    result.Append((FixedString32Bytes)"  --->");
                    result.Append(h.m_Value);
                    result.Append('\n');
                }
                result.Append('\n');
#endif

                result.Append((FixedString32Bytes)"  BufferA: ");
                RingBufferState(m_DefaultBufferA);
                if (m_UseBufferA)
                    result.Append((FixedString32Bytes)" [Active]");
                result.Append('\n');

                result.Append((FixedString32Bytes)"  BufferB: ");
                RingBufferState(m_DefaultBufferB);
                if (m_UseBufferA == false)
                    result.Append((FixedString32Bytes)" [Active]");
                result.Append('\n');

                result.Append((FixedString32Bytes)"  Overflow: ");
                if (IsOverflowEnabled)
                    RingBufferState(m_OverflowBuffer);
                else
                    result.Append((FixedString32Bytes)" Disabled");
                result.Append('\n');

                result.Append((FixedString64Bytes)"  Locked buffers (with LockPayloadBuffer): ");
                if (m_LockedPayloads.IsCreated)
                    result.Append(m_LockedPayloads.Count());
                else
                    result.Append((FixedString32Bytes)"Not created");
                result.Append('\n');

                result.Append((FixedString64Bytes)"  Deferred release PayloadHandlesX: ");
                PayloadHandlesToReleaseState(m_PayloadHandlesToReleaseX);
                if (m_UseBufferX)
                    result.Append((FixedString32Bytes)" [Active]");
                result.Append('\n');

                result.Append((FixedString64Bytes)"  Deferred release PayloadHandlesY: ");
                PayloadHandlesToReleaseState(m_PayloadHandlesToReleaseY);
                if (m_UseBufferX == false)
                    result.Append((FixedString32Bytes)" [Active]");
                result.Append('\n');
            }
            else
            {
                result.Append((FixedString64Bytes)"[LogMemoryAllocator] Is not initialized!");
            }

            void PayloadHandlesToReleaseState(UnsafeList<PayloadHandle> reList)
            {
                if (reList.IsCreated == false)
                {
                    result.Append((FixedString32Bytes)"Not created");
                }
                else
                {
                    result.Append(reList.Length);
                }
            }

            void RingBufferState(UnsafePayloadRingBuffer rb)
            {
                if (rb.IsCreated == false)
                {
                    result.Append((FixedString32Bytes)"Not created");
                }
                else
                {
                    result.Append(rb.BytesAllocated);
                    result.Append((FixedString32Bytes)" / ");
                    result.Append(rb.Capacity);
                }
            }

            return result;
        }

        /// <summary>
        /// Lock to make sure no <see cref="Update"/> is called during it.
        /// Enters the read lock, means a lot of threads can enter. <see cref="Update"/> will enter the exclusive lock (so if no read locks / exclusive locks are present)
        /// <seealso cref="UnlockRead"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LockRead()
        {
            m_UpdateLock.LockRead();
        }

        /// <summary>
        /// Unlock <see cref="LockRead"/>
        /// <seealso cref="LockRead"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UnlockRead()
        {
            m_UpdateLock.UnlockRead();
        }

        /// <summary>
        /// Performs maintenance work on the allocated RingBuffers and should be called once per frame.
        /// <para>Enters exclusive lock, so make sure it is not called during <see cref="LockRead"/> on the same thread. <seealso cref="LockRead"/> <seealso cref="UnlockRead"/></para>
        /// </summary>
        /// <remarks>
        /// Do not call this directly; updating is performed automatically by the <see cref="LogController"/>.
        /// </remarks>
        public void Update()
        {
            if (!IsInitialized)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Interlocked.Increment(ref m_UpdateCounter);
#endif

            using var exclusiveUpdateLock = new SpinLockReadWrite.ScopedExclusiveLock(m_UpdateLock);

            // NOTE: can only be called from one thread at a time, not thread-safe
            UpdatePayloadBufferDeferred();

            if (!m_DefaultBufferA.IsRingBufferEmpty())
            {
                m_DefaultBufferA.ReclaimReleasedPayloadBlocks();
            }

            if (!m_DefaultBufferB.IsRingBufferEmpty())
            {
                m_DefaultBufferB.ReclaimReleasedPayloadBlocks();
            }

            // NOTE: If the Overflow buffer is utilized, we'll automatically increase the size of the default buffer (if resize is enable).
            // However, to prevent us from continuously increasing buffer's size (because Overflow allocations aren't released right away),
            // we'll save the current Overflow utilization and only trigger another resize if Overflow increases again.

            bool overflowTriggered = false;
            if (!m_OverflowBuffer.IsRingBufferEmpty())
            {
                if (m_OverflowBuffer.BytesAllocated > m_OverflowResizeFence)
                {
                    m_OverflowResizeFence = m_OverflowBuffer.BytesAllocated;
                    overflowTriggered = true;
                }

                m_OverflowBuffer.ReclaimReleasedPayloadBlocks();
            }
            else
            {
                // Reset our fence value once Overflow buffer is empty
                m_OverflowResizeFence = 0;
            }

            // If automatic resizing is disabled then skip the rest of Update
            if (!m_MovingAverage.IsCreated)
                return;

            // We don't count an empty buffer against average utilization, i.e. want to
            // track utilization of actual log Payloads and don't care about frequency of log calls
            var currUtilization = GetCurrentDefaultBufferUsage();
            if (currUtilization > 0)
            {
                m_MovingAverage.AddSample(currUtilization);
            }

            // Check average buffer utilization and start buffer resize if thresholds are reached
            // else update an existing resize process
            if (!m_Resizing)
            {
                m_Resizing = CheckBufferThresholdsAndResizeIfNeeded(overflowTriggered);
            }
            else
            {
                m_Resizing = UpdateResizing();
            }
        }

        /// <summary>
        /// Allocates a new Payload buffer from the default Payload container.
        /// </summary>
        /// <param name="payloadSize">Number of bytes to allocate; must fall within the range of <see cref="UnsafePayloadRingBuffer.MinimumPayloadSize"/> and <see cref="UnsafePayloadRingBuffer.MaximumPayloadSize"/>.</param>
        /// <returns>A valid <see cref="PayloadHandle"/> if successful.</returns>
        public PayloadHandle AllocatePayloadBuffer(uint payloadSize)
        {
            return AllocatePayloadBufferInternal(payloadSize, out _, false);
        }

        /// <summary>
        /// Allocates a new Payload buffer from the default Payload container.
        /// </summary>
        /// <remarks>
        /// If successful a PayloadHandle referencing the allocated memory is returned, and a
        /// NativeArray with read/write access (as a view into the buffer) is passed out.
        /// If allocation fails an invalid handle and buffer are returned.
        ///
        /// The PayloadHandle must be saved, as it's needed to retrieve the payload buffer again and
        /// also to release it. However, the passed out NativeArray is only intended for immediate
        /// reading/writing into the buffer; the variable must not be saved.
        ///
        /// IMPORTANT: The Payload buffer must eventually be released by calling <see cref="LogMemoryManager.ReleasePayloadBuffer(PayloadHandle, out PayloadReleaseResult, bool)"/>
        /// Failure to do so will cause a "leak" in the Payload container.
        ///
        /// NOTE: Do not call Dispose on the returned NativeArray; it's only a view into the Payload buffer.
        /// </remarks>
        /// <param name="payloadSize">Number of bytes to allocate; must fall within the range of <see cref="UnsafePayloadRingBuffer.MinimumPayloadSize"/> and <see cref="UnsafePayloadRingBuffer.MaximumPayloadSize"/>.</param>
        /// <param name="buffer">NativeArray that allows safe access to the allocated Payload buffer</param>
        /// <returns>A valid <see cref="PayloadHandle"/> if successful.</returns>
        public PayloadHandle AllocatePayloadBuffer(uint payloadSize, out NativeArray<byte> buffer)
        {
            return AllocatePayloadBufferInternal(payloadSize, out buffer, false);
        }

        /// <summary>
        /// Allocates a new Disjointed buffer, which includes allocating the individual Payloads that make up the entire buffer.
        /// </summary>
        /// <remarks>
        /// A 'Disjointed' buffer is a set of individual Payload buffers that are collectively treated as a single buffer; it's similar to a Jagged Array or can
        /// be thought of as an "array of arrays". Internally, the Disjointed buffer is implemented by a "head" buffer that holds <see cref="PayloadHandle"/>
        /// values referencing one or more Payloads, which hold the actual data. A single handle to the head buffer, referencing all the individual payloads, which
        /// can be passed within a single <see cref="LogMessage"/> component. As the name Disjointed suggests, the memory for the Payloads themselves is not contiguous
        /// (similar to a Jagged Array) and therefore each Payload buffer must be retrieved and accessed separately.
        ///
        /// Disjointed buffers can either be allocated up front via this Method or created from existing Payloads via <see cref="CreateDisjointedPayloadBufferFromExistingPayloads(ref FixedList512Bytes{PayloadHandle})"/>.
        /// Regardless, as with regular allocations, the buffer must be released by calling <see cref="ReleasePayloadBuffer(PayloadHandle, out PayloadReleaseResult, bool)"/>
        /// on the <see cref="PayloadHandle"/> returned by this method. Failure to do so will result in a "leak" of payload allocations; not just for the head buffer
        /// but all allocations that make up the DisjointedBuffer.
        ///
        /// When using this method, the individual Payload buffers are automatically allocated using the passed in size values. All Payload allocations sizes (including
        /// the head buffer) must fall within the <see cref="UnsafePayloadRingBuffer.MinimumPayloadSize"/> and <see cref="UnsafePayloadRingBuffer.MaximumPayloadSize"/>
        /// range. This means the total size of a Disjointed buffer is limited to <see cref="UnsafePayloadRingBuffer.MaximumDisjointedPayloadCount"/>, which is the number
        /// of <see cref="PayloadHandle"/> values that can fit into a single payload allocation. Payload allocations do not have to come from the same memory source, e.g.
        /// they can be allocated from the Default or Overflow RingBuffers. Payload data can be retrieved by calling <see cref="RetrieveDisjointedPayloadBuffer(PayloadHandle, int, bool, out NativeArray{byte})"/>
        /// on the Disjointed handle or by directly calling <see cref="RetrievePayloadBuffer(PayloadHandle, bool, out NativeArray{byte})"/> on the Payload handles stored
        /// in the head buffer.
        ///
        /// IMPORTANT: If any of the allocations (including the head buffer) fail, the entire operation is aborted and any allocated memory is released. However that memory
        /// won't be available for new allocations until after it's been reclaimed by a call to <see cref="Update"/>. This means, an attempt to allocate too much memory may
        /// result in a significant waste of available space causing other allocation requests to fail.
        ///
        /// Disjointed buffers are intended for the following scenarios:
        /// - Payload data exceeds the maximum Payload size and must be broken up into multiple pieces
        /// - The entire Payload data size isn't known up front and must be allocated in different stages
        /// - Additional data needs to be "appended" to an existing payload but without needing to completely reallocate a new buffer
        ///
        /// In general, Disjointed buffers should be treated as a single allocation and the individual Payloads that make up the buffer should only be used
        /// for immediate reading/writing data. It's recommended to follow these guidelines:
        /// - Do not store or pass the individual Payload handles; only the head handle (returned by this method) should be stored
        /// - Do not release the individual payload allocations; all Disjointed memory is released through the head allocation
        /// - Do not call <see cref="LockPayloadBuffer(PayloadHandle)"/> on individual payload handle; only the head buffer should be locked
        /// - Do not use a given Payload allocation in multiple Disjointed buffers
        /// - Do not use a Disjointed head handle within another Disjointed buffer (unsupported scenario)
        ///
        /// NOTE: These rules are not generally not checked nor enforced, and any validation that is performed only occurs when ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG is enabled.
        /// </remarks>
        /// <param name="payloadSizes">Set of buffer sizes to allocate for each Payload that comprises the DisjointedBuffer.</param>
        /// <param name="payloadHandles">Optional list that receives the <see cref="PayloadHandle"/> values for each Payload allocated by this method.</param>
        /// <returns>If successful, a valid <see cref="PayloadHandle"/> to the DisjointedBuffer's head.</returns>
        public PayloadHandle AllocateDisjointedBuffer(ref FixedList64Bytes<ushort> payloadSizes, NativeList<PayloadHandle> payloadHandles = new NativeList<PayloadHandle>())
        {
            return AllocateDisjointedPayloadBufferInternal(ref payloadSizes, ref payloadHandles);
        }

        /// <summary>
        /// Allocates a new Disjointed buffer, which includes allocating the individual Payloads that make up the entire buffer.
        /// </summary>
        /// <param name="payloadSizes">Set of buffer sizes to allocate for each Payload that comprises the DisjointedBuffer.</param>
        /// <param name="payloadHandles">Optional list that receives the <see cref="PayloadHandle"/> values for each Payload allocated by this method.</param>
        /// <returns>If successful, a valid <see cref="PayloadHandle"/> to the DisjointedBuffer's head.</returns>
        public PayloadHandle AllocateDisjointedBuffer(ref FixedList512Bytes<ushort> payloadSizes, NativeList<PayloadHandle> payloadHandles = new NativeList<PayloadHandle>())
        {
            return AllocateDisjointedPayloadBufferInternal(ref payloadSizes, ref payloadHandles);
        }

        /// <summary>
        /// Allocates a new Disjointed buffer, which includes allocating the individual Payloads that make up the entire buffer.
        /// </summary>
        /// <param name="payloadSizes">Set of buffer sizes to allocate for each Payload that comprises the DisjointedBuffer.</param>
        /// <param name="payloadHandles">Optional list that receives the <see cref="PayloadHandle"/> values for each Payload allocated by this method.</param>
        /// <returns>If successful, a valid <see cref="PayloadHandle"/> to the DisjointedBuffer's head.</returns>
        public PayloadHandle AllocateDisjointedBuffer(ref NativeList<ushort> payloadSizes, NativeList<PayloadHandle> payloadHandles = new NativeList<PayloadHandle>())
        {
            return AllocateDisjointedPayloadBufferInternal(ref payloadSizes, ref payloadHandles);
        }

        /// <summary>
        /// Creates a new Disjointed buffer that's composed of preallocated Payloads, instead of allocating new ones.
        /// </summary>
        /// <remarks>
        /// See <see cref="AllocateDisjointedBuffer(ref Unity.Collections.FixedList64Bytes{ushort},Unity.Collections.NativeList{Unity.Logging.PayloadHandle})"/> for a general
        /// overview on "Disjointed" payload buffers.
        ///
        /// Use this method to group a set of Payload buffers that have already been allocated into a single Disjointed buffer.
        /// In this case, only the "head" payload needs to be allocated, which is then filled with the specified <see cref="PayloadHandle"/>
        /// value. A handle to the new head buffer is returned just as with <see cref="AllocateDisjointedBuffer(ref Unity.Collections.FixedList64Bytes{ushort},Unity.Collections.NativeList{Unity.Logging.PayloadHandle})"/>.
        ///
        /// Disjointed buffers created this way should be treated exactly the same as those allocated up front; the individual Payloads
        /// are now part of the whole buffer and should only be used for immediate reading/writing of data. Likewise, calling
        /// <see cref="ReleasePayloadBuffer(PayloadHandle, out PayloadReleaseResult, bool)"/> on the returned handle will now
        /// automatically release all the individual Payload buffers as well.
        ///
        /// NOTE: The <see cref="PayloadHandle"/> values passed into this method must reference valid Payload buffers and cannot
        /// be themselves handles to Disjointed buffers. However, the handles are only validated when ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
        /// are enabled.
        /// </remarks>
        /// <param name="payloadHandles">Set a <see cref="PayloadHandle"/> values referencing Payload buffers that'll compose the new Disjointed buffer.</param>
        /// <returns>If successful, a valid <see cref="PayloadHandle"/> to the DisjointedBuffer's head.</returns>
        public PayloadHandle CreateDisjointedPayloadBufferFromExistingPayloads(ref FixedList512Bytes<PayloadHandle> payloadHandles)
        {
            return CreateDisjointedPayloadBufferFromExistingPayloadsInternal(ref payloadHandles);
        }

        /// <summary>
        /// Creates a new Disjointed buffer that's composed of preallocated Payloads, instead of allocating new ones.
        /// </summary>
        /// <remarks>
        /// See <see cref="LogMemoryManager.CreateDisjointedPayloadBufferFromExistingPayloads(ref FixedList512Bytes{PayloadHandle})"/>.
        /// </remarks>
        /// <param name="payloadHandles">Set a <see cref="PayloadHandle"/> values referencing Payload buffers that'll compose the new Disjointed buffer.</param>
        /// <returns>If successful, a valid <see cref="PayloadHandle"/> to the DisjointedBuffer's head.</returns>
        public PayloadHandle CreateDisjointedPayloadBufferFromExistingPayloads(ref FixedList4096Bytes<PayloadHandle> payloadHandles)
        {
            return CreateDisjointedPayloadBufferFromExistingPayloadsInternal(ref payloadHandles);
        }

        /// <summary>
        /// Creates a new Disjointed buffer that's composed of preallocated Payloads, instead of allocating new ones.
        /// </summary>
        /// <remarks>
        /// See <see cref="LogMemoryManager.CreateDisjointedPayloadBufferFromExistingPayloads(ref FixedList512Bytes{PayloadHandle})"/>.
        /// </remarks>
        /// <param name="payloadHandles">Set a <see cref="PayloadHandle"/> values referencing Payload buffers that'll compose the new Disjointed buffer.</param>
        /// <returns>If successful, a valid <see cref="PayloadHandle"/> to the DisjointedBuffer's head.</returns>
        public PayloadHandle CreateDisjointedPayloadBufferFromExistingPayloads(ref NativeList<PayloadHandle> payloadHandles)
        {
            return CreateDisjointedPayloadBufferFromExistingPayloadsInternal(ref payloadHandles);
        }

        /// <summary>
        /// Releases the Payload memory allocated within the default Payload container.
        /// </summary>
        /// <remarks>
        /// This must be called when the Payload data is no longer needed, otherwise
        /// payload blocks will "leak" within the container. Once complete the handle
        /// becomes invalid and attempts to retrieve the buffer again will fail.
        ///
        /// If the Payload buffer has been "locked", this operation will fail until all
        /// locks have been released. However, this behavior can be overridden using
        /// the "force" option; if set the buffer will be released irregardless of the
        /// number of locks on it.
        ///
        /// If the handle is for a Disjointed buffer, then all individual Payload buffers will also be released. When
        /// working with DisjointedBuffers, this is the recommended way to handle individual payloads. In general,
        /// it's not recommended to manually release payloads referenced by a DisjointedBuffer.
        ///
        /// Should a given Payload referenced by the DisjointedBuffer handle fail to release (for any reason) the call
        /// will fail with the result: <see cref="PayloadReleaseResult.DisjointedPayloadReleaseFailed"/>. In this case
        /// all the other valid payloads referenced by DisjointedBuffer are released, but the "head" payload is not.
        /// To actually release the Disjointed buffer, this method must be called again with the force parameter set true;
        /// the returned result will be <see cref="PayloadReleaseResult.ForcedRelease"/>.
        /// Note: if "force" is set when releasing a DisjointedBuffer the buffer, it will always succeed even if one or
        /// more referenced payloads fail to release.
        ///
        /// In general, it isn't necessary to call this directly because <see cref="LogController"/> will automatically
        /// cleanup message data and release Payload buffers.
        /// </remarks>
        /// <param name="handle">The <see cref="PayloadHandle"/> referencing the Payload buffer to release.</param>
        /// <param name="result">A detailed result code from <see cref="PayloadReleaseResult"/>.</param>
        /// <param name="force">Forces release of the Payload buffer even if it can't be performed "cleanly".</param>
        /// <returns>
        /// True if the payload buffer specifically referenced by the handle is released and false if not.
        /// For DisjointedBuffers, true is only returned if the "head" buffer is actually released. Valid Payloads
        /// referenced by a DisjointedBuffer are always released regardless of the return value.
        /// </returns>
        public bool ReleasePayloadBuffer(PayloadHandle handle, out PayloadReleaseResult result, bool force = false)
        {
            // NOTE: SpinLock can deadlock if Lock is called on uninitialized data.
            if (!IsInitialized)
            {
                result = PayloadReleaseResult.NotInitialized;
                return false;
            }

            bool success = false;

            using (var exclusiveLock = new SpinLockReadWrite.ScopedExclusiveLock(m_PayloadLockSync))
            {
                bool bufferLocked = m_LockedPayloads.ContainsKey(handle);

                if (!bufferLocked || force)
                {
                    // If Disjointed buffer, first attempt to release Payloads referenced by it
                    PayloadReleaseResult disjointResult = PayloadReleaseResult.Success;
                    bool disjointSuccess = true;
                    if (handle.IsDisjointedBuffer)
                    {
                        disjointSuccess = ReleaseDisjointedBufferPayloads(handle, out disjointResult);
                    }

                    // Alright a little confusing logic here: proceed to release the main payload ONLY if the following are true:
                    // - Disjointed payloads released successfully or this case doesn't apply (not Disjointed buffer)
                    // - Disjointed payloads didn't release successfully but we're overriding with 'force' parameter
                    // - EXCEPT: Disjointed handle is invalid; don't continue because we already know the handle is bad
                    if (disjointSuccess || (force && disjointResult != PayloadReleaseResult.InvalidHandle))
                    {
                        success = ReleasePayloadBufferInternal(handle);
                        if (success && (bufferLocked || !disjointSuccess))
                        {
                            // Payload was successfully release, but not in a "clean" way (had to override)
                            // - Buffer was locked but still released it
                            // - Disjointed buffer failed to release one or more of it's payloads
                            result = PayloadReleaseResult.ForcedRelease;
                        }
                        else
                        {
                            result = success ? PayloadReleaseResult.Success : PayloadReleaseResult.InvalidHandle;
                        }
                    }
                    else
                    {
                        // Failed to release Disjointed buffer; use the result from ReleaseDisjointedBufferPayloads()
                        success = false;
                        result = disjointResult;
                    }
                }
                else
                {
                    // Payload buffer is locked and we're not forcing the release
                    success = false;
                    result = PayloadReleaseResult.BufferLocked;
                }
            }

            return success;
        }

        /// <summary>
        /// Releases the Payload memory allocated within the default Payload container after two (system is double buffered) <see cref="Update"/>> calls
        /// </summary>
        /// <remarks>
        /// This call adds payload handle to 'deferred' list, so it can be released after everything that uses this payload was processed.
        /// General use case is 'decoration' for logging messages. Some messages can be decorated with this payloads.
        /// Then Decorator is released, ReleasePayloadBufferDeferred is called and that guarantees that this decoration won't be attached to any log from this point.
        /// And then after two <see cref="Update"/>> calls when all users of this Payload were processed - we should safely release it.
        /// For more details see <see cref="ReleasePayloadBuffer"/>
        /// </remarks>
        /// <param name="handle">The <see cref="PayloadHandle"/> referencing the Payload buffer to release.</param>
        public void ReleasePayloadBufferDeferred(PayloadHandle handle)
        {
            // NOTE: SpinLock can deadlock if Lock is called on uninitialized data.
            if (!IsInitialized)
            {
                return;
            }

            DebugMemPayloadBufferFreeDeferred(handle);

            using (var exclusiveLock = new SpinLockExclusive.ScopedLock(m_PayloadReleaseDeferredListLockSync))
            {
                if (m_UseBufferX)
                    m_PayloadHandlesToReleaseX.Add(handle);
                else
                    m_PayloadHandlesToReleaseY.Add(handle);
            }
        }

        /// <summary>
        /// Update deferred mechanism under <see cref="ReleasePayloadBuffer"/>
        /// </summary>
        private void UpdatePayloadBufferDeferred()
        {
            using (var exclusiveLock = new SpinLockExclusive.ScopedLock(m_PayloadReleaseDeferredListLockSync))
            {
                if (m_UseBufferX)
                {
                    var n = m_PayloadHandlesToReleaseY.Length;
                    for (var i = 0; i < n; i++)
                    {
                        ReleasePayloadBuffer(m_PayloadHandlesToReleaseY[i], out var result);
                        CheckReleaseWasSuccessful(result);
                    }
                    m_PayloadHandlesToReleaseY.Clear();
                }
                else
                {
                    var n = m_PayloadHandlesToReleaseX.Length;
                    for (var i = 0; i < n; i++)
                    {
                        ReleasePayloadBuffer(m_PayloadHandlesToReleaseX[i], out var result);
                        CheckReleaseWasSuccessful(result);
                    }
                    m_PayloadHandlesToReleaseX.Clear();
                }

                // Flip X-Y buffer (happens each Update())
                m_UseBufferX = !m_UseBufferX;
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
        private static void CheckReleaseWasSuccessful(PayloadReleaseResult result)
        {
            if (result != PayloadReleaseResult.Success)
                throw new Exception("Deferred ReleasePayloadBuffer failed!");
        }

        /// <summary>
        /// Retrieves a NativeArray to safely access Payload memory.
        /// </summary>
        /// <remarks>
        /// See <see cref="RetrievePayloadBuffer(PayloadHandle, bool, out NativeArray{byte})"/>
        /// </remarks>
        /// <param name="handle">A valid <see cref="PayloadHandle"/> value for the Payload buffer to access.</param>
        /// <param name="payloadBuffer">NativeArray that allows safe access to the allocated Payload buffer.</param>
        /// <returns>True if successfully accessed Payload buffer.</returns>
        public bool RetrievePayloadBuffer(PayloadHandle handle, out NativeArray<byte> payloadBuffer)
        {
            return RetrievePayloadBuffer(handle, false, out payloadBuffer);
        }

        /// <summary>
        /// Retrieves a NativeArray to safely access Payload memory.
        /// </summary>
        /// <remarks>
        /// Listeners must call this to read the log message data when processing a <see cref="LogMessage"/>.
        /// The passed out NativeArray is only intended for immediate reading/writing into the buffer; the
        /// variable must not be saved.
        ///
        /// By default the return NativeArray is read-only, since typically Listeners only need to de-serialize the
        /// buffer contents and don't need to write into the buffer.
        /// </remarks>
        /// <param name="handle">A valid <see cref="PayloadHandle"/> value for the Payload buffer to access.</param>
        /// <param name="readWriteAccess">True to allow write-access to the returned NativeArray, otherwise it's read-only</param>
        /// <param name="payloadBuffer">NativeArray that allows safe access to the allocated Payload buffer.</param>
        /// <returns>True if successfully accessed Payload buffer.</returns>
        public bool RetrievePayloadBuffer(PayloadHandle handle, bool readWriteAccess, out NativeArray<byte> payloadBuffer)
        {
            bool success;
            var bufferId = PayloadHandle.ExtractBufferIdFromHandle(ref handle);

            switch (bufferId)
            {
                case DefaultBufferAId:
                    success = m_DefaultBufferA.RetrievePayloadFromHandle(handle, readWriteAccess, out payloadBuffer);
                    break;

                case DefaultBufferBId:
                    success = m_DefaultBufferB.RetrievePayloadFromHandle(handle, readWriteAccess, out payloadBuffer);
                    break;

                case OverflowBufferId:
                    success = m_OverflowBuffer.RetrievePayloadFromHandle(handle, readWriteAccess, out payloadBuffer);
                    break;

                default:
                    payloadBuffer = new NativeArray<byte>();
                    success = false;
                    break;
            }

            return success;
        }

        /// <summary>
        /// Retrieves a NativeArray to safely access an individual Payload that's part of a Disjointed buffer.
        /// </summary>
        /// <remarks>
        /// See <see cref="RetrieveDisjointedPayloadBuffer(PayloadHandle, int, bool, out NativeArray{byte})"/>.
        /// </remarks>
        /// <param name="handle">A <see cref="PayloadHandle"/> value the references a valid Disjointed buffer."</param>
        /// <param name="payloadBufferIndex">Index of the Payload, referenced by the Disjointed buffer, to retrieve.</param>
        /// <param name="payloadBuffer">NativeArray that allows safe access to the allocated Payload buffer.</param>
        /// <returns>True if successfully accessed Payload buffer.</returns>
        public bool RetrieveDisjointedPayloadBuffer(PayloadHandle handle, int payloadBufferIndex, out NativeArray<byte> payloadBuffer)
        {
            return RetrieveDisjointedPayloadBuffer(handle, payloadBufferIndex, false, out payloadBuffer);
        }

        /// <summary>
        /// Retrieves a NativeArray to safely access an individual Payload that's part of a Disjointed buffer.
        /// </summary>
        /// <remarks>
        /// See <see cref="AllocateDisjointedBuffer(ref Unity.Collections.FixedList64Bytes{ushort},Unity.Collections.NativeList{Unity.Logging.PayloadHandle})"/> for a general
        /// overview on "Disjointed" payload buffers.
        ///
        /// Use this method to safely retrieve one of the Payload buffers, that's referenced by a Disjointed buffer, for
        /// reading or writing payload data, similar to <see cref="RetrievePayloadBuffer(PayloadHandle, bool, out NativeArray{byte})"/>.
        ///
        /// The Payload buffer to retrieve is specified by an index of the <see cref="PayloadHandle"/> value within the head buffer.
        /// This index value corresponds to the list index used to create the Disjointed buffer. That is, the Payload size list passed into
        /// <see cref="AllocateDisjointedBuffer(ref Unity.Collections.FixedList64Bytes{ushort},Unity.Collections.NativeList{Unity.Logging.PayloadHandle})"/> or the <see cref="PayloadHandle"/>
        /// value list passed into <see cref="CreateDisjointedPayloadBufferFromExistingPayloads(ref FixedList512Bytes{PayloadHandle})"/>.
        ///
        /// As with <see cref="RetrievePayloadBuffer(PayloadHandle, bool, out NativeArray{byte})"/>, the returned NativeArray is a "view"
        /// into the actual memory and must not be stored; it's only for immediate access to the underlying memory.
        /// </remarks>
        /// <param name="handle">A <see cref="PayloadHandle"/> value the references a valid Disjointed buffer."</param>
        /// <param name="payloadBufferIndex">Index of the Payload, referenced by the Disjointed buffer, to retrieve.</param>
        /// <param name="readWriteAccess">True to allow write-access to the returned NativeArray, otherwise it's read-only.</param>
        /// <param name="payloadBuffer">NativeArray that allows safe access to the allocated Payload buffer.</param>
        /// <returns>True if successfully accessed Payload buffer.</returns>
        public bool RetrieveDisjointedPayloadBuffer(PayloadHandle handle, int payloadBufferIndex, bool readWriteAccess, out NativeArray<byte> payloadBuffer)
        {
            bool success = false;
            payloadBuffer = new NativeArray<byte>();

            if (handle.IsDisjointedBuffer && RetrievePayloadBuffer(handle, false, out var headBuffer))
            {
                int numHandles = headBuffer.Length / UnsafeUtility.SizeOf<PayloadHandle>();
                if (payloadBufferIndex < numHandles)
                {
                    PayloadHandle contentHandle;
                    unsafe
                    {
                        var handles = (PayloadHandle*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(headBuffer);
                        contentHandle = handles[payloadBufferIndex];
                    }
                    success = RetrievePayloadBuffer(contentHandle, readWriteAccess, out payloadBuffer);
                }
            }

            return success;
        }

        /// <summary>
        /// Debug function that returns information about <see cref="PayloadHandle"/>
        /// </summary>
        /// <param name="handle">PayloadHandle to analyze</param>
        /// <returns>FixedString that contains debug information</returns>
        public FixedString4096Bytes DebugDetailsOfPayloadHandle(ref PayloadHandle handle)
        {
            var bufferId = PayloadHandle.ExtractBufferIdFromHandle(ref handle);

            switch (bufferId)
            {
                case DefaultBufferAId:
                    return m_DefaultBufferA.DebugDetailsOfPayloadHandle(handle);

                case DefaultBufferBId:
                    return m_DefaultBufferB.DebugDetailsOfPayloadHandle(handle);

                case OverflowBufferId:
                    return m_OverflowBuffer.DebugDetailsOfPayloadHandle(handle);

                default:
                    var result = new FixedString4096Bytes();
                    result.Append((FixedString64Bytes)"[Invalid] data.BufferId is unknown: <");
                    result.Append(bufferId);
                    result.Append((FixedString32Bytes)">");
                    return result;
            }
        }

        /// <summary>
        /// Tests if <see cref="PayloadHandle"/> references a valid Payload buffer or not.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="PayloadHandle.IsValid"/>, which only tests if the handle value is valid,
        /// this verifies the handle currently references a valid Payload buffer.
        /// </remarks>
        /// <param name="handle">PayloadHandle value to test for validity.</param>
        /// <returns>True if handle references a valid Payload buffer and false if not.</returns>
        public bool IsPayloadHandleValid(PayloadHandle handle)
        {
            var bufferId = PayloadHandle.ExtractBufferIdFromHandle(ref handle);

            var success = bufferId switch
            {
                DefaultBufferAId => m_DefaultBufferA.IsPayloadHandleValid(handle),
                DefaultBufferBId => m_DefaultBufferB.IsPayloadHandleValid(handle),
                OverflowBufferId => m_OverflowBuffer.IsPayloadHandleValid(handle),
                _ => false
            };

            return success;
        }

        /// <summary>
        /// Returns the Capacity of the current Payload container from which Payloads are allocated from.
        /// </summary>
        /// <remarks>
        /// During a Resize operation 2 containers are used: an "active" one which holds the new size
        /// and the "old" one which holds the previous allocations that haven't yet been released.
        /// Only the Capacity from the "active" container is returned; the Overflow buffer is never included.
        /// </remarks>
        /// <returns>Capacity of the current Payload container from which Payloads are allocated from.</returns>
        public uint GetCurrentDefaultBufferCapacity()
        {
            return m_UseBufferA ? m_DefaultBufferA.Capacity : m_DefaultBufferB.Capacity;
        }

        /// <summary>
        /// Returns the Usage of the current Payload container from which Payloads are allocated from.
        /// </summary>
        /// <remarks>
        /// During a Resize operation 2 containers are used: an "active" one which holds the new size
        /// and the "old" one which holds the previous allocations that haven't yet been released.
        /// Only the Usage from the "active" container is returned; the Overflow buffer is never included.
        /// </remarks>
        /// <returns>Usage of the current Payload container from which Payloads are allocated from.</returns>
        public uint GetCurrentDefaultBufferUsage()
        {
            return m_UseBufferA ? m_DefaultBufferA.BytesAllocated : m_DefaultBufferB.BytesAllocated;
        }

        /// <summary>
        /// Adds a "Lock" on the specified Payload buffer, preventing it from being released while the lock is active.
        /// </summary>
        /// <remarks>
        /// Only active Payload buffers can be locked; this method will fail if the buffer has
        /// already been released. Multiple locks can be applied to the same buffer simultaneously,
        /// (distinguished by a "context" value) but is limited to a max of 64.
        ///
        /// Each successful "lock" operation generates a <see cref="PayloadLockContext"/> value,
        /// which must be saved and later used to unlock the Payload buffer.
        ///
        /// The purpose of Payload Locks is to coordinate multiple log Listeners sharing the same
        /// payload memory, so the buffer is only released once all Listeners are finished. Upon
        /// receiving a LogMessage, a Listener immediately calls this method to lock the buffer,
        /// then processes and outputs the log data, and finally releases the lock when finished.
        /// This ensures the buffer isn't released prematurely while a Listener is still accessing
        /// the memory.
        /// </remarks>
        /// <param name="handle">A valid <see cref="PayloadHandle"/> value for the Payload buffer to lock.</param>
        /// <returns>If successful, a valid <see cref="PayloadLockContext"/> is returned.</returns>
        /// <seealso cref="UnlockPayloadBuffer"/>
        /// <seealso cref="IsPayloadBufferLocked(PayloadHandle, out int)"/>
        /// <seealso cref="ReleasePayloadBuffer(PayloadHandle, out PayloadReleaseResult, bool)"/>
        public PayloadLockContext LockPayloadBuffer(PayloadHandle handle)
        {
            // NOTE: SpinLock can deadlock if Lock is called on uninitialized data.
            if (!IsInitialized)
                return default;

            using (var exclusiveLock = new SpinLockReadWrite.ScopedExclusiveLock(m_PayloadLockSync))
            {
                // Only allow active payload handles to be locked
                if (IsPayloadHandleValid(handle))
                {
                    // Create a new lock entry if one doesn't already exist for this handle
                    if (!m_LockedPayloads.TryGetValue(handle, out var data))
                    {
                        data = new PayloadBufferLockData();
                        var context = data.CreateNewContext();
                        m_LockedPayloads.Add(handle, data);
                        return context;
                    }
                    else
                    {
                        // Update an existing lock with a new context
                        var context = data.CreateNewContext();
                        if (context.IsValid)
                        {
                            m_LockedPayloads[handle] = data;
                        }
                        return context;
                    }
                }
            }

            return default;
        }

        /// <summary>
        /// Releases an existing Payload Lock on the specified buffer for a given context.
        /// </summary>
        /// <remarks>
        /// Each call to <see cref="LockPayloadBuffer(PayloadHandle)"/> generates a unique context value
        /// for a given buffer and Unlock must called for each context before the buffer can be safely released.
        /// While it's still possible to "force" release a locked Payload buffer, this isn't recommended.
        ///
        /// If the specified buffer wasn't locked for this specific context, the unlock operation will fail.
        /// </remarks>
        /// <param name="handle">A valid <see cref="PayloadHandle"/> value for the Payload buffer to unlock.</param>
        /// <param name="context">Value returned by preceding call to <see cref="LockPayloadBuffer(PayloadHandle)"/>.</param>
        /// <returns>True if unlock operation was successful or not.</returns>
        /// <seealso cref="LockPayloadBuffer(PayloadHandle)"/>
        /// <seealso cref="IsPayloadBufferLocked(PayloadHandle, out int)"/>
        /// <seealso cref="ReleasePayloadBuffer(PayloadHandle, out PayloadReleaseResult, bool)"/>
        public bool UnlockPayloadBuffer(PayloadHandle handle, PayloadLockContext context)
        {
            // NOTE: SpinLock can deadlock if Lock is called on uninitialized data.
            if (!IsInitialized)
                return false;

            using (var exclusiveLock = new SpinLockReadWrite.ScopedExclusiveLock(m_PayloadLockSync))
            {
                if (m_LockedPayloads.TryGetValue(handle, out var data))
                {
                    if (data.RemoveLockContext(context))
                    {
                        // If all locks have been released, then remove this payload from the map
                        if (data.LockCount == 0)
                        {
                            m_LockedPayloads.Remove(handle);
                        }
                        else
                        {
                            m_LockedPayloads[handle] = data;
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tests if the specified Payload buffer is locked and returns the number of individual locks.
        /// </summary>
        /// <remarks>
        /// See <see cref="IsPayloadBufferLocked(PayloadHandle, out int)"/>.
        /// </remarks>
        /// <param name="handle">A valid <see cref="PayloadHandle"/> value for the Payload buffer to test.</param>
        /// <returns>True if Payload buffer has at least 1 lock and false otherwise.</returns>
        public bool IsPayloadBufferLocked(PayloadHandle handle)
        {
            return IsPayloadBufferLocked(handle, out _);
        }

        /// <summary>
        /// Tests if the specified Payload buffer is locked and returns the number of individual locks.
        /// </summary>
        /// <param name="handle">A valid <see cref="PayloadHandle"/> value for the Payload buffer to test.</param>
        /// <param name="numLockContexts">Number of active locks on this Payload buffer.</param>
        /// <returns>True if Payload buffer has at least 1 lock and false otherwise.</returns>
        /// <seealso cref="LockPayloadBuffer(PayloadHandle)"/>
        /// <seealso cref="UnlockPayloadBuffer"/>
        /// <seealso cref="ReleasePayloadBuffer(PayloadHandle, out PayloadReleaseResult, bool)"/>
        public bool IsPayloadBufferLocked(PayloadHandle handle, out int numLockContexts)
        {
            // NOTE: SpinLock can deadlock if Lock is called on uninitialized data.
            numLockContexts = 0;

            if (!IsInitialized)
            {
                return false;
            }

            using (var readLock = new SpinLockReadWrite.ScopedReadLock(m_PayloadLockSync))
            {
                var result = m_LockedPayloads.TryGetValue(handle, out var data);

                if (result)
                    numLockContexts = data.LockCount;

                return result;
            }
        }

        // Internal properties for testing
        internal int MovingAverageSampleCount => m_MovingAverage.SampleCount;
        internal ulong MovingAverageTotal => m_MovingAverage.Total;
        internal bool MovingAverageCreated => m_MovingAverage.SampleQueueCreated;
        internal bool MovingAverageHasMaxSamples => m_MovingAverage.HasMaximumSamples;
        internal uint DefaultBufferACapacity => m_DefaultBufferA.Capacity;
        internal uint DefaultBufferBCapacity => m_DefaultBufferB.Capacity;
        internal uint OverflowBufferCapacity => m_OverflowBuffer.Capacity;
        internal uint DefaultBufferAUsage => m_DefaultBufferA.BytesAllocated;
        internal uint DefaultBufferBUsage => m_DefaultBufferB.BytesAllocated;
        internal uint OverflowBufferUsage => m_OverflowBuffer.BytesAllocated;
        internal bool IsUsingBufferA => m_UseBufferA;
        internal bool IsOverflowEnabled => m_OverflowBuffer.IsCreated;

        int m_IgnoreOutOfMemoryError; // blittable type

        internal bool IgnoreOutOfMemoryError
        {
            get => m_IgnoreOutOfMemoryError != 0;
            set => m_IgnoreOutOfMemoryError = value ? 1 : 0;
        }

        private LogMemoryManagerParameters  m_BufferParams;
        private UnsafePayloadRingBuffer     m_DefaultBufferA;
        private UnsafePayloadRingBuffer     m_DefaultBufferB;
        private UnsafePayloadRingBuffer     m_OverflowBuffer;
        private SimpleMovingAverage         m_MovingAverage;

        private UnsafeList<PayloadHandle>   m_PayloadHandlesToReleaseX;
        private UnsafeList<PayloadHandle>   m_PayloadHandlesToReleaseY;

        private SpinLockReadWrite           m_UpdateLock;

        private PayloadLockHashMap          m_LockedPayloads;
        private SpinLockReadWrite           m_PayloadLockSync;
        private SpinLockExclusive           m_PayloadReleaseDeferredListLockSync;
        private uint                        m_OverflowResizeFence;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        private long                        m_UpdateCounter;
#endif

#if LOGGING_MEM_DEBUG
        private UnsafeParallelHashSet<PayloadHandle> m_AllocatedPayloads;
#endif

        private volatile int m_ResizingBlittable;
        private volatile int m_UseBufferABlittable;
        private volatile int m_UseBufferXBlittable;
        private volatile int m_InitializedBlittable;

        private bool m_Resizing
        {
            get => m_ResizingBlittable != 0;
            set => m_ResizingBlittable = value ? 1 : 0;
        }
        private bool m_UseBufferA
        {
            get => m_UseBufferABlittable != 0;
            set => m_UseBufferABlittable = value ? 1 : 0;
        }

        private bool m_UseBufferX
        {
            get => m_UseBufferXBlittable != 0;
            set => m_UseBufferXBlittable = value ? 1 : 0;
        }
        private bool m_Initialized
        {
            get => m_InitializedBlittable != 0;
            set => m_InitializedBlittable = value ? 1 : 0;
        }

        private bool CheckBufferThresholdsAndResizeIfNeeded(bool overflowTriggered)
        {
            // Don't even think about resizing until the "alternate" RingBuffer is empty
            if ((m_UseBufferA && !m_DefaultBufferB.IsRingBufferEmpty()) ||
                (!m_UseBufferA && !m_DefaultBufferA.IsRingBufferEmpty()))
            {
                return false;
            }

            // Also don't resize until we have a full complement of samples (unless overflow tripped)
            if (!m_MovingAverage.HasMaximumSamples && !overflowTriggered)
            {
                return false;
            }

            // Retrieve current average utilization, make sure it's valid
            float currUsage = m_MovingAverage.Average;
            if (float.IsNaN(currUsage))
            {
                return false;
            }

            uint newCapacity = 0;
            var currCapacity = GetCurrentDefaultBufferCapacity();
            var currUsageRatio = (float)currUsage / (float)currCapacity;

            // Check if average utilization crosses thresholds (or an overflow was triggered) to compute a new size for the buffer,
            // provided grow/shrink haven't been disabled.
            // NOTE: Overflow only triggered an increase in buffer size and only if BufferGrowth is allowed
            if ((currUsageRatio >= m_BufferParams.BufferGrowThreshold || overflowTriggered) && m_BufferParams.BufferGrowThreshold > 0)
            {
                newCapacity = (uint)(currCapacity * m_BufferParams.BufferGrowFactor + 0.5f);
            }
            else if (currUsageRatio <= m_BufferParams.BufferShrinkThreshold && m_BufferParams.BufferShrinkThreshold > 0)
            {
                newCapacity = (uint)(currCapacity * m_BufferParams.BufferShrinkFactor + 0.5f);
            }

            if (newCapacity != 0)
            {
                if (newCapacity > UnsafePayloadRingBuffer.MaximumCapacity)
                {
                    newCapacity = UnsafePayloadRingBuffer.MaximumCapacity;
                }
                else if (newCapacity < UnsafePayloadRingBuffer.MinimumCapacity)
                {
                    newCapacity = UnsafePayloadRingBuffer.MinimumCapacity;
                }

                if (m_UseBufferA)
                {
                    m_DefaultBufferB.Dispose();
                    m_DefaultBufferB = new UnsafePayloadRingBuffer(newCapacity, DefaultBufferBId, Allocator.Persistent);
                    m_UseBufferA = false;
                }
                else
                {
                    m_DefaultBufferA.Dispose();
                    m_DefaultBufferA = new UnsafePayloadRingBuffer(newCapacity, DefaultBufferAId, Allocator.Persistent);
                    m_UseBufferA = true;
                }

                // Reset MovingAverage of old Samples; allows a new Average to stabilize before trying to resize again
                m_MovingAverage.Flush();
            }

            return newCapacity != 0;
        }

        private bool UpdateResizing()
        {
            bool stillResizing;

            if (!m_UseBufferA && m_DefaultBufferA.IsRingBufferEmpty())
            {
                m_DefaultBufferA.Dispose();
                stillResizing = false;
            }
            else if (m_UseBufferA && m_DefaultBufferB.IsRingBufferEmpty())
            {
                m_DefaultBufferB.Dispose();
                stillResizing = false;
            }
            else stillResizing = true;

            return stillResizing;
        }

        private PayloadHandle AllocatePayloadBufferInternal(uint payloadSize, out NativeArray<byte> buffer, bool disjointedBuffer)
        {
            bool success;
            PayloadHandle handle;

            if (m_UseBufferA)
            {
                success = m_DefaultBufferA.AllocatePayload(payloadSize, out handle, out buffer, disjointedBuffer);
            }
            else
            {
                success = m_DefaultBufferB.AllocatePayload(payloadSize, out handle, out buffer, disjointedBuffer);
            }

            // If failed to allocate from default buffer, try again from the overflow buffer
            if (!success && m_OverflowBuffer.IsCreated)
            {
                success = m_OverflowBuffer.AllocatePayload(payloadSize, out handle, out buffer, disjointedBuffer);
            }

            if (!success)
            {
                OnFailedToAllocateMemory();
            }
            else
            {
#if LOGGING_MEM_DEBUG
                var res = m_AllocatedPayloads.Add(handle);
                Assert.IsTrue(res);

                DebugMemPayloadBufferAlloc(handle);
#endif
            }

            return success ? handle : new PayloadHandle();
        }

        /// <summary>
        /// Copy existing <see cref="PayloadHandle"/> to a new one. Usually used for constant decorations
        /// <seealso cref="LogController.AddConstDecorateHandlers"/>
        /// </summary>
        /// <param name="handle"><see cref="PayloadHandle"/> to copy from in this <see cref="LogMemoryManager"/></param>
        /// <returns>New copied <see cref="PayloadHandle"/> in this <see cref="LogMemoryManager"/></returns>
        internal PayloadHandle Copy(PayloadHandle handle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.IsTrue(IsPayloadHandleValid(handle));
#endif
            var success = RetrievePayloadBuffer(handle, false, out var src);
            if (success)
            {
                var copyHandle = AllocatePayloadBufferInternal((uint)src.Length, out var dest, handle.IsDisjointedBuffer);
                src.CopyTo(dest);

                DebugMemPayloadBufferCopy(handle, copyHandle);

                return copyHandle;
            }

            return default;
        }

        /// <summary>
        /// Copy existing <see cref="PayloadHandle"/> to a new one in another <see cref="LogMemoryManager"/>. Usually used for constant decorations
        /// <seealso cref="Unity.Logging.Internal.LoggerManager.AddConstDecorateHandlers"/>
        /// </summary>
        /// <param name="destMemoryManager"><see cref="LogMemoryManager"/> where to create a copy</param>
        /// <param name="handle"><see cref="PayloadHandle"/> to copy from in this <see cref="LogMemoryManager"/></param>
        /// <returns>New copied <see cref="PayloadHandle"/> in destMemoryManager <see cref="LogMemoryManager"/></returns>
        internal PayloadHandle Copy(ref LogMemoryManager destMemoryManager, PayloadHandle handle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.IsTrue(IsPayloadHandleValid(handle));
#endif

            var success = RetrievePayloadBuffer(handle, false, out var src);
            if (success)
            {
                var copyHandle = destMemoryManager.AllocatePayloadBufferInternal((uint)src.Length, out var dest, handle.IsDisjointedBuffer);
                src.CopyTo(dest);

                DebugMemPayloadBufferCopy(handle, copyHandle);

                return copyHandle;
            }

            return default;
        }

        private unsafe PayloadHandle AllocateDisjointedPayloadBufferInternal<T>(ref T payloadSizes, ref NativeList<PayloadHandle> payloadHandles)
            where T : struct, INativeList<ushort>
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            // Validate the number of payload handles will fit into the "head" buffer
            if (payloadHandles.IsCreated)
                CheckForMaxDisjointedPayloadCount(payloadHandles.Length);
            CheckForMaxDisjointedPayloadCount(payloadSizes.Length);

            // Validate the payloadSizes are within a valid range for a payload buffer
            for (int i = 0; i < payloadSizes.Length; i++)
            {
                var size = payloadSizes[i];
                if (size < UnsafePayloadRingBuffer.MinimumPayloadSize || size > UnsafePayloadRingBuffer.MaximumPayloadSize)
                    throw new ArgumentOutOfRangeException($"Specified Payload size {size} is invalid; must be in range [{UnsafePayloadRingBuffer.MinimumPayloadSize}, {UnsafePayloadRingBuffer.MaximumPayloadSize}]");
            }
#endif
            var handleSize = UnsafeUtility.SizeOf<PayloadHandle>();

            // In this case allocate our Disjointed Buffer "head" first, so we don't have to allocate a secondary array/list to hold payload handles
            var headHandle = AllocatePayloadBufferInternal((uint)(handleSize * payloadSizes.Length), out var headBuffer, true);
            if (!headHandle.IsValid)
                return headHandle;

            PayloadHandle* pHeadBuffer = (PayloadHandle*)headBuffer.GetUnsafePtr();

            int currCount = 0;
            bool failed = false;

            // Allocate all the "content" payload buffers as specified by payloadSizes
            // If any one of these allocations fails we'll have to fail the entire operation
            for (int i = 0; i < payloadSizes.Length; i++)
            {
                var handle = AllocatePayloadBufferInternal(payloadSizes[i], out var buffer, false);
                if (!handle.IsValid)
                {
                    failed = true;
                    break;
                }

                // Add the handle to the output list (if there's space)
                if (payloadHandles.IsCreated)
                    payloadHandles.Add(handle);

                // Write the new payload handle to our "head" buffer
                pHeadBuffer[currCount++] = handle;
            }

            // Something went wrong...have to clean up all the currently allocated memory
            //
            // NOTE: An allocation failure usually means we ran out of space in the RingBuffer, however the memory we've
            // already allocated doesn't actually get released until Update() is called. So if we hit this case we're
            // kind of screwed for the moment; cannot allocate any more payloads and the space that was available is now wasted.
            if (failed)
            {
                DebugMemDisjointedPayloadBufferAllocFailed(headHandle, ref payloadHandles);

                // Release all currently allocated buffers
                for (var i = currCount - 1; i >= 0; i--)
                {
                    ReleasePayloadBufferInternal(pHeadBuffer[i]);
                }

                // Release Disjointed head buffer
                ReleasePayloadBufferInternal(headHandle);

                // Clear payload buffers list (if necessary)
                if (payloadHandles.IsCreated && payloadHandles.Length > 0)
                    payloadHandles.Clear();

                // Clear handle for head buffer
                headHandle = new PayloadHandle();
            }
            else
            {
                DebugMemDisjointedPayloadBufferAlloc(headHandle, ref payloadHandles);
            }

            return headHandle;
        }

        private unsafe PayloadHandle CreateDisjointedPayloadBufferFromExistingPayloadsInternal<T>(ref T payloadHandles)
            where T : struct, INativeList<PayloadHandle>
        {
            var n = payloadHandles.Length;
            if (n == 0)
                return new PayloadHandle();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            // Check for errors or unsupported scenarios when creating Disjointed Buffer
            // - Payload handle is invalid or doesn't reference valid payload buffer
            // - Payload handle itself references a DisjointedBuffer; don't support "chaining" DisjointedBuffers together
            // - Number of payload handles will fit into the head buffer

            //var errorDetected = false;
            for (int i = 0; i < n; i++)
            {
                var handle = payloadHandles[i];
                if (IsPayloadHandleValid(handle) == false)
                {
                    UnityEngine.Debug.Log(string.Format("[DisjointedPayloadBuffer] not valid handle {0}. handle.m_Value = <{1}>", i, handle.m_Value));

                    FixedString4096Bytes debugStr = DebugDetailsOfPayloadHandle(ref handle);
                    UnityEngine.Debug.Log(debugStr);

            //        errorDetected = true;
                }
            }

            // if (errorDetected)
            // {
            //     UnityEngine.Debug.LogError(string.Format("[DisjointedPayloadBuffer] ERROR detected! {0} handles.", n));
            // }

            for (int i = 0; i < n; i++)
            {
                var handle = payloadHandles[i];

                if (IsPayloadHandleValid(handle) == false)
                {
                    throw new ArgumentException("Only valid Payload handles can be used within a Disjointed Buffer");
                }

                if (handle.IsDisjointedBuffer)
                    throw new ArgumentException("A Disjointed Buffer handle cannot be used within another Disjointed Buffer");
            }

            CheckForMaxDisjointedPayloadCount(n);
#endif
            var handleSize = UnsafeUtility.SizeOf<PayloadHandle>();

            var headHandle = AllocatePayloadBufferInternal((uint)(handleSize * n), out var headBuffer, true);
            if (!headHandle.IsValid)
                return headHandle;

            // Copy all the handle values into the newly created Payload buffer
            var pHeadBuffer = (PayloadHandle*)headBuffer.GetUnsafePtr();
            for (var i = 0; i < n; i++)
            {
                pHeadBuffer[i] = payloadHandles[i];
            }

            DebugMemDisjointedPayloadBufferAlloc(headHandle, ref payloadHandles);

            return headHandle;
        }

        private unsafe bool ReleaseDisjointedBufferPayloads(PayloadHandle handle, out PayloadReleaseResult result)
        {
            // IMPORTANT: Caller is responsible for ensuring thread safety!
            // Invokes ReleasePayloadBufferInternal() which must be called from a thread-safe context.

            if (!RetrievePayloadBuffer(handle, out var payloadBuffer))
            {
                result = PayloadReleaseResult.InvalidHandle;
                return false;
            }

            var numHandles = (int)(payloadBuffer.Length / (float)UnsafeUtility.SizeOf<PayloadHandle>());
            var handles = (PayloadHandle*)payloadBuffer.GetUnsafeReadOnlyPtr();

            // Attempt to release all the individual payloads from the Disjointed buffer
            // NOTE: We don't respect buffer "locks" on individual payloads, only the "head" buffer should be locked
            bool allGood = true;
            for (int i = 0; i < numHandles; i++)
            {
                if (!ReleasePayloadBufferInternal(handles[i]))
                    allGood = false;
            }

            DebugMemDisjointedPayloadBufferFree(handle, handles, numHandles);

            result = allGood ? PayloadReleaseResult.Success : PayloadReleaseResult.DisjointedPayloadReleaseFailed;
            return allGood;
        }

        private bool ReleasePayloadBufferInternal(PayloadHandle handle)
        {
            // IMPORTANT: Caller is responsible for ensuring thread safety!
            // m_LockedPayloads is modified by this method

            var bufferId = PayloadHandle.ExtractBufferIdFromHandle(ref handle);

            var success = bufferId switch
            {
                DefaultBufferAId => m_DefaultBufferA.ReleasePayload(handle),
                DefaultBufferBId => m_DefaultBufferB.ReleasePayload(handle),
                OverflowBufferId => m_OverflowBuffer.ReleasePayload(handle),
                _ => false
            };

            // Make sure the released handle is removed from the "Locked Payloads" map
            if (success)
            {
                m_LockedPayloads.Remove(handle);

                DebugMemPayloadBufferFree(handle);

#if LOGGING_MEM_DEBUG
                var removed = m_AllocatedPayloads.Remove(handle);
                Assert.IsTrue(removed);
#endif
            }
            else
            {
                DebugMemPayloadBufferFreeFailed(handle);
            }

            return success;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
        private void CheckForMaxDisjointedPayloadCount(int numPayloads)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            var maxNumPayloads = UnsafePayloadRingBuffer.MaximumDisjointedPayloadCount;
            if (numPayloads > maxNumPayloads)
                throw new ArgumentOutOfRangeException($"Number of Disjointed payload buffers {numPayloads} exceeds maximum allowed of {maxNumPayloads}\n" +
                    $"The maximum Payload size of {UnsafePayloadRingBuffer.MaximumPayloadSize} limits the number of Disjointed handles that can be referenced at once.");
#endif
        }

        private void OnFailedToAllocateMemory()
        {
            if (IgnoreOutOfMemoryError)
                return;

            // fallback to UnityEngine.Debug.Log, since com.unity.logging cannot work here
            FixedString4096Bytes s =
                "[com.unity.logging] Failed to allocate memory. Make sure you're unlocking all buffers after LockPayloadBuffer. Or if you're logging too much - try to change logging settings";
            s.Append('\n');
            s.Append(DebugStateString());

            Internal.Debug.SelfLog.OnFailedToAllocateMemory(s);
        }

        [Conditional("LOGGING_MEM_DEBUG")]
        private static void DebugMemDisjointedPayloadBufferAlloc<T>(PayloadHandle headHandle, ref T payloadHandles) where T : struct, INativeList<PayloadHandle>
        {
            var message = new FixedString4096Bytes();
            message.Append((FixedString64Bytes)"Disjointed Alloc: ");

            message.Append(headHandle.m_Value);
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
        private static void DebugMemDisjointedPayloadBufferAllocFailed<T>(PayloadHandle headHandle, ref T payloadHandles) where T : struct, INativeList<PayloadHandle>
        {
            var message = new FixedString4096Bytes();
            message.Append((FixedString64Bytes)"Failed to Disjointed Alloc: ");
            message.Append(headHandle.m_Value);
            message.Append((FixedString64Bytes)" with ");
            message.Append(payloadHandles.Length);
            message.Append((FixedString64Bytes)" handles:\n");

            for (var i = 0; i < payloadHandles.Length; i++)
            {
                message.Append((FixedString64Bytes)"   -");
                message.Append(payloadHandles[i].m_Value);
                message.Append('\n');
            }

            UnityEngine.Debug.LogError(message);
        }

        [Conditional("LOGGING_MEM_DEBUG")]
        private static unsafe void DebugMemDisjointedPayloadBufferFree(PayloadHandle handle, PayloadHandle* handles, int numHandles)
        {
            var message = new FixedString4096Bytes();
            message.Append((FixedString64Bytes)"Disjointed Free: ");
            message.Append(handle.m_Value);
            message.Append((FixedString64Bytes)" with ");
            message.Append(numHandles);
            message.Append((FixedString64Bytes)" handles:\n");

            for (var i = 0; i < numHandles; i++)
            {
                message.Append((FixedString64Bytes)"   -");
                message.Append(handles[i].m_Value);
                message.Append('\n');
            }

            UnityEngine.Debug.Log(message);
        }

        [Conditional("LOGGING_MEM_DEBUG")]
        private void DebugMemPayloadBufferAlloc(PayloadHandle handle)
        {
            var message = new FixedString4096Bytes();
            message.Append((FixedString64Bytes)"Alloc ");
            message.Append(handle.m_Value);
            if (handle.IsDisjointedBuffer)
                message.Append(" [Disjointed]");
            message.Append('\n');

            UnityEngine.Debug.Log(message);
        }

        [Conditional("LOGGING_MEM_DEBUG")]
        private static void DebugMemPayloadBufferFree(PayloadHandle handle)
        {
            var message = new FixedString4096Bytes();
            message.Append((FixedString64Bytes)"Remove ");
            message.Append(handle.m_Value);
            if (handle.IsDisjointedBuffer)
                message.Append(" [Disjointed]");
            message.Append('\n');
            UnityEngine.Debug.Log(message);
        }

        [Conditional("LOGGING_MEM_DEBUG")]
        private static void DebugMemPayloadBufferFreeDeferred(PayloadHandle handle)
        {
            var message = new FixedString4096Bytes();
            message.Append((FixedString64Bytes)"Remove Deferred (in 2 updates) ");
            message.Append(handle.m_Value);
            if (handle.IsDisjointedBuffer)
                message.Append(" [Disjointed]");
            message.Append('\n');
            UnityEngine.Debug.Log(message);
        }

        [Conditional("LOGGING_MEM_DEBUG")]
        private static void DebugMemPayloadBufferFreeFailed(PayloadHandle handle)
        {
            var message = new FixedString4096Bytes();
            message.Append((FixedString64Bytes)"Failed to remove ");
            message.Append(handle.m_Value);
            if (handle.IsDisjointedBuffer)
                message.Append(" [Disjointed]");
            message.Append('\n');
            UnityEngine.Debug.LogError(message);
        }

        [Conditional("LOGGING_MEM_DEBUG")]
        private static void DebugMemPayloadBufferCopy(PayloadHandle handle, PayloadHandle copyHandle)
        {
            var message = new FixedString4096Bytes();
            message.Append(handle.m_Value);
            message.Append((FixedString64Bytes)"--copy-->");
            message.Append(copyHandle.m_Value);
            if (handle.IsDisjointedBuffer)
                message.Append(" [Disjointed]");
            message.Append('\n');
            UnityEngine.Debug.Log(message);
        }

        /// <summary>
        /// Converts pointer into ref LogMemoryManager
        /// </summary>
        /// <param name="memoryManager">IntPtr that should reference LogMemoryManager. Cannot be null</param>
        /// <returns>LogMemoryManager converted from a pointer</returns>
        public static ref LogMemoryManager FromPointer(IntPtr memoryManager)
        {
            unsafe
            {
                return ref UnsafeUtility.AsRef<LogMemoryManager>(memoryManager.ToPointer());
            }
        }
    }
}
