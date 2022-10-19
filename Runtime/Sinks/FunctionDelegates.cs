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

    /// <summary>
    /// Struct that wraps OnBeforeSinkDelegate for a sink
    /// </summary>
    public unsafe readonly struct OnBeforeSinkDelegate
    {
        /// <summary>
        /// Delegate that describes the function pointer
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(IntPtr userData);

        private readonly FunctionPointer<Delegate> m_FunctionPointer;

        /// <summary>
        /// True if function pointer was created
        /// </summary>
        public bool IsCreated => m_FunctionPointer.IsCreated;

        /// <summary>
        /// Constructor that takes the delegate and compiles it with burst
        /// </summary>
        /// <param name="func">Delegate to compile</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OnBeforeSinkDelegate(Delegate func)
        {
            FunctionDelegates.MustHaveAttributes<Delegate>(func);
            m_FunctionPointer = BurstCompiler.CompileFunctionPointer(func);
        }

        /// <summary>
        /// Invoke the compiled delegate
        /// </summary>
        /// <param name="p">UserData</param>
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

    /// <summary>
    /// Struct that wraps OnAfterSinkDelegate for a sink
    /// </summary>
    public unsafe readonly struct OnAfterSinkDelegate
    {
        /// <summary>
        /// Delegate that describes the function pointer
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(IntPtr userData);

        private readonly FunctionPointer<Delegate> m_FunctionPointer;

        /// <summary>
        /// True if function pointer was created
        /// </summary>
        public bool IsCreated => m_FunctionPointer.IsCreated;

        /// <summary>
        /// Constructor that takes the delegate and compiles it with burst
        /// </summary>
        /// <param name="func">Delegate to compile</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OnAfterSinkDelegate(Delegate func)
        {
            FunctionDelegates.MustHaveAttributes<Delegate>(func);
            m_FunctionPointer = BurstCompiler.CompileFunctionPointer(func);
        }

        /// <summary>
        /// Invoke the compiled delegate
        /// </summary>
        /// <param name="p">UserData</param>
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

    /// <summary>
    /// Struct that wraps OnDisposeDelegate for a sink
    /// </summary>
    public unsafe readonly struct OnDisposeDelegate
    {
        /// <summary>
        /// Delegate that describes the function pointer
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(IntPtr userData);

        private readonly FunctionPointer<Delegate> m_FunctionPointer;

        /// <summary>
        /// True if function pointer was created
        /// </summary>
        public bool IsCreated => m_FunctionPointer.IsCreated;

        /// <summary>
        /// Constructor that takes the delegate and compiles it with burst
        /// </summary>
        /// <param name="func">Delegate to compile</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OnDisposeDelegate(Delegate func)
        {
            FunctionDelegates.MustHaveAttributes<Delegate>(func);
            m_FunctionPointer = BurstCompiler.CompileFunctionPointer(func);
        }

        /// <summary>
        /// Invoke the compiled delegate
        /// </summary>
        /// <param name="p">UserData</param>
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

    /// <summary>
    /// Struct that wraps OnLogMessageEmitDelegate for a sink
    /// </summary>
    public unsafe readonly struct OnLogMessageEmitDelegate
    {
        /// <summary>
        /// Delegate that describes the function pointer
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(in LogMessage logEvent, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator);

        private readonly FunctionPointer<Delegate> m_FunctionPointer;

        /// <summary>
        /// True if function pointer was created
        /// </summary>
        public bool IsCreated => m_FunctionPointer.IsCreated;

        /// <summary>
        /// Constructor that takes the delegate and compiles it with burst
        /// </summary>
        /// <param name="func">Delegate to compile</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OnLogMessageEmitDelegate(Delegate func)
        {
            FunctionDelegates.MustHaveAttributes<Delegate>(func);
            m_FunctionPointer = BurstCompiler.CompileFunctionPointer(func);
        }

        /// <summary>
        /// Invoke the delegate that emits the message converted to <see cref="UnsafeText"/>
        /// </summary>
        /// <param name="logEvent">Log message event</param>
        /// <param name="outTemplate">Template that sink is using</param>
        /// <param name="messageBuffer">Text representation of the message</param>
        /// <param name="memoryManager">Memory manager that owns Log message</param>
        /// <param name="userData">User data</param>
        /// <param name="allocator">Allocator that should be used in case of any allocation needed</param>
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

    /// <summary>
    /// Struct that wraps OnLogMessageFormatterDelegate for a sink
    /// </summary>
    public readonly struct OnLogMessageFormatterDelegate
    {
        /// <summary>
        /// Delegate that describes the function pointer
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int Delegate(in LogMessage logEvent, ref FormatterStruct formatter, ref FixedString512Bytes outTemplate, ref UnsafeText messageBuffer, IntPtr memoryManager, IntPtr userData, Allocator allocator);

        private readonly FunctionPointer<Delegate> m_FunctionPointer;

        /// <summary>
        /// True if function pointer was created
        /// </summary>
        public bool IsCreated => m_FunctionPointer.IsCreated;

        /// <summary>
        /// Constructor that takes the delegate and compiles it with burst
        /// </summary>
        /// <param name="func">Delegate to compile</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OnLogMessageFormatterDelegate(Delegate func)
        {
            FunctionDelegates.MustHaveAttributes<Delegate>(func);
            m_FunctionPointer = BurstCompiler.CompileFunctionPointer(func);
        }

        /// <summary>
        /// Invoke the compiled delegate to convert message into <see cref="UnsafeText"/>
        /// </summary>
        /// <param name="logEvent">Log message event</param>
        /// <param name="formatter">Formatter that sink is using</param>
        /// <param name="outTemplate">Template that sink is using</param>
        /// <param name="messageBuffer">Text representation of the message</param>
        /// <param name="memoryManager">Memory manager that owns Log message</param>
        /// <param name="userData">User data</param>
        /// <param name="allocator">Allocator that should be used in case of any allocation needed</param>
        /// <returns>Length of the messageOutput. Negative on error</returns>
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
