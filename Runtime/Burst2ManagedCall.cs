using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Debug = UnityEngine.Debug;

namespace Unity.Logging
{
    [BurstCompile(CompileSynchronously = true)]
    public static class BurstHelper
    {
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

        public static bool IsBurst => IsManaged == false;

        public static void DebugLogIsManaged()
        {
            UnityEngine.Debug.Log(IsManaged ? "IsManaged" : "IsBursted");
        }

        public static void DebugLogIsBurstEnabled()
        {
            UnityEngine.Debug.Log(IsBurstEnabled ? "Burst is enabled" : "Burst is NOT enabled");
        }

        struct CheckThatBurstIsEnabledKey {}

        internal static readonly SharedStatic<byte> s_BurstIsEnabled = SharedStatic<byte>.GetOrCreate<byte, CheckThatBurstIsEnabledKey>(16);

        public static bool CheckThatBurstIsEnabled(bool forceRefresh)
        {
            if (s_BurstIsEnabled.Data == 0 || forceRefresh)
            {
                if (IsManaged == false)
                    throw new Exception("Call CheckThatBurstIsEnabled from Managed C#, not from Burst");
                CheckThatBurstIsEnabledBurstDirectCall();
            }

            return IsBurstEnabled;
        }

        [BurstCompile(CompileSynchronously = true)]
        static void CheckThatBurstIsEnabledBurstDirectCall()
        {
            if (IsManaged)
                s_BurstIsEnabled.Data = 1;              // we supposed to be in burst, but this is a managed C#, probably burst is disabled
            else
                s_BurstIsEnabled.Data = byte.MaxValue;
        }

        public static bool IsBurstEnabled
        {
            get
            {
                if (s_BurstIsEnabled.Data == 0)
                    throw new Exception("Call CheckThatBurstIsEnabled first from Managed C#");

                return s_BurstIsEnabled.Data == byte.MaxValue;
            }
        }

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
