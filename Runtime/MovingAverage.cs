using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging
{
    /// <summary>
    /// Used by LogMemoryManager to monitor default PaylodRingBuffer utilization and increase/decrease capacity as needed.
    /// </summary>
    [BurstCompile]
    internal struct SimpleMovingAverage : IDisposable
    {
        private UnsafeRingQueue<UInt32>  m_Samples;
        private UInt32 m_MaxSamples;
        private UInt64 m_Total;
        private volatile float m_Average;

        public bool IsCreated => m_Samples.IsCreated;

        /// <summary>
        /// Returns true if a full complement of sample data has been accumulated.
        /// </summary>
        public bool HasMaximumSamples => m_Samples.Length == m_MaxSamples && IsCreated;

        /// <summary>
        /// Maximum number of samples considered in the rolling (moving) average.
        /// </summary>
        public UInt32 MaximumSamples => m_MaxSamples;

        /// <summary>
        /// Average of current sample values
        /// </summary>
        public float Average => m_Average;

        // Internal properties for testing
        internal int SampleCount => m_Samples.Length;
        internal UInt64 Total => m_Total;
        internal bool SampleQueueCreated => m_Samples.IsCreated;


        public SimpleMovingAverage(UInt32 maxSamples)
        {
            m_Samples = new UnsafeRingQueue<UInt32>((int)maxSamples, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_MaxSamples = maxSamples;
            m_Total = 0;
            m_Average = float.NaN;
        }

        public void Dispose()
        {
            if (m_Samples.IsCreated)
                m_Samples.Dispose();

            m_MaxSamples = 0;
            m_Total = 0;
            m_Average = float.NaN;
        }

        /// <summary>
        /// Adds a new sample value to the rolling average and computes new Average value.
        /// </summary>
        /// <remarks>
        /// While the Average property can be access safely from multiple threads (atomic field) it's
        /// not protected from race-conditions. If called from a different thread than AddSample, there's
        /// no guarantee Average will incorporate the latest sample value.
        /// </remarks>
        public void AddSample(UInt32 newValue)
        {
            if (HasMaximumSamples)
            {
                UInt32 oldValue;
                if (m_Samples.TryDequeue(out oldValue))
                {
                    m_Total -= oldValue;
                }
            }

            if (m_Samples.TryEnqueue(newValue))
            {
                m_Total += newValue;
            }

            if (m_Samples.Length > 0)
            {
                m_Average = (float)((double)m_Total / (double)m_Samples.Length);
            }
        }

        /// <summary>
        /// Clears all samples and resets Average.
        /// </summary>
        public void Flush()
        {
            // Remove all samples from the RingQueue
            // NOTE: Don't Dispose the queue, want to clear the values and not deallocate the memory
            UInt32 value;
            while (m_Samples.TryDequeue(out value)) {; }

            m_Total = 0;
            m_Average = float.NaN;
        }
    }
}
