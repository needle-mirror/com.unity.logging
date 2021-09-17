using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Logging;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Logging.Tests
{
    [TestFixture]
    public class MemoryManagerTests
    {
        private static readonly SharedStatic<LogMemoryManager> m_LogMemoryManagerInstance = SharedStatic<LogMemoryManager>.GetOrCreate<LogMemoryManager, MemoryManagerTests>(16);

        private static ref LogMemoryManager m_Instance => ref m_LogMemoryManagerInstance.Data;

        [SetUp]
        public void Setup()
        {
            m_LogMemoryManagerInstance.Data = new LogMemoryManager();
        }

        [TearDown]
        public void TearDown()
        {
            m_LogMemoryManagerInstance.Data = default;
        }

        [Test]
        public void InitializationParametersAreCorrect()
        {
            // NOTE: Not using NUnit TestCase attribute for these various cases; too many parameters and would just be confusing

            // Case 1: basic set of parameters
            {
                var parameters = MakeInitializationParameters(
                    10 * 1024,
                    365,
                    0.532f,
                    0.23f,
                    4.39f,
                    0.234f,
                    1024 * 200);
                var expected = parameters;
                ValidateInitializationParameters(parameters, expected);
            }

            // Case 2: max/min values
            {
                var parameters = GetMinMaxInitializationParameterValues(true);
                var expected = parameters;
                ValidateInitializationParameters(parameters, expected);

                parameters = GetMinMaxInitializationParameterValues(false);
                expected = parameters;
                ValidateInitializationParameters(parameters, expected);
            }

            // Case 3: out-of-range values (should resolve back to defaults)
            {
                var parameters = GetMinMaxInitializationParameterValues(true);
                parameters.InitialBufferCapacity += 10;
                parameters.BufferSampleCount += 3;
                parameters.BufferGrowThreshold += 1.2f;
                parameters.BufferShrinkThreshold += 3.3f;
                parameters.BufferGrowFactor += 1.3f;
                parameters.BufferShrinkFactor += 34.3f;
                parameters.OverflowBufferSize += 30;

                var expected = MakeInitializationParameters();
                ValidateInitializationParameters(parameters, expected);

                parameters = GetMinMaxInitializationParameterValues(false);
                parameters.InitialBufferCapacity--;
                parameters.BufferSampleCount -= 200;    // Unsigned value will underflow; should still trigger out-of-bounds check
                parameters.BufferGrowThreshold -= 0.5f;
                parameters.BufferShrinkThreshold -= 0.1f;
                parameters.BufferGrowFactor -= 1.4f;
                parameters.BufferShrinkFactor -= 0.1f;
                parameters.OverflowBufferSize -= 15;

                expected = MakeInitializationParameters();
                ValidateInitializationParameters(parameters, expected);
            }

            // Case 4: some out-of-range but others valid; only invalid params reset
            {
                var maxParams = GetMinMaxInitializationParameterValues(true);
                maxParams.BufferGrowThreshold += 10.0f;
                maxParams.BufferShrinkFactor += 10000.0f;

                {
                    var expected = maxParams;
                    expected.BufferGrowThreshold = LogMemoryManagerParameters.DefaultBufferGrowThreshold;
                    expected.BufferShrinkFactor = LogMemoryManagerParameters.DefaultBufferShrinkFactor;
                    ValidateInitializationParameters(maxParams, expected);
                }


                var minParams = GetMinMaxInitializationParameterValues(true);
                minParams.BufferShrinkThreshold -= 34.0f;
                minParams.BufferGrowFactor -= 203.3f;

                {
                    var expected = maxParams;
                    expected.BufferShrinkThreshold = LogMemoryManagerParameters.DefaultBufferShrinkThreshold;
                    expected.BufferGrowFactor = LogMemoryManagerParameters.DefaultBufferGrowFactor;
                }
            }

            // Case 5: Resizing and Overflow buffer are disabled
            {
                var parameters = MakeInitializationParameters();
                parameters.BufferGrowThreshold = 0;
                parameters.BufferShrinkThreshold = 0;
                parameters.OverflowBufferSize = 0;
                var expected = parameters;

                ValidateInitializationParameters(parameters, expected);
            }
        }

        [TestCase(100, 0.0, 0.15)]
        [TestCase(100, 0.75, 0.0)]
        [TestCase(100, 0.0, 0.0)]
        [TestCase(0, 0.75, 0.15)]
        public void AutomaticBufferResizingCanBeDisabled(int sampleCount, double growThreshold, double shrinkThreshold)
        {
            var parameters = MakeInitializationParameters(
                1024 * 3,
                (uint)sampleCount,
                (float)growThreshold,
                (float)shrinkThreshold);

            m_Instance.Initialize(parameters);
            try
            {
                // Validate MovingAverage is not initialized when automatic resize is disabled
                bool shouldMovingAverageBeUninitialized = sampleCount == 0 || (growThreshold == 0.0f && shrinkThreshold == 0.0f);
                Assert.AreEqual(shouldMovingAverageBeUninitialized, !m_Instance.MovingAverageCreated);

                // Validate MovingAverage is initialized otherwise
                Assert.AreEqual(!shouldMovingAverageBeUninitialized, m_Instance.MovingAverageCreated);
            }
            finally
            {
                m_Instance.Shutdown();
            }
        }

        // IMPORTANT: The Resizing logic uses a Simple Moving average to calculate buffer utilization vs. thresholds. Since this test
        // alternates 1 to 1 ratio of Allocate/Update calls, it's possible for the buffer to fill up before the threshold has been reached.
        // This is a limitation of the rather simple logic currently being in used and ideally should use a more robust solution,
        // e.g. use weighted values that return larger utilization averages as buffer fills up.
        // For now test cases need to be "generous" growth thresholds.
        //
        // ADDITIONAL: Use of the Overflow buffer shouldn't have any impact on this test; we expect the default buffer to grow before
        // it runs out of space. Nevertheless we'll cover cases both with and without overflow buffer enabled.

        [TestCase(2000, true, 0.5, 2.0, 40, false, ExpectedResult = 4000, Description = "Small buffer with small growth")]
        [TestCase(20000, true, 0.75, 5.0, 480, false, ExpectedResult = 100000, Description = "Moderate initial buffer with large growth")]
        [TestCase(2000, true, 0.5, 10.0, 120, false, ExpectedResult = 20000, Description = "Small buffer with very large growth")]
        [TestCase(2000, true, 0.5, 2.0, 40, false, ExpectedResult = 4000, Description = "Small buffer with small growth (Overflow enabled)")]
        [TestCase(20000, true, 0.75, 5.0, 480, true, ExpectedResult = 100000, Description = "Moderate initial buffer with large growth  (Overflow enabled)")]
        [TestCase(2000, true, 0.5, 10.0, 120, true, ExpectedResult = 20000, Description = "Small buffer with very large growth  (Overflow enabled)")]
        [TestCase(2000, false, 0.5, 2.0, 40, true, ExpectedResult = 2000, Description = "Resize disabled")]
        [TestCase(2000, true, 0, 2.0, 30, false, ExpectedResult = 2000, Description = "Growth resize disabled")]
        public uint BufferGrowsWhenThresholdIsReached(int initialCapacity, bool enableResize, double growThreshold, double growFactor, int numAllocations, bool allowOverflow)
        {
            // A sampleCount of 0 disables automatic resize
            uint numSamples = enableResize ? 10u : 0;

            // Allocations are fixed at 50 bytes (includes the header)
            uint allocationSize = 50 - PayloadBlockHeader.HeaderSize;

            var parameters = MakeInitializationParameters(
                (uint)initialCapacity,
                numSamples,
                (float)growThreshold,
                0.0f,   // Disable buffer shrinking for this test
                (float)growFactor,
                1.0f,   // Don't care about shrink factor
                allowOverflow ? 1024 * 200 : 0u  // Use a large overflow buffer size (if enabled)
            );

            bool expectToGrow = enableResize && growThreshold != 0;

            m_Instance.Initialize(parameters);
            try
            {
                // Allocate specified number of payload buffers
                // Break if allocations fails; we're run out of space
                for (int i = 0; i < numAllocations; i++)
                {
                    PayloadHandle handle;

                    handle = m_Instance.AllocatePayloadBuffer(allocationSize);
                    if (!handle.IsValid)
                    {
                        if (expectToGrow)
                        {
                            Assert.Fail("Failed to allocate Payload; buffer didn't grow as expected");
                        }
                        break;
                    }

                    // Pump update after each allocation which performs resizing logic
                    m_Instance.Update();
                }

                uint currCapacity = m_Instance.GetCurrentDefaultBufferCapacity();

                if (expectToGrow)
                {
                    Assert.That(currCapacity > initialCapacity, "Buffer didn't grow as expected");
                }
                else
                {
                    Assert.That(currCapacity == initialCapacity, "Buffer changed size when it wasn't supposed to");
                }
                return currCapacity;
            }
            finally
            {
                m_Instance.Shutdown();
            }
        }

        static uint CurrentUsage(ref LogMemoryManager instance, bool expectedOverflow)
        {
            if (expectedOverflow)
                return instance.OverflowBufferUsage;
            return instance.GetCurrentDefaultBufferUsage();
        }

        void FillTillFull(ref LogMemoryManager instance, Queue<PayloadHandle> q, uint size, bool expectedOverflow = false)
        {
            var realSize = UnsafePayloadRingBuffer.RoundToNextAlign(size) + PayloadBlockHeader.HeaderSize;

            var expectingNext = CurrentUsage(ref instance, expectedOverflow) + realSize;
            while (expectingNext <= (expectedOverflow ? instance.OverflowBufferCapacity : instance.GetCurrentDefaultBufferCapacity()))
            {
                q.Enqueue(AllocatePayloadAndValidate(size, expectedOverflow));
                UnityEngine.Assertions.Assert.AreEqual(expectingNext, CurrentUsage(ref instance, expectedOverflow), "wrong calculations");
                expectingNext = CurrentUsage(ref instance, expectedOverflow) + realSize;
            }
        }

        // IMPORTANT: As with BufferGrowsWhenThresholdIsReached we're using contrived parameters in order to managed MovingAverage logic.
        // We need to force Shrink logic, but typically you wouldn't use these values for shrink threshold/factor.
        // Additionally, use of the Overflow buffer has no bearing on the buffer shrinking logic, but we'll still cover cases with it
        // enabled/disabled to ensure proper covereage.

        [TestCase(3000, true, 0.5, 0.9, 40, false, ExpectedResult = 2700, Description = "Small buffer with small reduction")]
        [TestCase(40000, true, 0.4, 0.1, 600, false, ExpectedResult = 4000, Description = "Moderate initial buffer with large reduction")]
        [TestCase(60000, true, 0.3, 0.02, 925, false, ExpectedResult = 1200, Description = "Large buffer with very large reduction")]
        [TestCase(3000, true, 0.5, 0.9, 40, true, ExpectedResult = 2700, Description = "Small buffer with small reduction  (Overflow enabled)")]
        [TestCase(40000, true, 0.4, 0.1, 600, true, ExpectedResult = 4000, Description = "Moderate initial buffer with large reduction  (Overflow enabled)")]
        [TestCase(60000, true, 0.3, 0.02, 925, true, ExpectedResult = 1200, Description = "Large buffer with very large reduction  (Overflow enabled)")]
        [TestCase(2000, false, 0.25, 0.5, 30, false, ExpectedResult = 2000, Description = "Resize disabled")]
        [TestCase(2000, true, 0, 0.2, 30, false, ExpectedResult = 2000, Description = "Shrink resize disabled")]
        public uint BufferShrinksWhenThresholdIsReached(int initialCapacity, bool enableResize, double shrinkThreshold, double shrinkFactor, int numReleases, bool allowOverflow)
        {
            var handleQueue = new System.Collections.Generic.Queue<PayloadHandle>(500);

            // A sampleCount of 0 disables automatic resize
            uint numSamples = enableResize ? 10u : 0;

            // Allocations are fixed at 50 bytes (includes the header)
            uint allocationSize = 50 - PayloadBlockHeader.HeaderSize;

            var parameters = MakeInitializationParameters(
                (uint)initialCapacity,
                numSamples,
                0.0f,   // Disable buffer growing for this test
                (float)shrinkThreshold,
                1.0f,   // Don't care about grow factor
                (float)shrinkFactor,
                allowOverflow ? 1024 * 200 : 0u  // Use a large overflow buffer size (if enabled)
            );

            bool expectToShrink = enableResize && shrinkThreshold != 0;

            m_Instance.Initialize(parameters);
            try
            {
                // Fill buffer with allocations up to initialCapacity; save the handles this time
                FillTillFull(ref m_Instance, handleQueue, allocationSize);

                // Need to pump Update to fill up MovingAverage samples
                for (int i = 0; i < numSamples; i++)
                {
                    m_Instance.Update();
                }

                // Now release specified number of Payload buffers
                for (int i = 0; i < numReleases; i++)
                {
                    Assert.That(handleQueue.Count > 0, "Specified numReleases parameter exceeds number of actual allocations; either decrease numReleases or increase initialCapacity");

                    ReleasePayloadAndValidate(handleQueue.Dequeue());
                    m_Instance.Update();
                }

                uint currCapacity = m_Instance.GetCurrentDefaultBufferCapacity();

                if (expectToShrink)
                {
                    Assert.That(currCapacity < initialCapacity, "Buffer didn't shrink as expected");
                }
                else
                {
                    Assert.That(currCapacity == initialCapacity, "Buffer changed size when it wasn't supposed to");
                }
                return currCapacity;
            }
            finally
            {
                m_Instance.Shutdown();
            }
        }

        [TestCase(2000, 2000, 95, Description = "Small main/overflow buffers and small allocation size")]
        [TestCase(2000, 5000, 1000, Description = "Small main/overflow buffers and large allocation size")]
        [TestCase(100000, 10000, 103, Description = "Large main/overflow buffer and small allocation size")]
        [TestCase(100000, 1500, 1009, Description = "Large main/small overflow buffer and large allocation size")]
        [TestCase(2000, 100000, 1750, Description = "Small main/large overflow buffer and large allocation size")]
        public void OverflowBufferUtilizedWhenOutOfSpace(int bufferCapacity, int overflowCapacity, int allocationSize)
        {
            var bufferHandleQueue = new System.Collections.Generic.Queue<PayloadHandle>(500);
            var overflowHandleQueue = new System.Collections.Generic.Queue<PayloadHandle>(500);

            allocationSize -= (int)PayloadBlockHeader.HeaderSize;
            var realSize = UnsafePayloadRingBuffer.RoundToNextAlign((uint)allocationSize) + PayloadBlockHeader.HeaderSize;
            Assert.GreaterOrEqual(allocationSize, UnsafePayloadRingBuffer.MinimumPayloadSize);

            // Disable buffer resizing for this test
            var parameters = MakeInitializationParameters(
                (uint)bufferCapacity,
                0,
                0.0f,
                0.0f,
                0.0f,
                0.0f,
                (uint)overflowCapacity
            );

            m_Instance.Initialize(parameters);
            try
            {
                // Fill buffer with allocations up to it's capacity
                FillTillFull(ref m_Instance, bufferHandleQueue, (uint)allocationSize, false);

                Assert.Zero(m_Instance.OverflowBufferUsage);

                // Now fill up the Overflow buffer with allocations until it's also full
                // NOTE: AllocatePayloadBuffer should NOT fail during this phase
                FillTillFull(ref m_Instance, overflowHandleQueue, (uint)allocationSize, true);

                var lastOverflowUsage = m_Instance.OverflowBufferUsage;
                Assert.NotZero(lastOverflowUsage);

                Internal.Debug.SelfLog.SetMode(Internal.Debug.SelfLog.Mode.EnabledInMemory);
                // Attempt another allocation, expect this one WILL FAIL
                using (var scope = new Internal.Debug.SelfLog.Assert.TestScope(Allocator.Persistent))
                {
                    scope.ExpectingOutOfMemory();
                    var handle = m_Instance.AllocatePayloadBuffer((uint)allocationSize);
                    Assert.IsFalse(handle.IsValid, "Expected buffer allocation to fail");
                }

                // Release buffers from main buffer in order to validate we can allocate new buffers (from main)
                while (bufferHandleQueue.Count > 0)
                {
                    ReleasePayloadAndValidate(bufferHandleQueue.Dequeue());
                }
                m_Instance.Update();

                // Check main buffer is empty and Overflow usage hasn't changed
                Assert.Zero(m_Instance.GetCurrentDefaultBufferUsage());
                Assert.AreEqual(lastOverflowUsage, m_Instance.OverflowBufferUsage);

                // Again test we can allocate more payloads from the main buffer; fill it up again
                FillTillFull(ref m_Instance, bufferHandleQueue, (uint)allocationSize);

                var lastBufferUsage = m_Instance.GetCurrentDefaultBufferUsage();

                Internal.Debug.SelfLog.SetMode(Internal.Debug.SelfLog.Mode.EnabledInMemory);
                // Attempt another allocation, expect this one WILL FAIL
                using (var scope = new Internal.Debug.SelfLog.Assert.TestScope(Allocator.Persistent))
                {
                    scope.ExpectingOutOfMemory();
                    var handle = m_Instance.AllocatePayloadBuffer((uint)allocationSize);
                    Assert.IsFalse(handle.IsValid, "Expected buffer allocation to fail");
                }

                // Release payloads from Overflow buffer in order to validate we can allocate new buffers (from overflow)
                while (overflowHandleQueue.Count > 0)
                {
                    ReleasePayloadAndValidate(overflowHandleQueue.Dequeue());
                }
                m_Instance.Update();

                // Check overflow buffer is empty and main usage hasn't changed
                Assert.Zero(m_Instance.OverflowBufferUsage);
                Assert.AreEqual(lastBufferUsage, m_Instance.GetCurrentDefaultBufferUsage());

                // Finally test we can allocate again from Overflow buffer
                overflowHandleQueue.Enqueue(AllocatePayloadAndValidate((uint)allocationSize, true));
            }
            finally
            {
                m_Instance.Shutdown();
            }
        }

        [Test]
        public void ValidatePayloadBufferRelease()
        {
            // Use basic settings (no resize, no overflow, etc.) for this test
            var parameters = MakeInitializationParameters(
                (uint)1024 * 200, // Give ample space for all the allocations (won't be reclaiming them)
                0,
                0.0f,
                0.0f,
                0.0f,
                0.0f,
                0   // No overflow buffer; just increase main buffer's Capacity
            );

            var disjointHandles = new NativeList<PayloadHandle>();
            m_Instance.Initialize(parameters);
            try
            {
                PayloadReleaseResult result;

                // Normal payload scenarios
                {
                    var handle = m_Instance.AllocatePayloadBuffer(50);
                    Assert.IsTrue(handle.IsValid);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, true, PayloadReleaseResult.Success);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                }
                {
                    var handle = m_Instance.AllocatePayloadBuffer(50);
                    Assert.IsTrue(handle.IsValid);
                    ValidatePayloadReleaseResults(handle, disjointHandles, true, true, PayloadReleaseResult.Success);
                    ValidatePayloadReleaseResults(handle, disjointHandles, true, false, PayloadReleaseResult.InvalidHandle);
                }
                {
                    var handle = m_Instance.AllocatePayloadBuffer(50);
                    Assert.IsTrue(handle.IsValid);
                    m_Instance.ReleasePayloadBuffer(handle, out result);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                    ValidatePayloadReleaseResults(handle, disjointHandles, true, false, PayloadReleaseResult.InvalidHandle);
                }
                {
                    PayloadHandle handle;
                    PayloadHandleData data = new PayloadHandleData
                    {
                        Offset = 35,
                        Version = 1002,
                        BufferId = 20,
                        BitFields = 0,
                    };
                    PayloadHandle.CreateHandleFromFields(ref data, out handle);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                }
                {
                    var handle = m_Instance.AllocatePayloadBuffer(50);
                    Assert.IsTrue(handle.IsValid);
                    var context = m_Instance.LockPayloadBuffer(handle);
                    Assert.IsTrue(context.IsValid);

                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.BufferLocked);
                    ValidatePayloadReleaseResults(handle, disjointHandles, true, true, PayloadReleaseResult.ForcedRelease);
                    ValidatePayloadReleaseResults(handle, disjointHandles, true, false, PayloadReleaseResult.InvalidHandle);
                }
                {
                    var handle = m_Instance.AllocatePayloadBuffer(50);
                    Assert.IsTrue(handle.IsValid);
                    var context = m_Instance.LockPayloadBuffer(handle);
                    Assert.IsTrue(context.IsValid);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.BufferLocked);
                    m_Instance.UnlockPayloadBuffer(handle, context);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, true, PayloadReleaseResult.Success);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                }
                {
                    var handle = m_Instance.AllocatePayloadBuffer(50);
                    Assert.IsTrue(handle.IsValid);
                    var context = m_Instance.LockPayloadBuffer(handle);
                    Assert.IsTrue(context.IsValid);
                    ValidatePayloadReleaseResults(handle, disjointHandles, true, true,
                        PayloadReleaseResult.ForcedRelease);
                    m_Instance.UnlockPayloadBuffer(handle, context);
                }
                // Allocate Disjointed buffer scenarios
                disjointHandles = new NativeList<PayloadHandle>(20, Allocator.Temp);
                var payloadSizes = new FixedList64Bytes<ushort>()
                {
                    21, 46, 100, 63
                };

                {
                    var handle = m_Instance.AllocateDisjointedBuffer(ref payloadSizes, disjointHandles);
                    Assert.IsTrue(handle.IsValid);

                    ValidatePayloadReleaseResults(handle, disjointHandles, false, true, PayloadReleaseResult.Success);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                    disjointHandles.Clear();
                }
                {
                    var handle = m_Instance.AllocateDisjointedBuffer(ref payloadSizes, disjointHandles);
                    Assert.IsTrue(handle.IsValid);

                    var context = m_Instance.LockPayloadBuffer(handle);
                    Assert.IsTrue(context.IsValid);

                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.BufferLocked);
                    ValidatePayloadReleaseResults(handle, disjointHandles, true, true, PayloadReleaseResult.ForcedRelease);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                    m_Instance.UnlockPayloadBuffer(handle, context);

                    disjointHandles.Clear();
                }
                {
                    var handle = m_Instance.AllocateDisjointedBuffer(ref payloadSizes, disjointHandles);
                    Assert.IsTrue(handle.IsValid);

                    // Locking Disjointed Payloads isn't supported and the lock is ignored when head buffer released
                    var context = m_Instance.LockPayloadBuffer(disjointHandles[1]);
                    Assert.IsTrue(context.IsValid);

                    ValidatePayloadReleaseResults(handle, disjointHandles, false, true, PayloadReleaseResult.Success);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                    m_Instance.UnlockPayloadBuffer(handle, context);

                    disjointHandles.Clear();
                }
                {
                    var handle = m_Instance.AllocateDisjointedBuffer(ref payloadSizes, disjointHandles);
                    Assert.IsTrue(handle.IsValid);

                    // Manually releasing a Disjointed buffer payload is supported but NOT recommended
                    PayloadReleaseResult disjointResult;
                    Assert.IsTrue(m_Instance.ReleasePayloadBuffer(disjointHandles[2], out disjointResult, false));
                    Assert.AreEqual(PayloadReleaseResult.Success, disjointResult);

                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.DisjointedPayloadReleaseFailed);
                    ValidatePayloadReleaseResults(handle, disjointHandles, true, true, PayloadReleaseResult.ForcedRelease);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                    disjointHandles.Clear();
                }
                {
                    var handle = m_Instance.AllocateDisjointedBuffer(ref payloadSizes, disjointHandles);
                    Assert.IsTrue(handle.IsValid);

                    PayloadReleaseResult disjointResult;
                    Assert.IsTrue(m_Instance.ReleasePayloadBuffer(disjointHandles[0], out disjointResult, false));
                    Assert.AreEqual(PayloadReleaseResult.Success, disjointResult);

                    var context = m_Instance.LockPayloadBuffer(handle);
                    Assert.IsTrue(context.IsValid);

                    // When disjointed buffer is Locked, that check (and result) takes precedence over an invalid payload
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.BufferLocked);
                    m_Instance.UnlockPayloadBuffer(handle, context);

                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.DisjointedPayloadReleaseFailed);

                    // Lock the Disjointed buffer again
                    context = m_Instance.LockPayloadBuffer(handle);
                    Assert.IsTrue(context.IsValid);

                    // Force release now applies to both locked buffer and invalid disjointed payload; everything should be released
                    ValidatePayloadReleaseResults(handle, disjointHandles, true, true, PayloadReleaseResult.ForcedRelease);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                    m_Instance.UnlockPayloadBuffer(handle, context);

                    disjointHandles.Clear();
                }

                // Create Disjointed buffer (from existing payloads) scenarios
                {
                    foreach (var size in payloadSizes)
                    {
                        disjointHandles.Add(m_Instance.AllocatePayloadBuffer(size));
                    }

                    var handle = m_Instance.CreateDisjointedPayloadBufferFromExistingPayloads(ref disjointHandles);
                    Assert.IsTrue(handle.IsValid);

                    ValidatePayloadReleaseResults(handle, disjointHandles, false, true, PayloadReleaseResult.Success);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                    disjointHandles.Clear();
                }
                {
                    foreach (var size in payloadSizes)
                    {
                        disjointHandles.Add(m_Instance.AllocatePayloadBuffer(size));
                    }

                    var handle = m_Instance.CreateDisjointedPayloadBufferFromExistingPayloads(ref disjointHandles);
                    Assert.IsTrue(handle.IsValid);

                    PayloadReleaseResult disjointResult;
                    Assert.IsTrue(m_Instance.ReleasePayloadBuffer(disjointHandles[3], out disjointResult, false));
                    Assert.AreEqual(PayloadReleaseResult.Success, disjointResult);

                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.DisjointedPayloadReleaseFailed);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.DisjointedPayloadReleaseFailed);
                    ValidatePayloadReleaseResults(handle, disjointHandles, true, true, PayloadReleaseResult.ForcedRelease);
                    ValidatePayloadReleaseResults(handle, disjointHandles, false, false, PayloadReleaseResult.InvalidHandle);
                    disjointHandles.Clear();
                }
            }
            finally
            {
                if (disjointHandles.IsCreated)
                    disjointHandles.Dispose();

                m_Instance.Shutdown();
            }
        }

        // NOTE: This test is a bit clumsy because it doesn't accurately track allocations/releases
        // across both buffers. Ideally it would track capacity and usage separately for
        // both buffers and better validate if/when grow/shrinks should occur.
        // As of now, the test is fragile and changing the grow/shrink thresholds will likely break it.
        //
        // ADDITIONAL: Unlike the other Resize tests, use of the Overflow buffer is specifically disabled; this
        // test must fail if resizing doesn't work properly, which could be subverted by the Overflow buffer.


        [Test]
        public void ValidateMultipleResizes()
        {
            var handleQueue = new System.Collections.Generic.Queue<PayloadHandle>(500);

            uint actualAllocationSize1 = 128;
            uint actualAllocationSize2 = 256;
            uint allocationSize1 = actualAllocationSize1 - PayloadBlockHeader.HeaderSize;
            uint allocationSize2 = actualAllocationSize2 - PayloadBlockHeader.HeaderSize;

            // Use hard-coded values for grow/shrink thresholds and resize factors so changes in defaults doesn't break test
            const uint initialCapacity = 5000;
            const uint bufferSampleCount = 30;
            const float growThreshold = 0.70f;
            const float shrinkThreshold = 0.12f;
            const float growFactor = 2.0f;
            const float shrinkFactor = 0.75f;
            const uint overflowBufferSize = 0; // Overflow buffer is deliberately disabled

            var parameters = MakeInitializationParameters(
                initialCapacity,
                bufferSampleCount,
                growThreshold,
                shrinkThreshold,
                growFactor,
                shrinkFactor,
                overflowBufferSize);

            m_Instance.Initialize(parameters);
            try
            {
                const int numIterationsPhase1 = 40;
                const int numIterationsPhase2 = 30;
                const int numIterationsPhase3 = 45;

                // Make absolutely sure Overflow isn't enabled
                Assert.IsFalse(m_Instance.IsOverflowEnabled);

                uint prevCapacity = m_Instance.GetCurrentDefaultBufferCapacity();
                uint currUsage = m_Instance.GetCurrentDefaultBufferUsage();
                bool releaseAlloc1 = true;
                bool usingBufferA = true;

                // Phase 1: execute allocate/release that steadily increases usage; should trigger a Grow
                for (int i = 0; i < numIterationsPhase1; i++)
                {
                    handleQueue.Enqueue(AllocatePayloadAndValidate(allocationSize1));
                    currUsage += actualAllocationSize1;

                    m_Instance.Update();
                    CheckForBufferSwapAndValidateUsage(ref currUsage, ref usingBufferA);

                    handleQueue.Enqueue(AllocatePayloadAndValidate(allocationSize2));
                    currUsage += actualAllocationSize2;

                    m_Instance.Update();
                    CheckForBufferSwapAndValidateUsage(ref currUsage, ref usingBufferA);

                    ReleasePayloadAndValidate(handleQueue.Dequeue());

                    // NOTE: This isn't quite right, releases only effect currUsage if they come from
                    // the "active" buffer. If release comes from alternate buffer then no impact on current usage.
                    currUsage -= releaseAlloc1 ? actualAllocationSize1 : actualAllocationSize2;
                    releaseAlloc1 = !releaseAlloc1;

                    m_Instance.Update();
                    CheckForBufferSwapAndValidateUsage(ref currUsage, ref usingBufferA);
                }

                // Validate a resize (grow) occurred and Capacity matches expected values
                Assert.That(!usingBufferA, "BufferB isn't the active buffer as expected");
                Assert.AreEqual((uint)math.ceil(prevCapacity * parameters.BufferGrowFactor), m_Instance.GetCurrentDefaultBufferCapacity());

                // Release buffers until BufferA (original buffer) is empty and has been deallocated
                do
                {
                    ReleasePayloadAndValidate(handleQueue.Dequeue());
                    releaseAlloc1 = !releaseAlloc1;
                    m_Instance.Update();
                }
                while (m_Instance.DefaultBufferACapacity > 0);

                currUsage = m_Instance.DefaultBufferBUsage;
                prevCapacity = m_Instance.DefaultBufferBCapacity;

                // Phase 2: execute releases that steadily decreases BufferB usage; should trigger a Shrink
                for (int i = 0; i < numIterationsPhase2; i++)
                {
                    ReleasePayloadAndValidate(handleQueue.Dequeue());
                    currUsage -= releaseAlloc1 ? actualAllocationSize1 : actualAllocationSize2;
                    releaseAlloc1 = !releaseAlloc1;

                    m_Instance.Update();
                    CheckForBufferSwapAndValidateUsage(ref currUsage, ref usingBufferA);
                }

                // Pump Update several more times to ensure resize threshold is triggered
                for (int j = 0; j < m_Instance.Parameters.BufferSampleCount; j++)
                {
                    m_Instance.Update();
                    CheckForBufferSwapAndValidateUsage(ref currUsage, ref usingBufferA);
                }

                // Validate a resize (shrink) occurred and Capacity matches expected values
                Assert.That(usingBufferA, "BufferA isn't the active buffer as expected");
                Assert.AreEqual((uint)math.ceil(prevCapacity * parameters.BufferShrinkFactor), m_Instance.GetCurrentDefaultBufferCapacity());

                currUsage = m_Instance.DefaultBufferBUsage;
                prevCapacity = m_Instance.DefaultBufferACapacity;

                // Phase 3: again execute allocations/releases that increase usage; should trigger another grow
                // Basically a repeat of phase 1
                for (int i = 0; i < numIterationsPhase3; i++)
                {
                    handleQueue.Enqueue(AllocatePayloadAndValidate(allocationSize1));
                    currUsage += actualAllocationSize1;

                    m_Instance.Update();
                    CheckForBufferSwapAndValidateUsage(ref currUsage, ref usingBufferA);

                    handleQueue.Enqueue(AllocatePayloadAndValidate(allocationSize2));
                    currUsage += actualAllocationSize2;

                    m_Instance.Update();
                    CheckForBufferSwapAndValidateUsage(ref currUsage, ref usingBufferA);

                    ReleasePayloadAndValidate(handleQueue.Dequeue());

                    currUsage -= releaseAlloc1 ? actualAllocationSize1 : actualAllocationSize2;
                    releaseAlloc1 = !releaseAlloc1;

                    m_Instance.Update();
                    CheckForBufferSwapAndValidateUsage(ref currUsage, ref usingBufferA);
                }

                // Validate a resize (grow) occurred and Capacity matches expected values
                Assert.That(!usingBufferA, "BufferB isn't the active buffer as expected");
                Assert.AreEqual((uint)math.ceil(prevCapacity * parameters.BufferGrowFactor), m_Instance.GetCurrentDefaultBufferCapacity());
            }
            finally
            {
                m_Instance.Shutdown();
            }
        }

        // NOTE: When a allocation from the Overflow buffer occurs, it should trigger an increase in the default buffer
        // size, regardless if the GrowThreshold was reached, which is validated by this test.

        [TestCase(2033, Description = "Small buffer capacity")]
        [TestCase(50033, Description = "Large buffer capacity")]
        public void ValidateOverflowTriggersResize(int initialCapacity)
        {
            const uint numSamples = 1000;
            const float growthFactor = 2.0f;

            var handleQueue = new System.Collections.Generic.Queue<PayloadHandle>(500);

            // Allocations are fixed at 50 bytes (includes the header)
            uint allocationSize = 100 - PayloadBlockHeader.HeaderSize;

            var parameters = MakeInitializationParameters(
                (uint)initialCapacity,
                numSamples,
                0.9999f,    // Use a ridiculously high GrowthThreshold; resize triggered by Overflow usage
                0.0f,       // Disable buffer shrinking
                growthFactor,
                0.0f,       // Don't care about shrink factor
                1024 * 200  // Use a large overflow buffer size
            );

            m_Instance.Initialize(parameters);
            try
            {
                int mainAllocationCount = 0;

                // Fill buffer with allocations up to initialCapacity; save the handles this time
                while (m_Instance.GetCurrentDefaultBufferUsage() + allocationSize < m_Instance.GetCurrentDefaultBufferCapacity())
                {
                    handleQueue.Enqueue(AllocatePayloadAndValidate(allocationSize, false));
                    m_Instance.Update();
                    mainAllocationCount++;
                }
                Assert.AreEqual(m_Instance.GetCurrentDefaultBufferCapacity(), initialCapacity);

                // Now allocate a larger payload, which should come from Overflow buffer and trigger a resize
                handleQueue.Enqueue(AllocatePayloadAndValidate(allocationSize * 3, true));

                var lastOverflowUsage = m_Instance.OverflowBufferUsage;
                Assert.NotZero(lastOverflowUsage);

                // Update should trigger buffer resize
                m_Instance.Update();
                Assert.AreEqual(m_Instance.GetCurrentDefaultBufferCapacity(), initialCapacity * growthFactor);

                // Additional updates should NOT increase buffer size
                for (int i = 0; i < (numSamples / 10); i++)
                {
                    m_Instance.Update();
                }
                Assert.AreEqual(m_Instance.GetCurrentDefaultBufferCapacity(), initialCapacity * growthFactor);

                // Allocate a new payload; it should NOT go into Overflow buffer (should be in Buffer B)
                handleQueue.Enqueue(AllocatePayloadAndValidate(allocationSize, false));
                Assert.AreEqual(lastOverflowUsage, m_Instance.OverflowBufferUsage);
                Assert.AreEqual(UnsafePayloadRingBuffer.RoundToNextAlign(allocationSize) + PayloadBlockHeader.HeaderSize, m_Instance.DefaultBufferBUsage);

                var secondAllocationCount = 1;

                // Release all the buffers from the "main" set of allocations but leave the Overflow allocation
                for (int i = 0; i < mainAllocationCount; i++)
                {
                    ReleasePayloadAndValidate(handleQueue.Dequeue());
                    m_Instance.Update();
                }

                Assert.Zero(m_Instance.DefaultBufferAUsage);
                Assert.Zero(m_Instance.DefaultBufferACapacity);
                Assert.NotZero(m_Instance.OverflowBufferUsage);
                Assert.AreEqual(m_Instance.GetCurrentDefaultBufferCapacity(), m_Instance.DefaultBufferBCapacity);

                // Now fill up Buffer B with new allocations
                while (m_Instance.GetCurrentDefaultBufferUsage() + allocationSize < m_Instance.GetCurrentDefaultBufferCapacity())
                {
                    handleQueue.Enqueue(AllocatePayloadAndValidate(allocationSize, false));
                    m_Instance.Update();
                    secondAllocationCount++;
                }

                // Allocate another larger payload, which should come from Overflow buffer and trigger another resize
                handleQueue.Enqueue(AllocatePayloadAndValidate(allocationSize * 3, true));

                // Update should trigger buffer resize
                m_Instance.Update();
                Assert.AreEqual(m_Instance.GetCurrentDefaultBufferCapacity(), initialCapacity * growthFactor * 2);

                // Additional updates should NOT increase buffer size
                for (int i = 0; i < (numSamples / 10); i++)
                {
                    m_Instance.Update();
                }
                Assert.AreEqual(m_Instance.GetCurrentDefaultBufferCapacity(), initialCapacity * growthFactor * 2);
            }
            finally
            {
                m_Instance.Shutdown();
            }
        }

        [TestCase(1024 * 100, 5, 500, false, Description = "Normal RingBuffer, Few payloads, Moderate payload size")]
        [TestCase(1024 * 100, 75, 500, false, Description = "Normal RingBuffer, Some payloads, Moderate payload size")]
        [TestCase(1024 * 100, 300, 500, false, Description = "Normal RingBuffer, Many payloads, Moderate payload size")]

        [TestCase(1024 * 10, 5, 2000, false, Description = "Small RingBuffer, Few payloads, Large payload size")]
        [TestCase(1024 * 10, 75, 10, false, Description = "Small RingBuffer, Some payloads, Tiny payload size")]
        [TestCase(1024 * 10, 200, 30, false, Description = "Small RingBuffer, Many payloads, Small payload size")]

        [TestCase(1024 * 10, 5, 5000, true, Description = "Small RingBuffer w/ Overflow, Few payloads, Huge payload size")]
        [TestCase(1024 * 10, 75, 500, true, Description = "Small RingBuffer w/ Overflow, Some payloads, Moderate payload size")]
        [TestCase(1024 * 10, 500, 50, true, Description = "Small RingBuffer w/ Overflow, Many payloads, Small payload size")]

        [TestCase(1024 * 2000, 1024, 2000, true, Description = "Huge RingBuffer w/ Overflow, Max payloads, Large payload size")]
        [TestCase(1024 * 4000, 1024, 6000, true, Description = "Huge RingBuffer w/ Overflow, Max payloads, Huge payload size")]
        [TestCase(1024 * 2000, 1024, 2000, false, Description = "Huge RingBuffer, Excessive Max, Large payload size")]
        public void ValidateDisjointedBufferAllocation(int initialCapacity, int numPayloads, int maxBufferSize, bool allowOverflow)
        {
            // NOTE: Need fixed seed value; allocation tolerances are very thin in some test cases and payload allocation can fail if get dice gimped
            const int randSeed = 100;

            // For each payload allocation, randomly select size based on maxBufferSize while ensuring it falls with min/max payload buffer constraints
            var rand = new Mathematics.Random(randSeed);
            var bufferSizes = new List<ushort>(numPayloads);
            for (int i = 0; i < numPayloads; i++)
            {
                var randSize = rand.NextUInt(UnsafePayloadRingBuffer.MinimumPayloadSize, (uint)maxBufferSize);
                randSize = math.min(UnsafePayloadRingBuffer.MaximumPayloadSize, randSize);
                bufferSizes.Add((ushort)randSize);
            }

            var parameters = MakeInitializationParameters(
                (uint)initialCapacity
            );
            if (!allowOverflow)
                parameters.OverflowBufferSize = 0;

            var handleList = new NativeList<PayloadHandle>(numPayloads, Allocator.Temp);
            m_Instance.Initialize(parameters);
            try
            {
                PayloadHandle headHandle;
                long totalBytesToAllocate = 0;

                // Select the method overload based on numPayloads: small FixedList, large FixedList, NativeList
                if (numPayloads < 10)
                {
                    var nativeList = new FixedList64Bytes<ushort>();
                    foreach (var value in bufferSizes)
                    {
                        totalBytesToAllocate += UnsafePayloadRingBuffer.RoundToNextAlign(value) + PayloadBlockHeader.HeaderSize;
                        nativeList.Add(value);
                    }
                    totalBytesToAllocate += UnsafePayloadRingBuffer.RoundToNextAlign((uint)(UnsafeUtility.SizeOf<PayloadHandle>() * nativeList.Length)) + PayloadBlockHeader.HeaderSize;

                    headHandle = m_Instance.AllocateDisjointedBuffer(ref nativeList, handleList);
                }
                else if (numPayloads < 100)
                {
                    var nativeList = new FixedList512Bytes<ushort>();
                    foreach (var value in bufferSizes)
                    {
                        totalBytesToAllocate += UnsafePayloadRingBuffer.RoundToNextAlign(value) + PayloadBlockHeader.HeaderSize;
                        nativeList.Add(value);
                    }
                    totalBytesToAllocate += UnsafePayloadRingBuffer.RoundToNextAlign((uint)(UnsafeUtility.SizeOf<PayloadHandle>() * nativeList.Length)) + PayloadBlockHeader.HeaderSize;

                    headHandle = m_Instance.AllocateDisjointedBuffer(ref nativeList, handleList);
                }
                else
                {
                    var nativeList = new NativeList<ushort>(numPayloads, Allocator.Temp);
                    try
                    {
                        foreach (var value in bufferSizes)
                        {
                            totalBytesToAllocate += UnsafePayloadRingBuffer.RoundToNextAlign(value) + PayloadBlockHeader.HeaderSize;
                            nativeList.Add(value);
                        }
                        totalBytesToAllocate += UnsafePayloadRingBuffer.RoundToNextAlign((uint)(UnsafeUtility.SizeOf<PayloadHandle>() * nativeList.Length)) + PayloadBlockHeader.HeaderSize;

                        headHandle = m_Instance.AllocateDisjointedBuffer(ref nativeList, handleList);
                    }
                    finally
                    {
                        nativeList.Dispose();
                    }
                }

                Assert.IsTrue(headHandle.IsValid);
                Assert.AreEqual(totalBytesToAllocate, m_Instance.DefaultBufferAUsage + m_Instance.OverflowBufferUsage);

                ValidateDisjointedBuffer(headHandle, bufferSizes, handleList);
                ValidateDisjointedBufferRelease(headHandle, handleList);
            }
            finally
            {
                m_Instance.Shutdown();
                handleList.Dispose();
            }
        }

        [TestCase(1024 * 100, 5, 500, false, Description = "Normal RingBuffer, Few payloads, Moderate payload size")]
        [TestCase(1024 * 100, 75, 500, false, Description = "Normal RingBuffer, Some payloads, Moderate payload size")]
        [TestCase(1024 * 100, 300, 500, false, Description = "Normal RingBuffer, Many payloads, Moderate payload size")]

        [TestCase(1024 * 10, 5, 2000, false, Description = "Small RingBuffer, Few payloads, Large payload size")]
        [TestCase(1024 * 10, 75, 10, false, Description = "Small RingBuffer, Some payloads, Tiny payload size")]
        [TestCase(1024 * 10, 255, 17, false, Description = "Small RingBuffer, Many payloads, Small payload size")]

        [TestCase(1024 * 10, 5, 5000, true, Description = "Small RingBuffer w/ Overflow, Few payloads, Huge payload size")]
        [TestCase(1024 * 10, 75, 500, true, Description = "Small RingBuffer w/ Overflow, Some payloads, Moderate payload size")]
        [TestCase(1024 * 10, 500, 50, true, Description = "Small RingBuffer w/ Overflow, Many payloads, Small payload size")]

        [TestCase(1024 * 2000, 1024, 2000, true, Description = "Huge RingBuffer w/ Overflow, Max payloads, Large payload size")]
        [TestCase(1024 * 4000, 1024, 6000, true, Description = "Huge RingBuffer w/ Overflow, Max payloads, Huge payload size")]
        [TestCase(1024 * 2000, 1024, 2000, false, Description = "Huge RingBuffer, Excessive Max, Large payload size")]
        public void ValidateDisjointedBufferCreationFromExistingPayloads(int initialCapacity, int numPayloads, int maxBufferSize, bool allowOverflow)
        {
            // NOTE: Need fixed seed value; allocation tolerances are very thin in some test cases and payload allocation can fail if get dice gimped
            const int randSeed = 163;

            var rand = new Mathematics.Random(randSeed);
            var parameters = MakeInitializationParameters(
                (uint)initialCapacity
            );
            if (!allowOverflow)
                parameters.OverflowBufferSize = 0;

            var handleList = new NativeList<PayloadHandle>(numPayloads, Allocator.Temp);
            var bufferSizes = new List<ushort>(numPayloads);
            m_Instance.Initialize(parameters);
            try
            {
                PayloadHandle headHandle;
                long totalBytesToAllocate = 0;

                for (int i = 0; i < numPayloads; i++)
                {
                    var randSize = rand.NextUInt(UnsafePayloadRingBuffer.MinimumPayloadSize, (uint)maxBufferSize);
                    randSize = math.min(UnsafePayloadRingBuffer.MaximumPayloadSize, randSize);
                    totalBytesToAllocate += UnsafePayloadRingBuffer.RoundToNextAlign(randSize) + PayloadBlockHeader.HeaderSize;

                    var handle = m_Instance.AllocatePayloadBuffer(randSize);
                    Assert.IsTrue(handle.IsValid);

                    handleList.Add(handle);
                    bufferSizes.Add((ushort)randSize);
                }
                totalBytesToAllocate += UnsafePayloadRingBuffer.RoundToNextAlign((uint)(UnsafeUtility.SizeOf<PayloadHandle>() * numPayloads)) + PayloadBlockHeader.HeaderSize;

                // Select the method overload based on numPayloads: small FixedList, large FixedList, NativeList
                if (numPayloads < 10)
                {
                    var paramList = new FixedList512Bytes<PayloadHandle>();
                    foreach (var h in handleList)
                        paramList.Add(h);

                    headHandle = m_Instance.CreateDisjointedPayloadBufferFromExistingPayloads(ref paramList);
                }
                else if (numPayloads < 100)
                {
                    var paramList = new FixedList4096Bytes<PayloadHandle>();
                    foreach (var h in handleList)
                        paramList.Add(h);

                    headHandle = m_Instance.CreateDisjointedPayloadBufferFromExistingPayloads(ref paramList);
                }
                else
                {
                    headHandle = m_Instance.CreateDisjointedPayloadBufferFromExistingPayloads(ref handleList);
                }

                Assert.IsTrue(headHandle.IsValid);
                Assert.AreEqual(totalBytesToAllocate, m_Instance.DefaultBufferAUsage + m_Instance.OverflowBufferUsage);

                ValidateDisjointedBuffer(headHandle, bufferSizes, handleList);
                ValidateDisjointedBufferRelease(headHandle, handleList);
            }
            finally
            {
                m_Instance.Shutdown();
                handleList.Dispose();
            }
        }

        public enum DisjointedAllocationFailScenarios
        {
            HeadAllocationFails,
            FirstPayloadAllocationFails,
            MiddlePayloadAllocationFails,
            TooManyPayloadsThrowsException,
            TooLargePayloadThrowsException,
            TooSmallPayloadThrowException,
        }

        [TestCase(DisjointedAllocationFailScenarios.HeadAllocationFails, Description = "Negative case: Head allocation fails")]
        [TestCase(DisjointedAllocationFailScenarios.FirstPayloadAllocationFails, Description = "Negative case: First payload allocation fails")]
        [TestCase(DisjointedAllocationFailScenarios.MiddlePayloadAllocationFails, Description = "Negative case: Middle payload allocation fails")]
        [TestCase(DisjointedAllocationFailScenarios.TooManyPayloadsThrowsException, Description = "Negative case: Too may Payloads throws exception")]
        [TestCase(DisjointedAllocationFailScenarios.TooLargePayloadThrowsException, Description = "Negative case: Too large Payload throws exception")]
        [TestCase(DisjointedAllocationFailScenarios.TooSmallPayloadThrowException, Description = "Negative case: Too small Payload throws exception")]
        public void ValidateDisjointedBufferAllocationFails(DisjointedAllocationFailScenarios scenario)
        {
#if !ENABLE_UNITY_COLLECTIONS_CHECKS
            // This test cases require 'ENABLE_UNITY_COLLECTIONS_CHECKS'
            if (scenario == DisjointedAllocationFailScenarios.TooSmallPayloadThrowException)
                return;
            if (scenario == DisjointedAllocationFailScenarios.TooLargePayloadThrowsException)
                return;
            if (scenario == DisjointedAllocationFailScenarios.TooManyPayloadsThrowsException)
                return;
#endif

            var parameters = MakeInitializationParameters(
                UnsafePayloadRingBuffer.MinimumCapacity
            );
            parameters.OverflowBufferSize = 0;
            parameters.DispatchQueueSize = 10000;

            const ushort BasicPayloadAllocSize = 10;
            const int Scenario2_NumAllocations = 16;

            m_Instance.Initialize(parameters);
            var payloadHandles = new NativeList<PayloadHandle>(Allocator.Temp);
            var nativeList = new NativeList<ushort>(4000, Allocator.Temp);

            Internal.Debug.SelfLog.SetMode(Internal.Debug.SelfLog.Mode.EnabledInMemory);
            using var scope = new Internal.Debug.SelfLog.Assert.TestScope(Allocator.Persistent);

            if (scenario != DisjointedAllocationFailScenarios.TooLargePayloadThrowsException &&
                scenario != DisjointedAllocationFailScenarios.TooSmallPayloadThrowException &&
                scenario != DisjointedAllocationFailScenarios.TooManyPayloadsThrowsException)
            {
                scope.ExpectingOutOfMemory();
            }

            try
            {
                switch (scenario)
                {
                    case DisjointedAllocationFailScenarios.HeadAllocationFails:
                        // Allocation of the "head" buffer should fail; too many payload handles for the RingBuffer's size
                        for (int i = 0; i < 255; i++)
                            nativeList.Add(BasicPayloadAllocSize);

                        break;

                    case DisjointedAllocationFailScenarios.FirstPayloadAllocationFails:
                        // Allocation of the 1st payload should fail; space remaining after head buffer is too small
                        nativeList.Add((ushort)UnsafePayloadRingBuffer.MaximumPayloadSize);
                        for (int i = 0; i < 100; i++)
                            nativeList.Add(BasicPayloadAllocSize);

                        break;

                    case DisjointedAllocationFailScenarios.MiddlePayloadAllocationFails:
                        // Allocation of a "middle" payload should fail; run out of space allocating payload blocks
                        for (int i = 0; i < Scenario2_NumAllocations; i++)
                            nativeList.Add(BasicPayloadAllocSize);

                        nativeList.Add((ushort)UnsafePayloadRingBuffer.MaximumPayloadSize);

                        for (int i = 0; i < Scenario2_NumAllocations; i++)
                            nativeList.Add(BasicPayloadAllocSize);
                        break;

                    case DisjointedAllocationFailScenarios.TooManyPayloadsThrowsException:
                        // Request too many payloads; throws exception when COLLECTION_CHECKS enabled
                        for (int i = 0; i < UnsafePayloadRingBuffer.MaximumDisjointedPayloadCount + 1; i++)
                            nativeList.Add(BasicPayloadAllocSize);

                        break;

                    case DisjointedAllocationFailScenarios.TooLargePayloadThrowsException:
                        // Request payload that exceeds max Payload size
                        nativeList.Add((ushort)UnsafePayloadRingBuffer.MaximumPayloadSize + 100);
                        break;

                    case DisjointedAllocationFailScenarios.TooSmallPayloadThrowException:
                        // Request payload that's below min Payload size (assumes MinimumPayloadSize > 0)
                        nativeList.Add((ushort)UnsafePayloadRingBuffer.MinimumPayloadSize - 1);
                        break;

                    default:
                        throw new System.ArgumentOutOfRangeException($"Invalid scenario number: {scenario}");
                }

                var exceptionThrown = false;

                var handle = new PayloadHandle();
                try
                {
                    handle = m_Instance.AllocateDisjointedBuffer(ref nativeList, payloadHandles);
                }
                catch (ArgumentOutOfRangeException)
                {
                    exceptionThrown = true;
                }

                switch (scenario)
                {
                    case DisjointedAllocationFailScenarios.TooManyPayloadsThrowsException:
                    case DisjointedAllocationFailScenarios.TooLargePayloadThrowsException:
                    case DisjointedAllocationFailScenarios.TooSmallPayloadThrowException:
                        Assert.IsTrue(exceptionThrown, "ArgumentOutOfRangeException wasn't thrown, but was expected");

                        break;
                }

                // Verify allocation failed and MemoryManager state matches scenario outcome
                // If any one of disjointed payload allocations fail, previously allocated payloads are released
                // but still occupy space in the RingBuffer until Update is called and memory is reclaimed.
                Assert.IsFalse(handle.IsValid, "handle should be invalid, but wasn't");
                Assert.Zero(payloadHandles.Length);

                var currUsage = m_Instance.GetCurrentDefaultBufferUsage();
                switch (scenario)
                {
                    case DisjointedAllocationFailScenarios.HeadAllocationFails:
                    case DisjointedAllocationFailScenarios.TooManyPayloadsThrowsException:
                    {
                        // Head buffer (1st allocation) failed and so none of the individual payload allocations occurred; expect RingBuffer to be empty
                        Assert.Zero(currUsage);
                        break;
                    }
                    case DisjointedAllocationFailScenarios.FirstPayloadAllocationFails:
                    {
                        // Head buffer was allocated but 1st payload allocation failed so none of the payload allocations occurred; expect only head buffer space to be consumed
                        var expectedSize = UnsafePayloadRingBuffer.RoundToNextAlign((uint)(nativeList.Length * UnsafeUtility.SizeOf<PayloadHandle>())) + PayloadBlockHeader.HeaderSize;
                        Assert.AreEqual(expectedSize, currUsage);
                        break;
                    }
                    case DisjointedAllocationFailScenarios.MiddlePayloadAllocationFails:
                    {
                        var expectedSize =  PayloadBlockHeader.HeaderSize + UnsafePayloadRingBuffer.RoundToNextAlign((uint)(nativeList.Length * UnsafeUtility.SizeOf<PayloadHandle>()));
                        expectedSize += (PayloadBlockHeader.HeaderSize + UnsafePayloadRingBuffer.RoundToNextAlign(BasicPayloadAllocSize)) * Scenario2_NumAllocations;

                        Assert.AreEqual(expectedSize, currUsage);
                        break;
                    }
                    case DisjointedAllocationFailScenarios.TooLargePayloadThrowsException:
                        // Result depend if COLLECTION_CHECKS are enabled or not:
                        // If enabled Exception thrown before anything is allocated (same as scenario 0)
                        // otherwise result is the same as scenario 1
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        Assert.Zero(currUsage);
#else
                        // Ignore this case: same result as scenario 1
#endif
                        break;
                }

                // Pump RingBuffer to reclaim allocated memory; usage should be 0 after this
                m_Instance.Update();
                Assert.Zero(m_Instance.GetCurrentDefaultBufferUsage());
            }
            finally
            {
                payloadHandles.Dispose();
                nativeList.Dispose();
                m_Instance.Shutdown();
            }
        }

        public enum DisjointedCreationFailScenarios
        {
            HeadAllocationFails,
            NoPayloadsFails,
            TooManyPayloadsThrowsException,
            InvalidPayloadThrowsException,
            DisjointedBufferAsPayloadThrowsException,
        }

        [BurstCompile]
        struct StressAllocationJob : IJobParallelFor
        {
            public int count;

            public void Execute(int index)
            {
                Run(count, UnsafePayloadRingBuffer.MinimumPayloadSize);
            }

            public static void Run(int n, uint packSize)
            {
                var handlesList = new NativeList<PayloadHandle>(n, Allocator.Temp);

                m_LogMemoryManagerInstance.Data.LockRead();
                try
                {
                    for (int j = 0; j < n; j++)
                    {
                        var h = m_LogMemoryManagerInstance.Data.AllocatePayloadBuffer(packSize);
                        handlesList.Add(h);
                    }

                    var hd = m_LogMemoryManagerInstance.Data.CreateDisjointedPayloadBufferFromExistingPayloads(ref handlesList);

                    m_LogMemoryManagerInstance.Data.ReleasePayloadBuffer(hd, out var result, false);
                }
                finally
                {
                    m_LogMemoryManagerInstance.Data.UnlockRead();
                }

                handlesList.Dispose();
            }
        }

        [BurstCompile]
        struct InstanceUpdateJob : IJob
        {
            public void Execute()
            {
                m_LogMemoryManagerInstance.Data.Update();
            }
        }

        public enum StressAllocationsScenarios
        {
            SingleThreaded,
            Multithreaded,
            MultithreadedDuringUpdate
        }

        [TestCase(StressAllocationsScenarios.SingleThreaded, Description = "Single threaded")]
        [TestCase(StressAllocationsScenarios.Multithreaded, Description = "Multithreaded")]
        [TestCase(StressAllocationsScenarios.MultithreadedDuringUpdate, Description = "Multithreaded during update")]
        public void StressAllocations(StressAllocationsScenarios mode)
        {
            const int parallelIterations = 1024 * 5;
            const int internalIterations = 1024 * 2;
            const uint packSize = UnsafePayloadRingBuffer.MinimumPayloadSize;
            const uint sizeForEverything = packSize * parallelIterations * internalIterations;

            // Use hard-coded values for grow/shrink thresholds and resize factors so changes in defaults doesn't break test
            const uint initialCapacity = sizeForEverything;
            const uint bufferSampleCount = 30;
            const float growThreshold = 0.70f;
            const float shrinkThreshold = 0.12f;
            const float growFactor = 2.0f;
            const float shrinkFactor = 0.75f;
            const uint overflowBufferSize = 0; // Overflow buffer is deliberately disabled

            var parameters = MakeInitializationParameters(
                initialCapacity,
                bufferSampleCount,
                growThreshold,
                shrinkThreshold,
                growFactor,
                shrinkFactor,
                overflowBufferSize);

            m_Instance.Initialize(parameters);

            var jobUpdate = new InstanceUpdateJob();

            try
            {
                for (var i = 0; i < parallelIterations; i++)
                {
                    if (mode == StressAllocationsScenarios.SingleThreaded)
                    {
                        StressAllocationJob.Run(internalIterations, packSize);
                        StressAllocationJob.Run(internalIterations, packSize);

                        //m_Instance.Update();
                        jobUpdate.Schedule().Complete();
                    }
                    else if (mode == StressAllocationsScenarios.Multithreaded)
                    {
                        var div = 32;

                        var job = new StressAllocationJob {count = internalIterations / div};
                        var handle = job.Schedule(div, 1);
                        StressAllocationJob.Run(internalIterations, packSize);
                        handle.Complete();

                        jobUpdate.Schedule().Complete();
                    }
                    else
                    {
                        var div = 32;

                        var job = new StressAllocationJob {count = internalIterations / div};

                        var handle = job.Schedule(div, 1);
                        var handleUpdate = jobUpdate.Schedule();
                        handle = job.Schedule(div, 1, handle);
                        handleUpdate = jobUpdate.Schedule(handleUpdate);

                        for (int k = 0; k < 32; ++k)
                            handleUpdate = jobUpdate.Schedule(handleUpdate);

                        handle.Complete();
                        handleUpdate.Complete();
                    }
                }
            }
            finally
            {
                m_Instance.Shutdown();
            }
        }

        [TestCase(DisjointedCreationFailScenarios.HeadAllocationFails, Description = "Negative case: Head allocation fails")]
        [TestCase(DisjointedCreationFailScenarios.NoPayloadsFails, Description = "Negative case: First payload allocation fails")]
        [TestCase(DisjointedCreationFailScenarios.TooManyPayloadsThrowsException, Description = "Negative case: Too may Payloads throws exception")]
        [TestCase(DisjointedCreationFailScenarios.InvalidPayloadThrowsException, Description = "Negative case: Invalid payload handle fails Disjointed buffer creation")]
        [TestCase(DisjointedCreationFailScenarios.DisjointedBufferAsPayloadThrowsException, Description = "Negative case: Use of Disjointed payload within another Disjointed buffer prohibited")]
        public void ValidateDisjointedBufferCreationFromExistingPayloadsFails(DisjointedCreationFailScenarios scenario)
        {
#if !ENABLE_UNITY_COLLECTIONS_CHECKS
            // This test case requires 'ENABLE_UNITY_COLLECTIONS_CHECKS'
            if (scenario == DisjointedCreationFailScenarios.InvalidPayloadThrowsException)
                return;
#endif


            uint capacity;
            switch (scenario)
            {
                case DisjointedCreationFailScenarios.TooManyPayloadsThrowsException:
                    capacity = 1024 * 100;
                    break;

                default:
                    capacity = UnsafePayloadRingBuffer.MinimumCapacity;
                    break;
            }

            var parameters = MakeInitializationParameters(
                capacity
            );
            parameters.OverflowBufferSize = 0;

            Internal.Debug.SelfLog.SetMode(Internal.Debug.SelfLog.Mode.EnabledInMemory);
            using var scope = new Internal.Debug.SelfLog.Assert.TestScope(Allocator.Persistent);

            m_Instance.Initialize(parameters);
            var handleList = new NativeList<PayloadHandle>(Allocator.Temp);
            try
            {
                switch (scenario)
                {
                    case DisjointedCreationFailScenarios.HeadAllocationFails:
                    {
                        // Fill up RingBuffer with Payload allocations, allocation of Disjointed head buffer should fail (no more space).
                        var trueSize = UnsafePayloadRingBuffer.RoundToNextAlign(UnsafePayloadRingBuffer.MinimumPayloadSize) + PayloadBlockHeader.HeaderSize;
                        var count = UnsafePayloadRingBuffer.MinimumCapacity / trueSize;
                        for (int i = 0; i < count; i++)
                        {
                            var handle = m_Instance.AllocatePayloadBuffer(UnsafePayloadRingBuffer.MinimumPayloadSize);
                            Assert.IsTrue(handle.IsValid);
                            handleList.Add(handle);
                        }

                        scope.ExpectingOutOfMemory();
                        var headHandle = m_Instance.CreateDisjointedPayloadBufferFromExistingPayloads(ref handleList);
                        Assert.IsFalse(headHandle.IsValid);

                        // Verify all pre-allocated payloads are still valid
                        foreach (var handle in handleList)
                        {
                            NativeArray<byte> data;
                            Assert.IsTrue(m_Instance.RetrievePayloadBuffer(handle, out data));
                        }
                        break;
                    }

                    case DisjointedCreationFailScenarios.NoPayloadsFails:
                    {
                        var headHandle = m_Instance.CreateDisjointedPayloadBufferFromExistingPayloads(ref handleList);
                        Assert.IsFalse(headHandle.IsValid);
                        break;
                    }

                    case DisjointedCreationFailScenarios.TooManyPayloadsThrowsException:
                    {
                        // Allocate a lot of small payloads, which exceeds maximum allowed causing an exception (when COLLECTION_CHECKS enabled)
                        var trueSize = UnsafePayloadRingBuffer.RoundToNextAlign(UnsafePayloadRingBuffer.MinimumPayloadSize) + PayloadBlockHeader.HeaderSize;
                        var count = m_Instance.Parameters.InitialBufferCapacity / trueSize;
                        var maxC = UnsafePayloadRingBuffer.MaximumDisjointedPayloadCount;
                        UnityEngine.Assertions.Assert.IsTrue(count > maxC, $"Test is not correct, max count is {maxC} but we're trying to allocate {count}, so exception won't hit");
                        for (int i = 0; i < count; i++)
                        {
                            var handle = m_Instance.AllocatePayloadBuffer(UnsafePayloadRingBuffer.MinimumPayloadSize);
                            Assert.IsTrue(handle.IsValid);
                            handleList.Add(handle);
                        }

                        PayloadHandle headHandle = new PayloadHandle();
                        bool exceptionThrow = true;
                        try
                        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            // Exception thrown only when COLLECTION_CHECKS enabled
                            exceptionThrow = false;
#endif
                            headHandle = m_Instance.CreateDisjointedPayloadBufferFromExistingPayloads(ref handleList);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            exceptionThrow = true;
                        }

                        Assert.IsFalse(headHandle.IsValid);
                        Assert.IsTrue(exceptionThrow);
                        break;
                    }

                    case DisjointedCreationFailScenarios.InvalidPayloadThrowsException:
                    {
                        // Allocate some payloads as normal but then release one of them; invalid handle should trigger exception
                        for (int i = 0; i < 10; i++)
                        {
                            var handle = m_Instance.AllocatePayloadBuffer(UnsafePayloadRingBuffer.MinimumPayloadSize);
                            Assert.IsTrue(handle.IsValid);
                            handleList.Add(handle);
                        }

                        PayloadReleaseResult result;
                        Assert.IsTrue(m_Instance.ReleasePayloadBuffer(handleList[3], out result));

                        PayloadHandle headHandle = new PayloadHandle();
                        bool exceptionThrow = true;
                        try
                        {
                            headHandle = m_Instance.CreateDisjointedPayloadBufferFromExistingPayloads(ref handleList);
                        }
                        catch (ArgumentException)
                        {
                            exceptionThrow = true;
                        }

                        Assert.IsFalse(headHandle.IsValid);
                        Assert.IsTrue(exceptionThrow);
                        break;
                    }

                    case DisjointedCreationFailScenarios.DisjointedBufferAsPayloadThrowsException:
                    {
                        // A Disjointed buffer cannot be used within another Disjointed buffer

                        var sizeList = new FixedList64Bytes<ushort> { 10, 15, 12 };
                        var firstDisJointHandle = m_Instance.AllocateDisjointedBuffer(ref sizeList);
                        Assert.IsTrue(firstDisJointHandle.IsValid);

                        for (int i = 0; i < 4; i++)
                        {
                            var handle = m_Instance.AllocatePayloadBuffer(UnsafePayloadRingBuffer.MinimumPayloadSize);
                            Assert.IsTrue(handle.IsValid);
                            handleList.Add(handle);
                        }
                        handleList.Add(firstDisJointHandle);

                        PayloadHandle headHandle = new PayloadHandle();
                        bool exceptionThrow = true;
                        try
                        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            // Exception thrown only when COLLECTION_CHECKS enabled
                            exceptionThrow = false;
#endif
                            headHandle = m_Instance.CreateDisjointedPayloadBufferFromExistingPayloads(ref handleList);
                        }
                        catch (ArgumentException)
                        {
                            exceptionThrow = true;
                        }

                        // NOTE: This check is only performed when COLLECTION_CHECKS enabled and will succeed if they're disabled
                        Assert.IsTrue(exceptionThrow);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        Assert.IsFalse(headHandle.IsValid);
#endif
                        break;
                    }
                }
            }
            finally
            {
                handleList.Dispose();
                m_Instance.Shutdown();
            }
        }

        private LogMemoryManagerParameters MakeInitializationParameters(
            uint initialBufferCapacity = LogMemoryManagerParameters.DefaultBufferCapacity,
            uint bufferSampleCount = LogMemoryManagerParameters.DefaultBufferSampleCount,
            float bufferGrowThreshold = LogMemoryManagerParameters.DefaultBufferGrowThreshold,
            float bufferShrinkThreshold = LogMemoryManagerParameters.DefaultBufferShrinkThreshold,
            float bufferGrowFactor = LogMemoryManagerParameters.DefaultBufferGrowFactor,
            float bufferShrinkFactor = LogMemoryManagerParameters.DefaultBufferShrinkFactor,
            uint overflowBufferSize = LogMemoryManagerParameters.DefaultOverflowBufferSize)
        {
            return new LogMemoryManagerParameters
            {
                InitialBufferCapacity = initialBufferCapacity,
                BufferSampleCount = bufferSampleCount,
                BufferGrowThreshold = bufferGrowThreshold,
                BufferShrinkThreshold = bufferShrinkThreshold,
                BufferGrowFactor = bufferGrowFactor,
                BufferShrinkFactor = bufferShrinkFactor,
                OverflowBufferSize = overflowBufferSize,
            };
        }

        private LogMemoryManagerParameters GetMinMaxInitializationParameterValues(bool maxvalues)
        {
            return new LogMemoryManagerParameters
            {
                InitialBufferCapacity = maxvalues ? UnsafePayloadRingBuffer.MaximumCapacity : UnsafePayloadRingBuffer.MinimumCapacity,
                BufferSampleCount = maxvalues ? LogMemoryManager.MaximumRingBufferSampleCount : 0,
                BufferGrowThreshold = maxvalues ? LogMemoryManager.MaximumRingBufferGrowThreshold : LogMemoryManager.MinimumRingBufferGrowThreshold,
                BufferShrinkThreshold = maxvalues ? LogMemoryManager.MaximumRingBufferShrinkThreshold : LogMemoryManager.MinimumRingBufferShrinkThreshold,
                BufferGrowFactor = maxvalues ? LogMemoryManager.MaximumRingBufferGrowFactor : LogMemoryManager.MinimumRingBufferGrowFactor,
                BufferShrinkFactor = maxvalues ? LogMemoryManager.MaximumRingBufferShrinkFactor : LogMemoryManager.MinimumRingBufferShrinkFactor,
                OverflowBufferSize = maxvalues ? UnsafePayloadRingBuffer.MaximumCapacity : UnsafePayloadRingBuffer.MinimumCapacity,
            };
        }

        private void ValidateInitializationParameters(LogMemoryManagerParameters initValues, LogMemoryManagerParameters expectedValues)
        {
            m_Instance.Shutdown();

            m_Instance.Initialize(initValues);
            try
            {
                var actualValues = m_Instance.Parameters;

                // If buffer resizing is completely disabled, BufferSampleCount is ignored (doesn't apply) and reset to 0
                bool resizeEnabled = true;
                if (expectedValues.BufferGrowThreshold == 0 && expectedValues.BufferShrinkThreshold == 0)
                {
                    expectedValues.BufferSampleCount = 0;
                    resizeEnabled = false;
                }

                Assert.AreEqual(expectedValues.InitialBufferCapacity, actualValues.InitialBufferCapacity);
                Assert.AreEqual(expectedValues.BufferSampleCount, actualValues.BufferSampleCount);
                Assert.AreEqual(expectedValues.BufferGrowThreshold, actualValues.BufferGrowThreshold);
                Assert.AreEqual(expectedValues.BufferShrinkThreshold, actualValues.BufferShrinkThreshold);
                Assert.AreEqual(expectedValues.BufferGrowFactor, actualValues.BufferGrowFactor);
                Assert.AreEqual(expectedValues.BufferShrinkFactor, actualValues.BufferShrinkFactor);
                Assert.AreEqual(expectedValues.OverflowBufferSize, actualValues.OverflowBufferSize);

                // Make sure RingBuffer is actually the size we specified
                Assert.AreEqual(expectedValues.InitialBufferCapacity, m_Instance.GetCurrentDefaultBufferCapacity());

                // If buffer resizing is disabled ensure all "moving average" properties are 0
                if (!resizeEnabled)
                {
                    Assert.Zero(m_Instance.MovingAverageSampleCount);
                    Assert.Zero(m_Instance.MovingAverageTotal);
                    Assert.IsFalse(m_Instance.MovingAverageCreated);
                    Assert.IsFalse(m_Instance.MovingAverageHasMaxSamples);
                }

                // Make sure Overflow buffer is enabled according to setting and size matches
                Assert.AreEqual(expectedValues.OverflowBufferSize != 0, m_Instance.IsOverflowEnabled);
                Assert.AreEqual(expectedValues.OverflowBufferSize, m_Instance.OverflowBufferCapacity);
            }
            finally
            {
                m_Instance.Shutdown();
            }
            Assert.AreEqual(m_Instance.Parameters, new LogMemoryManagerParameters());
        }

        private PayloadHandle AllocatePayloadAndValidate(uint allocationSize, bool expectOverflow = false)
        {
            var handle = m_Instance.AllocatePayloadBuffer(allocationSize);
            Assert.That(handle.IsValid);

            var bufferId = PayloadHandle.ExtractBufferIdFromHandle(ref handle);

            if (expectOverflow)
            {
                Assert.AreEqual(LogMemoryManager.OverflowBufferId, bufferId, "Payload wasn't allocated from Overflow buffer as expected");
            }
            else
            {
                Assert.AreNotEqual(LogMemoryManager.OverflowBufferId, bufferId, "Payload was allocated from Overflow buffer, which wasn't expected");
            }
            return handle;
        }

        private void ReleasePayloadAndValidate(PayloadHandle handle)
        {
            PayloadReleaseResult result;
            bool success = m_Instance.ReleasePayloadBuffer(handle, out result);
            Assert.That(success);
            Assert.That(result == PayloadReleaseResult.Success);
        }

        struct DisjointHandleState
        {
            public bool Valid;
            public bool Locked;
        }

        internal static void ValidatePayloadReleaseResults(PayloadHandle handle, in NativeList<PayloadHandle> disjointedHandles, bool force, bool expectSuccess, PayloadReleaseResult expectedResult)
        {
            bool isValid = m_Instance.IsPayloadHandleValid(handle);
            bool isLocked = m_Instance.IsPayloadBufferLocked(handle);

            int disjointLength = 0;
            if (disjointedHandles.IsCreated)
                disjointLength = disjointedHandles.Length;

            int numInvalidPayloads = 0;
            var disjointStates = new List<DisjointHandleState>(disjointLength);
            if (disjointedHandles.IsCreated)
            {
                foreach (var val in disjointedHandles)
                {
                    disjointStates.Add(new DisjointHandleState
                    {
                        Valid = m_Instance.IsPayloadHandleValid(val),
                        Locked = m_Instance.IsPayloadBufferLocked(val),
                    });

                    if (!m_Instance.IsPayloadHandleValid(val))
                        numInvalidPayloads++;
                }
            }

            var success = m_Instance.ReleasePayloadBuffer(handle, out var result, force);

            bool isStillValid = m_Instance.IsPayloadHandleValid(handle);
            bool isStillLocked = m_Instance.IsPayloadBufferLocked(handle);

            var disjointNewStates = new List<DisjointHandleState>(disjointLength);
            if (disjointedHandles.IsCreated)
            {
                foreach (var val in disjointedHandles)
                {
                    disjointNewStates.Add(new DisjointHandleState
                    {
                        Valid = m_Instance.IsPayloadHandleValid(val),
                        Locked = m_Instance.IsPayloadBufferLocked(val),
                    });
                }
            }

            // Validate the current state based on return values
            // NOTE: This isn't necessarily what we expected to happen, but make sure the state matches what is being reported
            switch (result)
            {
                case PayloadReleaseResult.Success:
                    Assert.IsTrue(isValid);
                    Assert.IsFalse(isLocked);
                    Assert.IsFalse(isStillValid);
                    Assert.IsFalse(isStillLocked);
                    break;

                case PayloadReleaseResult.ForcedRelease:
                    Assert.IsTrue(isValid);
                    Assert.That((isLocked || numInvalidPayloads > 0) && force, "ForcedRelease result is unexpected and needed: locked buffer or invalid Disjointed payload, and 'force' param set.");
                    Assert.IsFalse(isStillValid);
                    Assert.IsFalse(isStillLocked);
                    break;

                case PayloadReleaseResult.BufferLocked:
                    Assert.IsTrue(isValid);
                    Assert.IsTrue(isLocked);
                    Assert.IsTrue(isStillValid);
                    Assert.IsTrue(isStillLocked);
                    break;

                case PayloadReleaseResult.InvalidHandle:
                    Assert.IsFalse(isValid);
                    Assert.IsFalse(isLocked);
                    Assert.IsFalse(isStillValid);
                    Assert.IsFalse(isStillLocked);
                    break;

                case PayloadReleaseResult.DisjointedPayloadReleaseFailed:
                    Assert.IsTrue(isValid);
                    Assert.IsTrue(isStillValid);
                    Assert.IsTrue(handle.IsDisjointedBuffer);
                    Assert.NotZero(numInvalidPayloads, "DisjointedPayloadReleaseFailed is unexpected and needed at least 1 invalid Disjointed payload.");

                    // Locked state is tangential to this case; validate state didn't change
                    Assert.That(isLocked == isStillLocked);
                    break;

                default:
                    Assert.Fail("Unknown PayloadReleaseResult value! Test must be updated to handle the new case.");
                    break;
            }

            // Additional checks for Disjointed buffers
            if (handle.IsDisjointedBuffer)
            {
                Assert.That(disjointStates.Count == disjointNewStates.Count && disjointStates.Count > 0, "Test bug: Validation of Disjointed buffer should have passed in individual Payload handles");

                for (int i = 0; i < disjointStates.Count; i++)
                {
                    var oldState = disjointStates[i];
                    var newState = disjointNewStates[i];

                    switch (result)
                    {
                        // If success returned all Payloads were valid but have now been released
                        case PayloadReleaseResult.Success:
                            Assert.Zero(numInvalidPayloads);
                            Assert.IsTrue(oldState.Valid);
                            Assert.IsFalse(newState.Valid);
                            Assert.IsFalse(newState.Locked);
                            break;

                        // If locked or invalid handle then no change is state should have occurred
                        case PayloadReleaseResult.BufferLocked:
                        case PayloadReleaseResult.InvalidHandle:
                            Assert.That(oldState.Valid == newState.Valid);
                            Assert.That(oldState.Locked == newState.Locked);
                            break;

                        // Verify all originally valid payloads were released
                        case PayloadReleaseResult.ForcedRelease:
                        case PayloadReleaseResult.DisjointedPayloadReleaseFailed:

                            if (oldState.Valid)
                            {
                                Assert.IsFalse(newState.Valid);
                            }
                            Assert.IsFalse(newState.Locked);
                            break;

                        default:
                            Assert.Fail("Unknown PayloadReleaseResult value! Test must be updated to handle the new case.");
                            break;
                    }
                }
            }

            // Finally validate actual result matches expectations
            Assert.AreEqual(expectSuccess, success, "Payload wasn't successfully released as expected");
            Assert.AreEqual(expectedResult, result, "Payload release Result didn't match expected value");
        }

        private void CheckForBufferSwapAndValidateUsage(ref uint expectedUsage, ref bool expectBufferA)
        {
            // Check if we've switched to the other buffer (resizing)
            // Validate usage (from previous buffer) matches expectation
            if (expectBufferA != m_Instance.IsUsingBufferA)
            {
                var actualUsage = expectBufferA ? m_Instance.DefaultBufferAUsage : m_Instance.DefaultBufferBUsage;
                Assert.AreEqual(expectedUsage, actualUsage, "Allocated bytes from " + (expectBufferA ? "BufferA" : "BufferB") + " doesn't match");

                expectedUsage = expectBufferA ? m_Instance.DefaultBufferBUsage : m_Instance.DefaultBufferAUsage;
                expectBufferA = !expectBufferA;
            }
        }

        private unsafe void ValidateDisjointedBuffer(PayloadHandle headHandle, List<ushort> expectedSizes, in NativeList<PayloadHandle> payloadHandles)
        {
            const byte pattern1 = 0xA5;
            const byte pattern2 = 0x3C;

            // Checks to perform on DisjointedBuffer
            // - Size of buffers matches the expectedSizes
            // - Head buffer (referenced by handle) holds valid Handles for the content buffers
            // - Payloads referenced in Head buffer match buffers list
            // - Can successfully write to buffers

            // Sanity check parameters
            Assert.IsTrue(headHandle.IsValid);
            Assert.IsTrue(headHandle.IsDisjointedBuffer);
            Assert.NotZero(expectedSizes.Count);
            Assert.AreEqual(expectedSizes.Count, payloadHandles.Length, "Number of content payloads doesn't match request count");

            // Retrieve all the payload buffers
            var buffers = new List<NativeArray<byte>>(payloadHandles.Length);
            foreach (var handle in payloadHandles)
            {
                NativeArray<byte> payloadData;
                Assert.IsTrue(m_Instance.RetrievePayloadBuffer(handle, out payloadData));
                buffers.Add(payloadData);
            }

            for (int i = 0; i < expectedSizes.Count; i++)
            {
                Assert.AreEqual(expectedSizes[i], buffers[i].Length, "Content buffer size doesn't match the requested size");
            }

            NativeArray<byte> headBuffer;
            Assert.IsTrue(m_Instance.RetrievePayloadBuffer(headHandle, out headBuffer), "Failed to retrieve head buffer");
            Assert.AreEqual(buffers.Count, headBuffer.Length / UnsafeUtility.SizeOf<PayloadHandle>(), $"Head buffer isn't expected size: should be exactly PayloadHandle size: {UnsafeUtility.SizeOf<PayloadHandle>()} * number of buffers: {buffers.Count}");

            // Retrieve each content buffer and validate it matches corresponding buffers data
            var contentHandles = (PayloadHandle*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(headBuffer);
            for (int i = 0; i < buffers.Count; i++)
            {
                NativeArray<byte> payload;
                var handle = contentHandles[i];

                Assert.IsTrue(m_Instance.RetrievePayloadBuffer(handle, true, out payload), "Failed to retrieve content payload using handle read from head buffer");
                Assert.AreEqual(buffers[i].Length, payload.Length, "Length of Payload retrieved from head buffer doesn't match returned buffer length");

                var pPayload = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                var pBuffer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffers[i]);
                Assert.That(pBuffer == pPayload, "Payload pointer retrieved from head buffer doesn't match returned buffer pointer");

                // Write the index number to the first byte followed by an alternating bit pattern for the remainder of the buffer
                payload[0] = (byte)i;
                for (int b = 1; b < payload.Length; b++)
                {
                    payload[b] = (b % 2) == 0 ? pattern1 : pattern2;
                }
            }

            // Retrieve each Payload buffer again using LogMemoryManager API and verify data matches
            for (int i = 0; i < buffers.Count; i++)
            {
                NativeArray<byte> payload;

                Assert.IsTrue(m_Instance.RetrieveDisjointedPayloadBuffer(headHandle, i, out payload), "Failed to retrieve content payload via RetrieveDisjointedPayloadBuffer() API");
                Assert.AreEqual(buffers[i].Length, payload.Length, "Length of Payload retrieved via RetrieveDisjointedPayloadBuffer() API doesn't match returned buffer length");

                var pPayload = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                var pBuffer = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffers[i]);
                Assert.That(pBuffer == pPayload, "Payload pointer retrieved via RetrieveDisjointedPayloadBuffer() API doesn't match returned buffer pointer");

                Assert.AreEqual((byte)i, payload[0]);

                for (int b = 1; b < payload.Length; b++)
                {
                    var expectedValue = (b % 2) == 0 ? pattern1 : pattern2;
                    Assert.AreEqual(expectedValue, payload[b]);
                }
            }
        }

        private void ValidateDisjointedBufferRelease(PayloadHandle headHandle, in NativeArray<PayloadHandle> handleList)
        {
            // Release the Disjointed buffer and validate all content payloads are also released
            NativeArray<byte> payloadBuffer;
            PayloadReleaseResult result;
            Assert.IsTrue(m_Instance.ReleasePayloadBuffer(headHandle, out result, false), "Failed to release Disjointed buffer");
            Assert.AreEqual(PayloadReleaseResult.Success, result);
            Assert.IsFalse(m_Instance.RetrievePayloadBuffer(headHandle, out payloadBuffer), "Able to retrieve Disjointed buffer after it was released");
            Assert.Zero(payloadBuffer.Length);

            // Check each handle created that was part of the Disjointed buffer has also been released
            foreach (var handle in handleList)
            {
                Assert.IsFalse(m_Instance.RetrievePayloadBuffer(handle, out payloadBuffer), "Able to retrieve payload after it was released");
                Assert.Zero(payloadBuffer.Length);
                Assert.IsFalse(m_Instance.RetrieveDisjointedPayloadBuffer(headHandle, handleList.IndexOf(handle), out payloadBuffer), "Able to retrieve Disjointed payload after it was released");
                Assert.Zero(payloadBuffer.Length);
            }
        }
    }
}
