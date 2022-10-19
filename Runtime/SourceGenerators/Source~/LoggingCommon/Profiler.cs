using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace LoggingCommon
{
    /// <summary>
    /// Simple hierarchical profiler
    /// Used to track some performance of our code-generation suite
    /// </summary>
    public class Profiler
    {
        public class Auto : IDisposable
        {
            public Auto(string name)
            {
                Begin(name);
            }

            public void Dispose()
            {
                End();
            }
        }

        private class Marker
        {
            public int parent;
            public int id;
            public string name;
            public long overheadTicks;
            public long ticks;
            public int count;
            public int depth;
            public readonly List<int> children = new List<int>();
            public long totalTicks => ticks - overheadTicks;
        }
        public class Record
        {
            public long totalTime;
            public int count;
        }

        private readonly List<Marker> timers = new List<Marker>();
        private int currentId;

        private int ThreadId;

        //This is necessary since the instance is static but it can be called by multiple threads.
        private static readonly ThreadLocal<Profiler> _instance = new ThreadLocal<Profiler>(() => new Profiler());

        private static Profiler instance => _instance.Value;

        private static ConcurrentDictionary<int, Profiler> s_AllThreadProfilers = new ConcurrentDictionary<int, Profiler>();

        private Profiler()
        {
            RegisterThisThread();
        }

        private void RegisterThisThread()
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            s_AllThreadProfilers.TryRemove(threadId, out _);
            Init(threadId);
            s_AllThreadProfilers.TryAdd(threadId, this);
        }

        public static void Initialize()
        {
            s_AllThreadProfilers.Clear();
            instance.RegisterThisThread();
        }

        public static void Begin(string marker)
        {
            instance.Start(marker);
        }

        public static void End()
        {
            instance.Stop();
        }

        public static string PrintStats()
        {
            var builder = new StringBuilder();

            var arr = s_AllThreadProfilers.OrderBy(p => p.Key).Select(kv => kv.Value).ToArray();

            builder.AppendLine($"Registered Profilers ({arr.Length}):");

            foreach (var p in arr)
            {
                builder.AppendLine(p.CollectStats());
            }

            return builder.ToString();
        }

        private int GetChildId(string name)
        {
            foreach (var childId in timers[currentId].children)
            {
                if (timers[childId].name == name)
                    return childId;
            }

            return -1;
        }

        private void Init(int threadId)
        {
            ThreadId = threadId;
            timers.Clear();
            timers.Add(new Marker
            {
                parent = 0,
                id = 0,
                name = $"Total Thread #{ThreadId}",
                overheadTicks = 0,
                ticks = Stopwatch.GetTimestamp(),
                count = 1,
                depth = 0
            });
            currentId = 0;
        }

        private void Start(string name)
        {
            var t1 = Stopwatch.GetTimestamp();
            var childId = GetChildId(name);
            if (childId < 0)
            {
                var marker = new Marker
                {
                    name = name,
                    id = timers.Count,
                    parent = timers[currentId].id,
                    ticks = 0,
                    count = 0,
                    depth = timers[currentId].depth + 1
                };
                timers[currentId].children.Add(marker.id);
                timers.Add(marker);
                childId = marker.id;
            }
            var t2 = Stopwatch.GetTimestamp();
            ++timers[childId].count;
            timers[childId].ticks -= t2;
            timers[childId].overheadTicks += t2 - t1;
            currentId = childId;
        }

        private void Stop()
        {
            var marker = timers[currentId];
            marker.ticks += Stopwatch.GetTimestamp();
            currentId = marker.parent;
        }

        private string CollectStats()
        {
            while (currentId != 0)
                Stop();

            var t = Stopwatch.GetTimestamp();

            var root = timers[0];
            root.ticks = t - root.ticks;

            var builder = new StringBuilder();

            PrintChildrenSorted(builder);
            //PrintInCallOrder(builder);

            root.ticks = Stopwatch.GetTimestamp();
            return builder.ToString();
        }

        private void PrintInCallOrder(StringBuilder builder)
        {
            builder.AppendLine("Timing in call order:");

            //Timers is a tree stored in depth first order
            var totalTicks = timers[0].totalTicks;
            builder.AppendLine($"{timers[0].name}: {TicksToMsec(totalTicks)}");
            for (int i = 1; i < timers.Count; ++i)
            {
                var node = timers[i];
                var nodeTotalTicks = node.totalTicks;
                var s = $"{node.name}: {TicksToMsec(nodeTotalTicks)} ({node.count} calls) | {GetPercents(nodeTotalTicks, totalTicks)}";
                builder.AppendLine(s.PadLeft(node.depth * 2 + s.Length));
            }
        }

        private void PrintChildrenSorted(StringBuilder builder)
        {
            var root = timers[0];

            builder.AppendLine("Timing. Slowest first:");

            void PrintChildrenSortedRecursion(Marker m)
            {
                var s = $"{m.name}: {TicksToMsec(m.totalTicks)} ({m.count} calls) | {GetPercents(m.totalTicks, root.totalTicks)}";
                builder.AppendLine(s.PadLeft(m.depth * 2 + s.Length));

                var childrenIds = m.children;
                var childrenMarkers = childrenIds.Select(id => timers.First(t => t.id == id));
                var childrenMarkersSorted = childrenMarkers.OrderByDescending(m => m.totalTicks);
                foreach (var c in childrenMarkersSorted)
                    PrintChildrenSortedRecursion(c);
            }

            PrintChildrenSortedRecursion(root);

            builder.AppendLine("Timing end");
        }

        private static string TicksToMsec(long ticks)
        {
            var div = 1000.0 * ticks / (double)Stopwatch.Frequency;
            if (div < 0)
                div = 0;
            return $"{div} msec";
        }

        private static string GetPercents(long nodeTotalTicks, long totalTicks)
        {
            var div = nodeTotalTicks / (double)totalTicks;
            if (div < 0)
                div = 0;
            return $"{div * 100.0 :F3}%";
        }
    }
}
