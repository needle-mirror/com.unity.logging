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
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Logging;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Logging.Internal
{
    /// <summary>
    /// StackTraceCapture is a static class for capturing StackTraces in a deferred way. Means you can capture now and analyze afterwards.
    /// Contains a special case logic for different runtimes
    /// <seealso cref="ManagedStackTraceWrapper"/>
    /// </summary>
    [HideInStackTrace]
    public static class StackTraceCapture
    {
        static readonly ProfilerMarker k_StackTraceCapture = new ProfilerMarker($"StackTrace. Capture");
        static readonly ProfilerMarker k_StackTraceToString = new ProfilerMarker($"StackTrace. Convert to string");

        static readonly ProfilerMarker k_StackTraceSlowPathCapture = new ProfilerMarker($"StackTrace. Capture (slow path)");
        static readonly ProfilerMarker k_StackTraceSlowPathToString = new ProfilerMarker($"StackTrace. Convert to string  (slow path)");

        /// <summary>
        /// Initialize. Called internally by logging
        /// </summary>
        [BurstDiscard]
        internal static void Initialize()
        {
            ReflectionHelper.Initialize();
            StackTraceDataAnalyzer.GetProjectFolder(); // init current folder
        }

        static StackTraceCapture()
        {
            Initialize();
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

        internal static void ReleaseStackTrace(StackTraceData d)
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

        [HideInStackTrace]
        internal static StackTraceData GetStackTrace()
        {
            using var marker = k_StackTraceCapture.Auto();

            var data = AllocStackTraceData();
            data.Capture();

            return data;
        }

        internal static void ToUnsafeTextStackTrace(StackTraceData data, ref UnsafeText result)
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
        internal class StackTraceData
        {
            internal const bool NeedFileInfo = true;

            internal object Helper;
            private object[] m_Params;

            private readonly ReaderWriterLockSlim m_Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            private byte[] m_StringUTF8;

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
                m_StringUTF8 = null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [HideInStackTrace]
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

                    Helper = new SlowStackTraceWrapper();
                }
            }

            public void AppendToString(ref UnsafeText result)
            {
                // 1. We never called this - need to lock and create internal string
                // 2. We called this - the internal string is creating in some other thread
                // 3. We can just use the internal string.

                try
                {
                    m_Lock.EnterUpgradeableReadLock();
                    if (m_StringUTF8 == null) // Case #1
                    {
                        try
                        {
                            m_Lock.EnterWriteLock();
                            if (m_StringUTF8 == null) // check again, another thread could just did this. Case #2
                            {
                                var analyzer = new StackTraceDataAnalyzer();
                                var str = analyzer.ToString(this);

                                m_StringUTF8 = Encoding.UTF8.GetBytes(str);
                            }
                        }
                        finally
                        {
                            m_Lock.ExitWriteLock();
                        }
                    }

                    if (m_StringUTF8 != null && m_StringUTF8.Length > 0)
                    {
                        unsafe
                        {
                            fixed (byte* ptr = &m_StringUTF8[0])
                            {
                                result.Append(ptr, m_StringUTF8.Length); // case #3 (#1 and #2 would also end up in here)
                            }
                        }
                    }
                }
                finally
                {
                    m_Lock.ExitUpgradeableReadLock();
                }
            }
        }

        internal struct StackTraceDataAnalyzer : StackTraceDataAnalyzer.IStackTrace
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

            internal interface IStackTrace
            {
                int FrameCount { get; }
                MethodBase GetMethodForFrame(int frameIndex);
                string GetFileNameForFrame(int frameIndex);
                int GetFileLineNumberForFrame(int frameIndex);
            }

            private static string s_ProjectFolder = "";
            internal static string GetProjectFolder()
            {
                if (string.IsNullOrEmpty(s_ProjectFolder))
                {
                    // A string that contains the absolute path of the current working directory, and does not end with a backslash (\).
                    s_ProjectFolder = System.IO.Directory.GetCurrentDirectory();
                    s_ProjectFolder = s_ProjectFolder.Replace("\\", "/");
                    s_ProjectFolder += "/";
                }

                return s_ProjectFolder;
            }

            enum HideOption
            {
                NotHide = 0,
                HideOnlyThis,
                HideEverythingInside
            }
            static HideOption ShouldHideInStackTrace(IEnumerable<Attribute> attributes)
            {
                foreach (var customAttribute in attributes)
                {
                    if (customAttribute is HideInStackTrace hideAttr)
                    {
                        if (hideAttr.HideEverythingInside)
                            return HideOption.HideEverythingInside;
                        return HideOption.HideOnlyThis;
                    }

                    var attributeName = customAttribute.GetType().Name;
                    if (attributeName.IndexOf("HideInCallstack", StringComparison.OrdinalIgnoreCase) != -1 ||
                        attributeName.IndexOf("HideInConsole", StringComparison.OrdinalIgnoreCase) != -1 ||
                        attributeName.IndexOf("StackTraceHidden", StringComparison.OrdinalIgnoreCase) != -1) // https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.stacktracehiddenattribute?view=net-6.0
                    {
                        return HideOption.HideOnlyThis;
                    }
                }

                return HideOption.NotHide;
            }

            internal static void FormatStackTrace<T>(T stackTrace, StringBuilder sb) where T : IStackTrace
            {
                // need to skip over "n" frames which represent the
                // System.Diagnostics package frames
                var iIndexCount = stackTrace.FrameCount;
                for (var iIndex = 0; iIndex < iIndexCount; iIndex++)
                {
                    var mb = stackTrace.GetMethodForFrame(iIndex);
                    if (mb == null)
                        continue;

                    var hideOption = ShouldHideInStackTrace(mb.GetCustomAttributes());
                    switch (hideOption)
                    {
                        case HideOption.HideEverythingInside:
                            sb.Clear();
                            continue;
                        case HideOption.HideOnlyThis:
                            continue;
                    }

                    Type classType = mb.DeclaringType;
                    if (classType == null)
                        continue;

                    hideOption = ShouldHideInStackTrace(classType.GetCustomAttributes());
                    switch (hideOption)
                    {
                        case HideOption.HideEverythingInside:
                            sb.Clear();
                            continue;
                        case HideOption.HideOnlyThis:
                            continue;
                    }

                    if (classType.DeclaringType != null)
                    {
                        hideOption = ShouldHideInStackTrace(classType.DeclaringType.GetCustomAttributes());
                        switch (hideOption)
                        {
                            case HideOption.HideEverythingInside:
                                sb.Clear();

                                continue;
                            case HideOption.HideOnlyThis:
                                continue;
                        }
                    }

                    // Add namespace.classname:MethodName
                    String ns = classType.Namespace;
                    if (!string.IsNullOrEmpty(ns))
                    {
                        sb.Append(ns);
                        sb.Append(".");
                    }

                    sb.Append(classType.Name);
                    sb.Append(":");
                    sb.Append(mb.Name);
                    sb.Append("(");

                    // Add parameters
                    int j = 0;
                    ParameterInfo[] pi = mb.GetParameters();
                    bool fFirstParam = true;
                    while (j < pi.Length)
                    {
                        if (fFirstParam == false)
                            sb.Append(", ");
                        else
                            fFirstParam = false;

                        sb.Append(pi[j].ParameterType.Name);
                        j++;
                    }
                    sb.Append(")");

                    // Add path name and line number - unless it is a Debug.Log call, then we are only interested
                    // in the calling frame.
                    string path = stackTrace.GetFileNameForFrame(iIndex);
                    if (path != null)
                    {
                        bool shouldStripLineNumbers =
                            (classType.Name == "Debug" && classType.Namespace == "UnityEngine") ||
                            (classType.Name == "Logger" && classType.Namespace == "UnityEngine") ||
                            (classType.Name == "DebugLogHandler" && classType.Namespace == "UnityEngine") ||
                            (classType.Name == "Assert" && classType.Namespace == "UnityEngine.Assertions") ||
                            (mb.Name == "print" && classType.Name == "MonoBehaviour" && classType.Namespace == "UnityEngine")
                        ;

                        if (!shouldStripLineNumbers)
                        {
                            sb.Append(" (at ");

                            var projectFolder = GetProjectFolder();
                            if (!string.IsNullOrEmpty(projectFolder))
                            {
                                if (path.Replace("\\", "/").StartsWith(projectFolder))
                                {
                                    path = path.Substring(projectFolder.Length, path.Length - projectFolder.Length);
                                }
                            }

                            sb.Append(path);
                            sb.Append(":");
                            sb.Append(stackTrace.GetFileLineNumberForFrame(iIndex));
                            sb.Append(")");
                        }
                    }

                    sb.Append(Environment.NewLine);
                }
            }

            public string ToString(StackTraceData data)
            {
                if (s_StringBuilder == null)
                    s_StringBuilder = new StringBuilder(1024);
                else
                    s_StringBuilder.Clear();

                if (ReflectionHelper.Initialized == false)
                {
                    using var marker = k_StackTraceSlowPathToString.Auto();

                    // slow stacktrace in data.Helper
                    if (data.Helper is SlowStackTraceWrapper slowStackTrace)
                    {
                        FormatStackTrace(slowStackTrace, s_StringBuilder);
                        return s_StringBuilder.ToString();
                    }

                    return "<No stacktrace>";
                }

                if (Analyze(data.Helper) == false)
                    return "<Failed to analyze the stacktrace>";

                FormatStackTrace(this, s_StringBuilder);
                return s_StringBuilder.ToString();
            }

            public int FrameCount => m_FrameCount;

            public MethodBase GetMethodForFrame(int frameIndex)
            {
                return GetMethodBase(frameIndex);
            }

            public string GetFileNameForFrame(int frameIndex)
            {
                // Getting the filename from a StackFrame is a privileged operation - we won't want
                // to disclose full path names to arbitrarily untrusted code.  Rather than just omit
                // this we could probably trim to just the filename so it's still mostly useful.
                try
                {
                    return m_RgFilenameArr[frameIndex];
                }
                catch (SecurityException)
                {
                    // If the demand for displaying filenames fails, then it won't
                    // succeed later in the loop.  Avoid repeated exceptions by not trying again.
                    return "???";
                }
            }

            public int GetFileLineNumberForFrame(int frameIndex)
            {
                return m_RgiLineNumberArr[frameIndex];
            }
        }

        private static class ReflectionHelper
        {
            public static ConstructorInfo Constructor;

            internal static MethodInfo GetNumberOfFrames;
            internal static MethodInfo GetMethodBase;
            internal static MethodInfo GetStackFramesInternal;

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
            internal static FieldInfo RgiMethodToken;
            internal static FieldInfo RgAssemblyPath;
            internal static FieldInfo RgiLoadedPeSize;
            internal static FieldInfo RgAssembly;
            internal static FieldInfo RgLoadedPeAddress;
            internal static FieldInfo RgInMemoryPdbAddress;
            internal static FieldInfo RgiInMemoryPdbSize;
            internal static FieldInfo RgiIlOffset;
            internal static FieldInfo RgFilename;
            internal static FieldInfo RgiLineNumber;
            internal static FieldInfo RgiColumnNumber;

            public static bool Initialized => s_InitState == InitState.Success;

            enum InitState : byte
            {
                NeverInitialized,
                FailedInitialization,
                Success
            }
            private static InitState s_InitState = InitState.NeverInitialized;

            [BurstDiscard]
            internal static void Initialize()
            {
                if (s_InitState != InitState.NeverInitialized) return;

                s_InitState = InitState.FailedInitialization;

                try
                {
                    var stackFrameHelperType = typeof(object).Assembly.GetType("System.Diagnostics.StackFrameHelper");
                    if (stackFrameHelperType != null)
                    {
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

                        if (TryGetNetCoreDelegate() || TryGetNetStandardDelegate())
                        {
                            s_InitState = InitState.Success;
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }

            static ReflectionHelper()
            {
                Initialize();
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

    internal class SlowStackTraceWrapper : StackTrace, StackTraceCapture.StackTraceDataAnalyzer.IStackTrace
    {
        public SlowStackTraceWrapper() : base(1, true)
        {
        }

        public MethodBase GetMethodForFrame(int frameIndex)
        {
            return GetFrame(frameIndex).GetMethod();
        }

        public string GetFileNameForFrame(int frameIndex)
        {
            return GetFrame(frameIndex).GetFileName();
        }

        public int GetFileLineNumberForFrame(int frameIndex)
        {
            return GetFrame(frameIndex).GetFileLineNumber();
        }
    }
}
