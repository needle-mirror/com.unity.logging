#if UNITY_DOTSRUNTIME || UNITY_2021_2_OR_NEWER
#define LOGGING_USE_UNMANAGED_DELEGATES
#endif

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Logging.Sinks
{
    internal static class FunctionDelegates
    {
        [Conditional("UNITY_EDITOR")]
        public static void MustHaveAttributes<T>(T funcT)
        {
            var func = funcT as Delegate;
            if (func == null)
            {
                UnityEngine.Debug.LogError($"MustHaveAttributes should be called with Delegate as an parameter!");
                throw new ArgumentException();
            }

            var attributes = func.Method.GetCustomAttributes(false);

            var foundAOT = false;
            var foundBurst = false;
            foreach (var a in attributes)
            {
                if (foundAOT == false && a.GetType().Name == "MonoPInvokeCallbackAttribute")
                    foundAOT = true;

                if (foundBurst == false && a.GetType().Name == "BurstCompileAttribute")
                    foundBurst = true;
            }

            if (foundAOT == false)
            {
                UnityEngine.Debug.LogError($"The method `{func.Method}` must have `MonoPInvokeCallback` attribute to be compatible with IL2CPP!");
            }

            if (foundBurst == false)
            {
                UnityEngine.Debug.LogError($"The method `{func.Method}` must have `BurstCompile` attribute to be used as a sink delegate!");
            }
        }
    }

    public unsafe readonly struct OnBeforeSinkDelegate
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(IntPtr userData);

        private readonly FunctionPointer<Delegate> m_FunctionPointer;
        public bool IsCreated => m_FunctionPointer.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OnBeforeSinkDelegate(Delegate func)
        {
            FunctionDelegates.MustHaveAttributes<Delegate>(func);
            m_FunctionPointer = BurstCompiler.CompileFunctionPointer(func);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(IntPtr p)
        {
#if LOGGING_USE_UNMANAGED_DELEGATES
            ((delegate * unmanaged[Cdecl] <IntPtr, void>)m_FunctionPointer.Value)(p);
#else
            m_FunctionPointer.Invoke(p);
#endif
        }
    }

    public unsafe readonly struct OnAfterSinkDelegate
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(IntPtr userData);

        private readonly FunctionPointer<Delegate> m_FunctionPointer;
        public bool IsCreated => m_FunctionPointer.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OnAfterSinkDelegate(Delegate func)
        {
            FunctionDelegates.MustHaveAttributes<Delegate>(func);
            m_FunctionPointer = BurstCompiler.CompileFunctionPointer(func);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(IntPtr p)
        {
#if LOGGING_USE_UNMANAGED_DELEGATES
            ((delegate * unmanaged[Cdecl] <IntPtr, void>)m_FunctionPointer.Value)(p);
#else
            m_FunctionPointer.Invoke(p);
#endif
        }
    }

    public unsafe readonly struct OnDisposeDelegate
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(IntPtr userData);

        private readonly FunctionPointer<Delegate> m_FunctionPointer;
        public bool IsCreated => m_FunctionPointer.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OnDisposeDelegate(Delegate func)
        {
            FunctionDelegates.MustHaveAttributes<Delegate>(func);
            m_FunctionPointer = BurstCompiler.CompileFunctionPointer(func);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(IntPtr p)
        {
#if LOGGING_USE_UNMANAGED_DELEGATES
            ((delegate * unmanaged[Cdecl] <IntPtr, void>)m_FunctionPointer.Value)(p);
#else
            m_FunctionPointer.Invoke(p);
#endif
        }
    }

    public unsafe readonly struct OnLogMessageEmitDelegate
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(in LogMessage logEvent, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator);

        private readonly FunctionPointer<Delegate> m_FunctionPointer;
        public bool IsCreated => m_FunctionPointer.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OnLogMessageEmitDelegate(Delegate func)
        {
            FunctionDelegates.MustHaveAttributes<Delegate>(func);
            m_FunctionPointer = BurstCompiler.CompileFunctionPointer(func);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(in LogMessage logEvent, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator)
        {
#if LOGGING_USE_UNMANAGED_DELEGATES
            ((delegate * unmanaged[Cdecl] <in LogMessage, ref FixedString512Bytes, ref UnsafeText, IntPtr, IntPtr, Allocator, void>)m_FunctionPointer.Value)
                (in logEvent, ref outTemplate, ref messageBuffer, memoryManager, userData, allocator);
#else
            m_FunctionPointer.Invoke(in logEvent, ref outTemplate, ref messageBuffer, memoryManager, userData, allocator);
#endif
        }
    }

    public readonly struct OnLogMessageFormatterDelegate
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int Delegate(in LogMessage logEvent, ref FormatterStruct formatter, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator);

        private readonly FunctionPointer<Delegate> m_FunctionPointer;
        public bool IsCreated => m_FunctionPointer.IsCreated;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OnLogMessageFormatterDelegate(Delegate func)
        {
            FunctionDelegates.MustHaveAttributes<Delegate>(func);
            m_FunctionPointer = BurstCompiler.CompileFunctionPointer(func);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(in LogMessage logEvent, ref FormatterStruct formatter, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator)
        {
            if (m_FunctionPointer.IsCreated == false) return 0;

#if LOGGING_USE_UNMANAGED_DELEGATES
            unsafe
            {
                return ((delegate * unmanaged[Cdecl] <in LogMessage, ref FormatterStruct, ref FixedString512Bytes, ref UnsafeText, IntPtr, IntPtr, Allocator, int>)m_FunctionPointer.Value)
                    (in logEvent, ref formatter, ref outTemplate, ref messageBuffer, memoryManager, userData, allocator);
            }
#else
            return m_FunctionPointer.Invoke(in logEvent, ref formatter, ref outTemplate, ref messageBuffer, memoryManager, userData, allocator);
#endif
        }
    }
}
