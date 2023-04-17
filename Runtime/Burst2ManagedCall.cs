using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Debug = UnityEngine.Debug;

namespace Unity.Logging
{
    /// <summary>
    /// Helper functions for Burst
    /// </summary>
    [BurstCompile]
    public static class BurstHelper
    {
        /// <summary>
        /// Checks if a method is called from a managed or Burst environment.
        /// </summary>
        /// <returns>
        /// Returns true if method is called from a managed, not Burst environment
        /// </returns>
        public static bool IsManaged
        {
            get
            {
                [BurstDiscard]
                // ReSharper disable once RedundantAssignment
                static void BurstTest(ref bool changeInBurst) { changeInBurst = true; }
                var isManaged = false; BurstTest(ref isManaged); return isManaged;
            }
        }

        /// <summary>
        /// Checks if a method is called from a managed or Burst environment.
        /// </summary>
        /// <returns>
        /// Returns true if method is called from Burst, not a managed environment
        /// </returns>
        public static bool IsBurst => IsManaged == false;

        /// <summary>
        /// Calls Debug.Log that prints if this is a managed or Burst environment
        /// </summary>
        public static void DebugLogIsManaged()
        {
            UnityEngine.Debug.Log(IsManaged ? "IsManaged" : "IsBursted");
        }

        /// <summary>
        /// Calls Debug.Log that prints if Burst enabled of not
        /// </summary>
        public static void DebugLogIsBurstEnabled()
        {
            UnityEngine.Debug.Log(IsBurstEnabled ? "Burst is enabled" : "Burst is NOT enabled");
        }

        struct CheckThatBurstIsEnabledKey {}

        internal static readonly SharedStatic<byte> s_BurstIsEnabled = SharedStatic<byte>.GetOrCreate<byte, CheckThatBurstIsEnabledKey>(16);

        /// <summary>
        /// Checks if Burst is enabled, caches the result. Should be called from a managed environment
        /// </summary>
        /// <param name="forceRefresh">If forceRefresh is true, refreshes the cache.</param>
        /// <returns>True if Burst is enabled</returns>
        /// <exception cref="Exception">If called from Burst environment</exception>
        public static bool CheckThatBurstIsEnabled(bool forceRefresh)
        {
            if (s_BurstIsEnabled.Data == 0 || forceRefresh)
            {
                if (IsManaged == false)
                    throw new Exception("Call CheckThatBurstIsEnabled from a managed C#, not from Burst");
                CheckThatBurstIsEnabledBurstDirectCall();
            }

            return IsBurstEnabled;
        }

        [BurstCompile]
        static void CheckThatBurstIsEnabledBurstDirectCall()
        {
            if (IsManaged)
                s_BurstIsEnabled.Data = 1;              // we supposed to be in Burst, but this is a managed C#, probably Burst is disabled
            else
                s_BurstIsEnabled.Data = byte.MaxValue;
        }

        /// <summary>
        /// Returns True if Burst is enabled. Can be called from Burst of a managed environments
        /// </summary>
        /// <exception cref="Exception">If <see cref="CheckThatBurstIsEnabled"/> was never called before</exception>
        public static bool IsBurstEnabled
        {
            get
            {
                if (s_BurstIsEnabled.Data == 0)
                    throw new Exception("Call CheckThatBurstIsEnabled first from a managed C#");

                return s_BurstIsEnabled.Data == byte.MaxValue;
            }
        }

        /// <summary>
        /// Throws / Debug.LogError-s if IsBurstEnabled is true and this is called from a managed environment
        /// </summary>
        /// <exception cref="Exception">If IsBurstEnabled is true and this is called from a managed environment</exception>
        [BurstDiscard]
        public static void AssertMustBeBurstCompiled()
        {
            if (IsBurstEnabled)
            {
                UnityEngine.Debug.LogError("AssertMustBeBurstCompiled");
                throw new Exception("AssertMustBeBurstCompiled");
            }
        }
    }

    internal static class Burst2ManagedCall<T, Key>
    {
        private static T s_Delegate;
        private static readonly SharedStatic<FunctionPointer<T>> s_SharedStatic = SharedStatic<FunctionPointer<T>>.GetOrCreate<FunctionPointer<T>, Key>(16);
        public static bool IsCreated => s_SharedStatic.Data.IsCreated;

        public static void Init(T @delegate)
        {
            CheckIsNotCreated();
            s_Delegate = @delegate;
            s_SharedStatic.Data = new FunctionPointer<T>(Marshal.GetFunctionPointerForDelegate(s_Delegate));
        }

        public static ref FunctionPointer<T> Ptr()
        {
            CheckIsCreated();
            return ref s_SharedStatic.Data;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
        private static void CheckIsCreated()
        {
            if (IsCreated == false)
                throw new InvalidOperationException("Burst2ManagedCall was NOT created!");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")] // ENABLE_UNITY_COLLECTIONS_CHECKS or UNITY_DOTS_DEBUG
        private static void CheckIsNotCreated()
        {
            if (IsCreated)
                throw new InvalidOperationException("Burst2ManagedCall was already created!");
        }
    }
}
