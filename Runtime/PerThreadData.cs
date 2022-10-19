using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Baselib.LowLevel;
using Unity.IL2CPP.CompilerServices;
using Unity.Logging.Internal;

namespace Unity.Logging
{
    /// <summary>
    /// Class that is used by logging to save some per-thread data for logging management. Usually stores thread's LoggerHandle that is currently used
    /// </summary>
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public static class PerThreadData
    {
        struct PerThreadDataKey {}

        private static readonly SharedStatic<UIntPtr> s_LoggerHandleTls = SharedStatic<UIntPtr>.GetOrCreate<LoggerHandle, PerThreadDataKey>(16);
        private static readonly SharedStatic<byte> s_Initialized = SharedStatic<byte>.GetOrCreate<byte, PerThreadDataKey>(16);

        static PerThreadData()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize static data
        /// </summary>
        [BurstDiscard]
        public static void Initialize()
        {
            if (s_Initialized.Data == 0)
            {
                s_LoggerHandleTls.Data = Binding.Baselib_TLS_Alloc();
                s_Initialized.Data = 1;
            }
        }

        private static void Shutdown()
        {
            if (s_Initialized.Data != 0)
            {
                Binding.Baselib_TLS_Free(s_LoggerHandleTls.Data);
                s_Initialized.Data = 0;
            }
        }

        /// <summary>
        /// Reset all per thread data
        /// </summary>
        public static void Reset()
        {
            Shutdown();
            Initialize();
        }

        /// <summary>
        /// Current LoggerHandle in this thread. Used internally by mirror struct's implicit constructors
        /// </summary>
        public static LoggerHandle ThreadLoggerHandle
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfNotInitialized();
                return LoggerHandle.CreateUsingKnownId((uint)Binding.Baselib_TLS_Get(s_LoggerHandleTls.Data));
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                ThrowIfNotInitialized();
                ThrowIfSetIsValid(value);
                Binding.Baselib_TLS_Set(s_LoggerHandleTls.Data, (UIntPtr)value.Value);
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfNotInitialized()
        {
            if (s_Initialized.Data == 0)
                throw new Exception("PerThreadData.Initialize() was not called!");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowIfSetIsValid(LoggerHandle newHandle)
        {
            if (newHandle.IsValid && Binding.Baselib_TLS_Get(s_LoggerHandleTls.Data) != UIntPtr.Zero) // both are valid
            {
                throw new Exception("ThreadLoggerHandle overrides a valid LoggerHandle, this usually means you forgot to reset it or you have a race condition.");
            }
        }
    }
}
