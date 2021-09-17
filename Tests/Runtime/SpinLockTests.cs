using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Logging.Tests
{
    public class SpinLockTests
    {
#if USE_BASELIB
        const int iterLarge = 100000;
        const int iter = 10000;
#else
        const int iterLarge = 5000;
        const int iter = 3000;
#endif

        public enum ThreadMode
        {
            ForceSingleThread,
            MultiThreadTwoThreads,
            MultiThreadFull
        }

        void SpinLockMustBeUnmanaged<T>(T sl) where T : unmanaged
        {
        }

        [Test]
        [TestCase(ThreadMode.ForceSingleThread)]
        [TestCase(ThreadMode.MultiThreadTwoThreads)]
        [TestCase(ThreadMode.MultiThreadFull)]
        public void BurstSpinLockTest(ThreadMode mode)
        {
            var shaderString = "";
            ulong[] sharedULong = new ulong[128];
            using var l = new BurstSpinLock(Allocator.Persistent);

            var threadsToUse = 256;

            if (mode == ThreadMode.ForceSingleThread)
                threadsToUse = 1;
            else if (mode == ThreadMode.MultiThreadTwoThreads)
                threadsToUse = 2;

            Parallel.For(0, iterLarge, new ParallelOptions {MaxDegreeOfParallelism = threadsToUse}, (int i) =>
            {
                l.Enter();

                var k = 0;
                for (; k < sharedULong.Length / 2; k++)
                    sharedULong[k]++;

                shaderString = $"So this one is changed from {i} length {shaderString.Length}";

                for (; k < sharedULong.Length; k++)
                    sharedULong[k]++;

                l.Exit();
            });

            var sl = new SpinLockExclusive(Allocator.Persistent);
            SpinLockMustBeUnmanaged(sl);

            Parallel.For(0, iterLarge, new ParallelOptions {MaxDegreeOfParallelism = threadsToUse}, (int i) =>
            {
                sl.Lock();
                {
                    var k = 0;
                    for (; k < sharedULong.Length / 2; k++)
                        sharedULong[k]++;

                    shaderString = $"So this one is changed from {i} length {shaderString.Length}";

                    for (; k < sharedULong.Length; k++)
                        sharedULong[k]++;
                }
                sl.Unlock();
            });
            sl.Dispose();

            var sl2 = new SpinLockExclusive(Allocator.Persistent);

            const int n = 128;


            // make sure that even if we copy spin lock - it still works as expected
            var arr = new SpinLockExclusive[n];
            for (var i = 0; i < n; i++)
                arr[i] = sl2; // copy

            Parallel.For(0, iterLarge, new ParallelOptions {MaxDegreeOfParallelism = threadsToUse}, (int i) =>
            {
                ref var sl = ref arr[i % n];
                sl.Lock();
                {
                    var k = 0;
                    for (; k < sharedULong.Length / 2; k++)
                        sharedULong[k]++;

                    shaderString = $"So this one is changed from {i} length {shaderString.Length}";

                    for (; k < sharedULong.Length; k++)
                        sharedULong[k]++;
                }
                sl.Unlock();
            });
            sl2.Dispose();

            const int expected = iterLarge * 3;

            foreach (var t in sharedULong)
                UnityEngine.Assertions.Assert.AreEqual(expected, t);

            UnityEngine.Debug.Log(shaderString);
        }

        [Test]
        [TestCase(ThreadMode.ForceSingleThread)]
        [TestCase(ThreadMode.MultiThreadTwoThreads)]
        [TestCase(ThreadMode.MultiThreadFull)]
#if !UNITY_DOTSRUNTIME
        [Timeout(10000)]
#endif
        public void PerfTestBurstSpinLockTestRW(ThreadMode mode)
        {
            var sharedULong = new ulong[512];

            var threadsToUse = 256;

            if (mode == ThreadMode.ForceSingleThread)
                threadsToUse = 1;
            else if (mode == ThreadMode.MultiThreadTwoThreads)
                threadsToUse = 2;

            var sl = new SpinLockReadWrite(Allocator.Persistent);
            SpinLockMustBeUnmanaged(sl);

            const int n = 128;

            // make sure that even if we copy spin lock - it still works as expected
            var arr = new SpinLockReadWrite[n];
            for (var i = 0; i < n; i++)
                arr[i] = sl; // copy

            var sw = Stopwatch.StartNew();
            Parallel.For(0, iter, new ParallelOptions {MaxDegreeOfParallelism = threadsToUse}, (int i) =>
            {
                ref var l = ref arr[i % n];
                if (i % 5 == 0)
                {
                    using var spinlock = new SpinLockReadWrite.ScopedExclusiveLock(l);

                    for (int k = 0; k < 5; k++)
                    {
                        for (int j = 0; j < sharedULong.Length; j++)
                        {
                            sharedULong[j]++;
                        }
                    }
                }
                else
                {
                    using (var spinlock = new SpinLockReadWrite.ScopedReadLock(l))
                    {
                        for (int j = 0; j < sharedULong.Length; j++)
                        {
                            if (j == 42)
                                Thread.Sleep(1);

                            Assert.IsTrue(sharedULong[j] % 5 == 0);
                        }
                    }
                }
            });
            sw.Stop();

            sl.Dispose();

            const int expected = iter;

            foreach (var t in sharedULong)
                UnityEngine.Assertions.Assert.AreEqual(expected, t);

            UnityEngine.Debug.Log($"SpinLockReadWrite {sw.ElapsedMilliseconds} msec. {sw.ElapsedTicks} ticks");
        }

        [Test]
        [TestCase(ThreadMode.ForceSingleThread)]
        [TestCase(ThreadMode.MultiThreadTwoThreads)]
        [TestCase(ThreadMode.MultiThreadFull)]
#if !UNITY_DOTSRUNTIME
        [Timeout(10000)]
#endif
        public void PerfTestBurstSpinLockTestExclusive(ThreadMode mode)
        {
            var sharedULong = new ulong[512];

            var threadsToUse = 256;

            if (mode == ThreadMode.ForceSingleThread)
                threadsToUse = 1;
            else if (mode == ThreadMode.MultiThreadTwoThreads)
                threadsToUse = 2;

            var sl = new SpinLockExclusive(Allocator.Persistent);
            SpinLockMustBeUnmanaged(sl);

            const int n = 128;

            // make sure that even if we copy spin lock - it still works as expected
            var arr = new SpinLockExclusive[n];
            for (var i = 0; i < n; i++)
                arr[i] = sl; // copy

            var sw = Stopwatch.StartNew();
            Parallel.For(0, iter, new ParallelOptions {MaxDegreeOfParallelism = threadsToUse}, (int i) =>
            {
                ref var l = ref arr[i % n];
                if (i % 5 == 0)
                {
                    using var spinlock = new SpinLockExclusive.ScopedLock(l);

                    for (int k = 0; k < 5; k++)
                    {
                        for (int j = 0; j < sharedULong.Length; j++)
                        {
                            sharedULong[j]++;
                        }
                    }
                }
                else
                {
                    using var spinlock = new SpinLockExclusive.ScopedLock(l);

                    for (int j = 0; j < sharedULong.Length; j++)
                    {
                        if (j == 42)
                            Thread.Sleep(1);

                        Assert.IsTrue(sharedULong[j] % 5 == 0);
                    }
                }
            });
            sw.Stop();

            sl.Dispose();

            const int expected = iter;

            foreach (var t in sharedULong)
                UnityEngine.Assertions.Assert.AreEqual(expected, t);

            UnityEngine.Debug.Log($"SpinLockExclusive {sw.ElapsedMilliseconds} msec. {sw.ElapsedTicks} ticks");
        }

        [Test]
        [TestCase(ThreadMode.ForceSingleThread)]
        [TestCase(ThreadMode.MultiThreadTwoThreads)]
        [TestCase(ThreadMode.MultiThreadFull)]
        public void PerfTestReferenceDotsNetExclusive(ThreadMode mode)
        {
            var sharedULong = new ulong[512];

            var threadsToUse = 256;

            if (mode == ThreadMode.ForceSingleThread)
                threadsToUse = 1;
            else if (mode == ThreadMode.MultiThreadTwoThreads)
                threadsToUse = 2;

            var sl = new object();

            const int n = 128;

            var arr = new object[n];
            for (var i = 0; i < n; i++)
                arr[i] = sl; // copy

            var sw = Stopwatch.StartNew();
            Parallel.For(0, iter, new ParallelOptions {MaxDegreeOfParallelism = threadsToUse}, (int i) =>
            {
                ref var l = ref arr[i % n];
                if (i % 5 == 0)
                {
                    lock (l)
                    {
                        for (int k = 0; k < 5; k++)
                        {
                            for (int j = 0; j < sharedULong.Length; j++)
                            {
                                sharedULong[j]++;
                            }
                        }
                    }
                }
                else
                {
                    lock (l)
                    {
                        for (int j = 0; j < sharedULong.Length; j++)
                        {
                            if (j == 42)
                                Thread.Sleep(1);

                            Assert.IsTrue(sharedULong[j] % 5 == 0);
                        }
                    }
                }
            });
            sw.Stop();
            const int expected = iter;

            foreach (var t in sharedULong)
                UnityEngine.Assertions.Assert.AreEqual(expected, t);

            UnityEngine.Debug.Log($"lock {sw.ElapsedMilliseconds} msec. {sw.ElapsedTicks} ticks");
        }

        [Test]
        [TestCase(ThreadMode.ForceSingleThread)]
        [TestCase(ThreadMode.MultiThreadTwoThreads)]
        [TestCase(ThreadMode.MultiThreadFull)]
        public void PerfTestReferenceDotsNetReadWrite(ThreadMode mode)
        {
            var sharedULong = new ulong[512];

            var threadsToUse = 256;

            if (mode == ThreadMode.ForceSingleThread)
                threadsToUse = 1;
            else if (mode == ThreadMode.MultiThreadTwoThreads)
                threadsToUse = 2;

            var sl = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

            const int n = 128;

            var arr = new ReaderWriterLockSlim[n];
            for (var i = 0; i < n; i++)
                arr[i] = sl; // copy

            var sw = Stopwatch.StartNew();
            Parallel.For(0, iter, new ParallelOptions {MaxDegreeOfParallelism = threadsToUse}, (int i) =>
            {
                ref var l = ref arr[i % n];
                if (i % 5 == 0)
                {
                    l.EnterWriteLock();

                    for (int k = 0; k < 5; k++)
                    {
                        for (int j = 0; j < sharedULong.Length; j++)
                        {
                            sharedULong[j]++;
                        }
                    }

                    l.ExitWriteLock();
                }
                else
                {
                    l.EnterReadLock();

                    for (int j = 0; j < sharedULong.Length; j++)
                    {
                        if (j == 42)
                            Thread.Sleep(1);

                        Assert.IsTrue(sharedULong[j] % 5 == 0);
                    }

                    l.ExitReadLock();
                }
            });
            sw.Stop();

            const int expected = iter;

            foreach (var t in sharedULong)
                UnityEngine.Assertions.Assert.AreEqual(expected, t);

            UnityEngine.Debug.Log($"ReaderWriterLockSlim {sw.ElapsedMilliseconds} msec. {sw.ElapsedTicks} ticks");
        }
    }
}
