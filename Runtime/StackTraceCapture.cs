using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Logging;
using Unity.Profiling;
using UnityEngine.Assertions;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// StackTraceCapture is a static class for capturing StackTraces in a deferred way. Means you can capture now and analyze afterwards.
    /// Contains a special case logic for different runtimes
    /// <seealso cref="ManagedStackTraceWrapper"/>
    /// </summary>
    public static class StackTraceCapture
    {
        static readonly ProfilerMarker k_StackTraceCapture = new ProfilerMarker($"StackTrace. Capture");
        static readonly ProfilerMarker k_StackTraceToString = new ProfilerMarker($"StackTrace. Convert to string");

        static readonly ProfilerMarker k_StackTraceSlowPathCapture = new ProfilerMarker($"StackTrace. Capture (slow path)");
        static readonly ProfilerMarker k_StackTraceSlowPathToString = new ProfilerMarker($"StackTrace. Convert to string  (slow path)");


        static StackTraceCapture()
        {
            var _ = ReflectionHelper.Initialized;
        }

        private static ConcurrentBag<StackTraceData> s_StackTracePool;

        private static StackTraceData AllocStackTraceData()
        {
            const int n = 32;
            s_StackTracePool ??= new ConcurrentBag<StackTraceData>();

            if (s_StackTracePool.IsEmpty)
                IncreasePool(n);

            return s_StackTracePool.TryTake(out var res) ? res : new StackTraceData();
        }

        public static void ReleaseStackTrace(StackTraceData d)
        {
            d.Reset();
            s_StackTracePool.Add(d);

            while (s_StackTracePool.Count > 256) // clear some memory
                s_StackTracePool.TryTake(out _);
        }

        private static void IncreasePool(int n)
        {
            for (var i = 0; i < n; i++)
            {
                s_StackTracePool.Add(new StackTraceData());
            }
        }

        public static StackTraceData GetStackTrace()
        {
            using var marker = k_StackTraceCapture.Auto();

            var data = AllocStackTraceData();
            data.Capture();

            return data;
        }

        public static void ToUnsafeTextStackTrace(StackTraceData data, ref UnsafeText result)
        {
            using var marker = k_StackTraceToString.Auto();

            if (StackTraceData.NeedFileInfo)
            {
                data.AppendToString(ref result);
            }
            else
#pragma warning disable 162
            {
                result.Append("<StackTrace not supported on this platform>");
            }
#pragma warning restore 162
        }

        /// <summary>
        /// Lightweight stack trace data structure. Can be analyzed afterwards
        /// </summary>
        public class StackTraceData
        {
            internal const bool NeedFileInfo = true;

            internal object Helper;
            private object[] m_Params;

            private readonly ReaderWriterLockSlim m_Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            private string m_String;

            public StackTraceData()
            {
                Reset();
            }

            public void Reset()
            {
                if (ReflectionHelper.Initialized)
                {
                    Helper = ReflectionHelper.Constructor.Invoke(new object[] { null });
                }
                else
                {
                    Helper = null;
                }

                m_Params = new[] { Helper, 0, NeedFileInfo, null };
                m_String = null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Capture()
            {
                if (ReflectionHelper.Initialized)
                {
                    // internal static extern void GetStackFramesInternal(StackFrameHelper sfh, int iSkip, bool fNeedFileInfo, Exception? e);
                    ReflectionHelper.GetStackFramesInternal.Invoke(null, m_Params);
                }
                else
                {
                    using var marker = k_StackTraceSlowPathCapture.Auto();

                    Helper = new StackTrace(0, NeedFileInfo);
                }
            }

            public void AppendToString(ref UnsafeText result)
            {
                // 1. We never called this - need to lock and create internal string
                // 2. We called this - the internal string is creating in some other thread
                // 3. We can just use the internal string.

                m_Lock.EnterUpgradeableReadLock();
                try
                {
                    if (m_String == null) // Case #1
                    {
                        m_Lock.EnterWriteLock();
                        try
                        {
                            if (m_String == null) // check again, another thread could just did this. Case #2
                            {
                                var analyzer = new StackTraceDataAnalyzer();
                                m_String = analyzer.ToString(this);
                            }
                        }
                        finally
                        {
                            m_Lock.ExitWriteLock();
                        }
                    }

                    Assert.IsNotNull(m_String);
                    result.Append(m_String); // case #3 (#1 and #2 would also end up in here)
                }
                finally
                {
                    m_Lock.ExitUpgradeableReadLock();
                }
            }
        }

        internal struct StackTraceDataAnalyzer
        {
            private int m_FrameCount;
            private object m_Helper;

            private string[] m_RgFilenameArr;
            private int[] m_RgiIlOffsetArr;
            private int[] m_RgiLineNumberArr;

            [ThreadStatic]
            private static int s_Reentrancy;

            bool Analyze(object dataHelper)
            {
                // Check if this function is being reentered because of an exception in the code below
                if (s_Reentrancy > 0)
                    return false;

                m_Helper = dataHelper;

                m_FrameCount = (int)ReflectionHelper.GetNumberOfFrames.Invoke(m_Helper, null);
                var rgiMethodTokenArr = (int[])ReflectionHelper.RgiMethodToken.GetValue(m_Helper);
                var rgAssemblyPathArr = (string[])ReflectionHelper.RgAssemblyPath.GetValue(m_Helper);
                var rgLoadedPeAddressArr = (IntPtr[])ReflectionHelper.RgLoadedPeAddress.GetValue(m_Helper);
                var rgiLoadedPeSizeArr = (int[])ReflectionHelper.RgiLoadedPeSize.GetValue(m_Helper);
                var rgInMemoryPdbAddressArr = (IntPtr[])ReflectionHelper.RgInMemoryPdbAddress.GetValue(m_Helper);

                var rgiInMemoryPdbSizeArr = (int[])ReflectionHelper.RgiInMemoryPdbSize.GetValue(m_Helper);
                m_RgiIlOffsetArr = (int[])ReflectionHelper.RgiIlOffset.GetValue(m_Helper);
                m_RgFilenameArr = (string[])ReflectionHelper.RgFilename.GetValue(m_Helper);
                m_RgiLineNumberArr = (int[])ReflectionHelper.RgiLineNumber.GetValue(m_Helper);
                var rgiColumnNumberArr = (int[])ReflectionHelper.RgiColumnNumber.GetValue(m_Helper);

                s_Reentrancy++;
                try
                {
                    if (ReflectionHelper.GetSourceLineInfo != null)
                    {
                        var rgAssemblyArr = (object[])ReflectionHelper.RgAssembly.GetValue(m_Helper);

                        for (var index = 0; index < m_FrameCount; index++)
                        {
                            // If there was some reason not to try get the symbols from the portable PDB reader like the module was
                            // ENC or the source/line info was already retrieved, the method token is 0.
                            if (rgiMethodTokenArr[index] != 0)
                            {
                                ReflectionHelper.GetSourceLineInfo((Assembly)rgAssemblyArr[index],
                                                                   rgAssemblyPathArr[index],
                                                                   rgLoadedPeAddressArr[index],
                                                                   rgiLoadedPeSizeArr[index],
                                                                   rgInMemoryPdbAddressArr[index],
                                                                   rgiInMemoryPdbSizeArr[index],
                                                                   rgiMethodTokenArr[index],
                                                                   m_RgiIlOffsetArr[index],
                                                                   out m_RgFilenameArr[index],
                                                                   out m_RgiLineNumberArr[index],
                                                                   out rgiColumnNumberArr[index]);
                            }
                        }
                    }
                    else if (ReflectionHelper.GetSourceLineInfoNoAssembly != null)
                    {
                        for (var index = 0; index < m_FrameCount; index++)
                        {
                            // If there was some reason not to try get the symbols from the portable PDB reader like the module was
                            // ENC or the source/line info was already retrieved, the method token is 0.
                            if (rgiMethodTokenArr[index] != 0)
                            {
                                ReflectionHelper.GetSourceLineInfoNoAssembly(rgAssemblyPathArr[index],
                                                                             rgLoadedPeAddressArr[index],
                                                                             rgiLoadedPeSizeArr[index],
                                                                             rgInMemoryPdbAddressArr[index],
                                                                             rgiInMemoryPdbSizeArr[index],
                                                                             rgiMethodTokenArr[index],
                                                                             m_RgiIlOffsetArr[index],
                                                                             out m_RgFilenameArr[index],
                                                                             out m_RgiLineNumberArr[index],
                                                                             out rgiColumnNumberArr[index]);
                            }
                        }
                    }
                }
                catch
                {
                    return false;
                }
                finally
                {
                    s_Reentrancy--;
                }

                return true;
            }

            private int GetNumberOfFrames()
            {
                return m_FrameCount;
            }

            private MethodBase GetMethodBase(int i)
            {
                return (MethodBase)ReflectionHelper.GetMethodBase.Invoke(m_Helper, new object[] { i });
            }

            [ThreadStatic]
            private static StringBuilder s_StringBuilder;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int CutLastIndexOf(string s, string substring)
            {
                var indx = s.LastIndexOf(substring, StringComparison.Ordinal);
                if (indx != -1)
                    indx += substring.Length;

                return indx;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int CutIndexOf(string s, string substring, int startIndex = 0)
            {
                var indx = s.IndexOf(substring, startIndex, StringComparison.Ordinal);
                if (indx != -1)
                    indx += substring.Length;

                return indx;
            }

            // Unfortunately we cannot tell how much frames we should skip - on different platforms we have different count + in case of burst enabled we have wrong function names sometimes
            internal static string CutStackTrace(string stacktraceText)
            {
                const string loggingCall = "Unity.Logging.Log.";
                var indx = CutLastIndexOf(stacktraceText, loggingCall);
                if (indx == -1)
                {
                    indx = CutIndexOf(stacktraceText, ManagedStackTraceWrapper.CaptureStackTraceFuncName2);

                    var indx2 = CutIndexOf(stacktraceText, ManagedStackTraceWrapper.CaptureStackTraceFuncName1, indx == -1 ? 0 : indx);
                    if (indx2 != -1)
                        indx = indx2;
                }

                if (indx != -1)
                {
                    var indxNewLine = CutIndexOf(stacktraceText, "\n", indx);
                    if (indxNewLine != -1)
                        indx = indxNewLine;

                    stacktraceText = stacktraceText.Substring(indx);
                }

                return stacktraceText;
            }

            public string ToString(StackTraceData data)
            {
                if (ReflectionHelper.Initialized == false)
                {
                    using var marker = k_StackTraceSlowPathToString.Auto();

                    // slow stacktrace in data.Helper
                    if (data.Helper is StackTrace slowStackTrace)
                    {
                        var stacktraceText = slowStackTrace.ToString();

                        return CutStackTrace(stacktraceText);
                    }

                    return "<No stacktrace>";
                }

                if (Analyze(data.Helper) == false)
                    return "<Failed to analyze the stacktrace>";

                if (s_StringBuilder == null)
                    s_StringBuilder = new StringBuilder(1024);
                else
                    s_StringBuilder.Clear();

                var displayFilenames = StackTraceData.NeedFileInfo;
                var fFirstFrame = true;
                var n = GetNumberOfFrames();
                for (var i = 0; i < n; i++)
                {
                    var mb = GetMethodBase(i);

                    if (mb != null)
                    {
                        // We want a newline at the end of every line except for the last
                        if (fFirstFrame)
                            fFirstFrame = false;
                        else
                            s_StringBuilder.Append(Environment.NewLine);

                        s_StringBuilder.Append((FixedString32Bytes)"   at ");

                        var t = mb.DeclaringType;
                        // if there is a type (non global method) print it
                        if (t != null)
                        {
                            s_StringBuilder.Append(t.FullName.Replace('+', '.'));
                            s_StringBuilder.Append('.');
                        }

                        s_StringBuilder.Append(mb.Name);

                        // deal with the generic portion of the method
                        if (mb is MethodInfo info && info.IsGenericMethod)
                        {
                            s_StringBuilder.Append('[');

                            var fFirstTyParam = true;
                            foreach (var type in info.GetGenericArguments())
                            {
                                if (fFirstTyParam == false)
                                    s_StringBuilder.Append(',');
                                else
                                    fFirstTyParam = false;

                                s_StringBuilder.Append(type.Name);
                            }

                            s_StringBuilder.Append(']');
                        }

                        // arguments printing
                        s_StringBuilder.Append('(');
                        var fFirstParam = true;
                        foreach (var t1 in mb.GetParameters())
                        {
                            if (fFirstParam == false)
                                s_StringBuilder.Append(", ");
                            else
                                fFirstParam = false;

                            var typeName = t1.ParameterType.Name;
                            s_StringBuilder.Append(typeName + " " + t1.Name);
                        }

                        s_StringBuilder.Append(')');

                        // source location printing
                        if (displayFilenames && m_RgiIlOffsetArr[i] != -1)
                        {
                            // If we don't have a PDB or PDB-reading is disabled for the module,
                            // then the file name will be null.
                            string fileName = null;

                            // Getting the filename from a StackFrame is a privileged operation - we won't want
                            // to disclose full path names to arbitrarily untrusted code.  Rather than just omit
                            // this we could probably trim to just the filename so it's still mostly useful.
                            try
                            {
                                fileName = m_RgFilenameArr[i];
                            }
                            catch (SecurityException)
                            {
                                // If the demand for displaying filenames fails, then it won't
                                // succeed later in the loop.  Avoid repeated exceptions by not trying again.
                                displayFilenames = false;
                            }

                            if (fileName != null)
                            {
                                s_StringBuilder.Append(" in ");
                                s_StringBuilder.Append(fileName);
                                s_StringBuilder.Append(":line ");
                                s_StringBuilder.Append(m_RgiLineNumberArr[i]);
                            }
                        }
                    }
                }

                return s_StringBuilder.ToString();
            }
        }

        private static class ReflectionHelper
        {
            public static readonly ConstructorInfo Constructor;

            internal static readonly MethodInfo GetNumberOfFrames;
            internal static readonly MethodInfo GetMethodBase;
            internal static readonly MethodInfo GetStackFramesInternal;

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate void GetSourceLineInfoDelegate(Assembly assembly, string assemblyPath, IntPtr loadedPeAddress,
                                                             int loadedPeSize, IntPtr inMemoryPdbAddress, int inMemoryPdbSize, int methodToken, int ilOffset,
                                                             out string sourceFile, out int sourceLine, out int sourceColumn);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate void GetSourceLineInfoDelegateNoAssembly(string assemblyPath, IntPtr loadedPeAddress,
                                                                       int loadedPeSize, IntPtr inMemoryPdbAddress, int inMemoryPdbSize, int methodToken, int ilOffset,
                                                                       out string sourceFile, out int sourceLine, out int sourceColumn);

            internal static GetSourceLineInfoDelegate GetSourceLineInfo;
            internal static GetSourceLineInfoDelegateNoAssembly GetSourceLineInfoNoAssembly;
            internal static readonly FieldInfo RgiMethodToken;
            internal static readonly FieldInfo RgAssemblyPath;
            internal static readonly FieldInfo RgiLoadedPeSize;
            internal static readonly FieldInfo RgAssembly;
            internal static readonly FieldInfo RgLoadedPeAddress;
            internal static readonly FieldInfo RgInMemoryPdbAddress;
            internal static readonly FieldInfo RgiInMemoryPdbSize;
            internal static readonly FieldInfo RgiIlOffset;
            internal static readonly FieldInfo RgFilename;
            internal static readonly FieldInfo RgiLineNumber;
            internal static readonly FieldInfo RgiColumnNumber;

            public static bool Initialized { get; }

            static ReflectionHelper()
            {
                try
                {
                    var stackFrameHelperType = typeof(object).Assembly.GetType("System.Diagnostics.StackFrameHelper");
                    Constructor = stackFrameHelperType.GetConstructor(new[] { typeof(Thread) });

                    GetNumberOfFrames = stackFrameHelperType.GetMethod("GetNumberOfFrames", BindingFlags.Instance | BindingFlags.Public);
                    GetMethodBase = stackFrameHelperType.GetMethod("GetMethodBase", BindingFlags.Instance | BindingFlags.Public);

                    RgiMethodToken = stackFrameHelperType.GetField("rgiMethodToken", BindingFlags.Instance | BindingFlags.NonPublic);
                    RgAssemblyPath = stackFrameHelperType.GetField("rgAssemblyPath", BindingFlags.Instance | BindingFlags.NonPublic);
                    RgAssembly = stackFrameHelperType.GetField("rgAssembly", BindingFlags.Instance | BindingFlags.NonPublic);
                    RgLoadedPeAddress = stackFrameHelperType.GetField("rgLoadedPeAddress", BindingFlags.Instance | BindingFlags.NonPublic);
                    RgiLoadedPeSize = stackFrameHelperType.GetField("rgiLoadedPeSize", BindingFlags.Instance | BindingFlags.NonPublic);
                    RgInMemoryPdbAddress = stackFrameHelperType.GetField("rgInMemoryPdbAddress", BindingFlags.Instance | BindingFlags.NonPublic);
                    RgiInMemoryPdbSize = stackFrameHelperType.GetField("rgiInMemoryPdbSize", BindingFlags.Instance | BindingFlags.NonPublic);
                    RgiIlOffset = stackFrameHelperType.GetField("rgiILOffset", BindingFlags.Instance | BindingFlags.NonPublic);
                    RgFilename = stackFrameHelperType.GetField("rgFilename", BindingFlags.Instance | BindingFlags.NonPublic);
                    RgiLineNumber = stackFrameHelperType.GetField("rgiLineNumber", BindingFlags.Instance | BindingFlags.NonPublic);
                    RgiColumnNumber = stackFrameHelperType.GetField("rgiColumnNumber", BindingFlags.Instance | BindingFlags.NonPublic);

                    GetStackFramesInternal = typeof(StackTrace).GetMethod("GetStackFramesInternal", BindingFlags.Static | BindingFlags.NonPublic);

                    Initialized = TryGetNetCoreDelegate() || TryGetNetStandardDelegate();
                }
                catch
                {
                    Initialized = false;
                }
            }

            private static bool TryGetNetStandardDelegate()
            {
                try
                {
                    Type type = Type.GetType("System.Diagnostics.StackTraceSymbols, System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", false);

                    MethodInfo method = type.GetMethod("GetSourceLineInfoWithoutCasAssert", new Type[10]
                    {
                        typeof(string),
                        typeof(IntPtr),
                        typeof(int),
                        typeof(IntPtr),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(string).MakeByRefType(),
                        typeof(int).MakeByRefType(),
                        typeof(int).MakeByRefType()
                    });

                    if (method == null)
                        method = type.GetMethod("GetSourceLineInfo", new Type[10]
                        {
                            typeof(string),
                            typeof(IntPtr),
                            typeof(int),
                            typeof(IntPtr),
                            typeof(int),
                            typeof(int),
                            typeof(int),
                            typeof(string).MakeByRefType(),
                            typeof(int).MakeByRefType(),
                            typeof(int).MakeByRefType()
                        });

                    object instance = Activator.CreateInstance(type);
                    GetSourceLineInfoNoAssembly = (GetSourceLineInfoDelegateNoAssembly)method.CreateDelegate(typeof(GetSourceLineInfoDelegateNoAssembly), instance);


                }
                catch
                {
                    return false;
                }

                return true;
            }

            private static bool TryGetNetCoreDelegate()
            {
                try
                {
                    var symbolsType = Type.GetType("System.Diagnostics.StackTraceSymbols, System.Diagnostics.StackTrace", throwOnError: false);

                    var parameterTypes = new[]
                    {
                        typeof(Assembly), typeof(string), typeof(IntPtr), typeof(int), typeof(IntPtr),
                        typeof(int), typeof(int), typeof(int),
                        typeof(string).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int).MakeByRefType()
                    };

                    var symbolsMethodInfo = symbolsType.GetMethod("GetSourceLineInfo", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, parameterTypes, null);

                    // Create an instance of System.Diagnostics.Stacktrace.Symbols
                    var target = Activator.CreateInstance(symbolsType);

                    // Create an instance delegate for the GetSourceLineInfo method
                    GetSourceLineInfo = (GetSourceLineInfoDelegate)symbolsMethodInfo.CreateDelegate(typeof(GetSourceLineInfoDelegate), target);
                }
                catch
                {
                    return false;
                }

                return true;
            }
        }
    }
}
