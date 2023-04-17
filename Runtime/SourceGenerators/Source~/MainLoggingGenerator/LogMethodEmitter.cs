using System;
using System.Collections.Generic;
using System.Text;
using LoggingCommon;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceGenerator.Logging.Declarations;

namespace SourceGenerator.Logging
{
    class LogMethodEmitter
    {
        private LogMethodEmitter()
        {
        }

        public static string Emit(in ContextWrapper context, in LogCallsCollection invokeData, ulong assemblyHash)
        {
            var emitter = new LogMethodEmitter
            {
                m_InvokeData = invokeData,
            };

            return $@"{EmitStrings.SourceFileHeader}
{EmitStrings.SourceFileHeaderIncludes}

namespace Unity.Logging
{{
    {EmitStrings.BurstCompileAttr}
    [HideInStackTrace]
    internal static class Log
    {{
        /// <summary>
        /// Gets/Sets the current active logger.
        /// Log.Info(...) and other calls will use the current one.
        /// </summary>
        public static Logger Logger {{
            get => Unity.Logging.Internal.LoggerManager.Logger;
            set => Unity.Logging.Internal.LoggerManager.Logger = value;
        }}

        internal struct LogContextWithLock {{
            public LogControllerScopedLock Lock;
            public bool IsValid => Lock.IsValid;
        }}

        /// <summary>
        /// Write to a particular Logger
        /// </summary>
        public static LogContextWithLock To(in Logger logger) {{
            var @lock = new LogContextWithLock {{
                Lock = LogControllerScopedLock.Create(logger.Handle)
            }};
            if (@lock.IsValid)
                PerThreadData.ThreadLoggerHandle = logger.Handle;
            return @lock;
        }}

        /// <summary>
        /// Write to a particular Logger that has the handle
        /// </summary>
        public static LogContextWithLock To(in LoggerHandle handle) {{
            var @lock = new LogContextWithLock {{
                Lock = LogControllerScopedLock.Create(handle)
            }};
            if (@lock.IsValid)
                PerThreadData.ThreadLoggerHandle = handle;
            return @lock;
        }}

        /// <summary>
        /// Flushes all log messages into sinks. Can be called only on the main thread.
        /// </summary>
        public static void FlushAll() {{
            Unity.Logging.Internal.LoggerManager.FlushAll();
        }}

        public static LogContextWithDecoratorLogTo To(in LogContextWithDecorator handle) {{
            return new LogContextWithDecoratorLogTo(handle);
        }}

        // Burst direct calls are initialized in 'AfterAssembliesLoaded', so use 'BeforeSplashScreen' as a next phase
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void UnityInitMethod() {{
            Init();
        }}

#if UNITY_EDITOR
        // Initialize on load is called just after domain reload, and burst could be not initialized
        [UnityEditor.InitializeOnLoadMethod]
        static void UnityInitMethodEditor()
        {{
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload += () =>
            {{
                // Burst 100% initialized
                Init();
            }};
        }}
#endif

#if UNITY_DOTSRUNTIME
        static Log() {{
            Init();
        }}
#endif

        private static byte s_Initialized = 0;
        static void Init() {{
            if (s_Initialized != 0) return;
                s_Initialized = 1;

            Unity.Logging.Internal.LoggerManager.Initialize();
            TextLoggerParserOutputHandlers{assemblyHash:X4}.RegisterTextLoggerParserOutputHandlers();
        }}


        // to add OutputWriterDecorateHandler globally
        // Example:
        //   using var a = Log.Decorate('ThreadId', DecoratorFunctions.DecoratorThreadId, true);
        public static LogDecorateHandlerScope Decorate(FixedString512Bytes message, LoggerManager.OutputWriterDecorateHandler Func, bool isBurstable)
        {{
            return LogWriterUtils.AddDecorateHandler(Func, isBurstable);
        }}

        // to add OutputWriterDecorateHandler to dec.Lock's handle
        // Example:
        //   using var threadIdDecor = Log.To(log2).Decorate('ThreadId', DecoratorFunctions.DecoratorThreadId, false);
        public static LogDecorateHandlerScope Decorate(this LogContextWithLock dec, FixedString512Bytes message, LoggerManager.OutputWriterDecorateHandler Func, bool isBurstable)
        {{
            var res = LogWriterUtils.AddDecorateHandler(dec.Lock, Func, isBurstable);
            PerThreadData.ThreadLoggerHandle = default;
            return res;
        }}

        ///////////////////////////////////////////////////////////////////////////////////////////
        /// Generated functions
        ///////////////////////////////////////////////////////////////////////////////////////////
        {emitter.EmitLogMethodDefinitions()}
    }}
}}

{EmitStrings.SourceFileFooter}
";
        }

        private StringBuilder EmitLogMethodDefinitions()
        {
            var sb = new StringBuilder();

            var uniqHashSet = new HashSet<string>();

            foreach (var levelPair in m_InvokeData.InvokeInstances)
            {
                var logLevel = levelPair.Key;
                foreach (var currMethod in levelPair.Value)
                {
                    EmitMethod(sb, in currMethod, logLevel, uniqHashSet);
                }
            }

            return sb;
        }

        private void EmitMethod(StringBuilder sb, in LogCallData currMethod, LogCallKind logLevel, HashSet<string> uniqHashSet)
        {
            var optionalUnsafe = currMethod.ShouldBeMarkedUnsafe ? "unsafe " : "";
            var paramListDeclarationThatUserSees = EmitLogMethodParameterList(currMethod).ToString();
            var paramListDeclarationBurstedFunction = EmitLogMethodParameterList(currMethod, visibleToBurst: true).ToString();

            var paramListCallThatWeSendToBurst = EmitLogMethodParameterListCall(currMethod);

            var uniqPostfix = GenerateUniqPostfix(paramListDeclarationThatUserSees, currMethod, uniqHashSet);

            var castCode = new StringBuilder();
            var sbConvert = new StringBuilder();
            var sbConvertGlobal = logLevel == LogCallKind.Decorate ? new StringBuilder() : null;

            if (currMethod.MessageData.Omitted == false && currMethod.ShouldUsePayloadHandleForMessage)
            {
                currMethod.MessageData.AppendConvertCode(sbConvert, sbConvertGlobal);
            }

            for (var argNumber = 0; argNumber < currMethod.ArgumentData.Count; argNumber++)
            {
                var arg = currMethod.ArgumentData[argNumber];

                arg.AppendConvertCode(argNumber, sbConvert, sbConvertGlobal);

                arg.AppendCastCode(argNumber, castCode);
            }

            if (logLevel == LogCallKind.Decorate)
            {
                sb.Append($@"
        ////////////////////////////////////////////
        /* [Burst] Decorate {currMethod.ToString().Replace("*/", "* /")} */

        {EmitStrings.BurstCompileAttr}
        private static void WriteBurstedDecorate{uniqPostfix}({paramListDeclarationBurstedFunction}, ref LogContextWithDecorator handles)
        {{
            ref var memManager = ref LogContextWithDecorator.GetMemoryManagerNotThreadSafe(ref handles);
            PayloadHandle handle;
            {castCode}
            {EmitLogHandles(currMethod, emitMessage: true)}
        }}

        // to add a constant key-value to a global decoration. When Log call is done - a snapshot will be taken
        // example:
        //  Log.Decorate('ConstantExampleLog1', 999999);
        public {optionalUnsafe}static LogDecorateScope Decorate({paramListDeclarationThatUserSees})
        {{
            {sbConvertGlobal}
            var dec = LoggerManager.BeginEditDecoratePayloadHandles(out var nBefore);

            WriteBurstedDecorate{uniqPostfix}({paramListCallThatWeSendToBurst}, ref dec);

            var payloadHandles = LoggerManager.EndEditDecoratePayloadHandles(nBefore);

            return new LogDecorateScope(default, payloadHandles);
        }}

        // adds a constant key-value to the decoration in dec's logger. When Log call is done - a snapshot will be taken
        // example:
        //   using var decorConst1 = Log.To(log).Decorate('ConstantExampleLog1', 999999);
        public {optionalUnsafe}static LogDecorateScope Decorate(in this LogContextWithLock ctx, {paramListDeclarationThatUserSees})
        {{
            try
            {{
                if (ctx.Lock.IsValid == false) return default;
                ref var logController = ref ctx.Lock.GetLogController();

                {sbConvert}
                var dec = LogController.BeginEditDecoratePayloadHandles(in ctx.Lock, out var nBefore);

                WriteBurstedDecorate{uniqPostfix}({paramListCallThatWeSendToBurst}, ref dec);

                var payloadHandles = LogController.EndEditDecoratePayloadHandles(ref logController, nBefore);

                return new LogDecorateScope(ctx.Lock, payloadHandles);
            }}
            finally
            {{
                PerThreadData.ThreadLoggerHandle = default;
            }}
        }}

        // called from the OutputWriterDecorateHandler inside Log call, so it will write directly to the log message
        // example:
        //   [BurstCompile]
        //   [AOT.MonoPInvokeCallback(typeof(LoggerManager.OutputWriterDecorateHandler))]
        //   public static void DecoratorFixedStringInt(ref LogContextWithDecorator d)
        //   {{
        //      Log.To(d).Decorate(""SomeInt"", 321); <--
        //   }}
        public {optionalUnsafe}static void Decorate(in this LogContextWithDecoratorLogTo dec, {paramListDeclarationThatUserSees})
        {{
            if (dec.context.Lock.IsValid == false) return;
            ref var logController = ref dec.context.Lock.GetLogController();

            {sbConvert}

            var context = dec.context;
            WriteBurstedDecorate{uniqPostfix}({paramListCallThatWeSendToBurst}, ref context);
        }}

        ////////////////////////////////////////////
");

            }
            else
            {
                sb.Append($@"
        ////////////////////////////////////////////
        /* [Burst] [{logLevel}] {currMethod.ToString().Replace("*/", "* /")} */

        {EmitStrings.BurstCompileAttr}
        private static void WriteBursted{logLevel}{uniqPostfix}({paramListDeclarationBurstedFunction}, ref LogController logController, ref LogControllerScopedLock @lock)
        {{
            {castCode}
            {EmitLogBuilders(currMethod, logLevel)}
        }}

        public {optionalUnsafe}static void {logLevel}({paramListDeclarationThatUserSees})
        {{
            var currentLoggerHandle = Unity.Logging.Internal.LoggerManager.CurrentLoggerHandle;
            if (currentLoggerHandle.IsValid == false) return;
            var scopedLock = LogControllerScopedLock.Create(currentLoggerHandle);
            try
            {{
                ref var logController = ref scopedLock.GetLogController();
                if (logController.HasSinksFor(LogLevel.{logLevel}) == false) return;
                {sbConvert}
                WriteBursted{logLevel}{uniqPostfix}({paramListCallThatWeSendToBurst}, ref logController, ref scopedLock);
            }}
            finally
            {{
                scopedLock.Dispose();
            }}
        }}

        public {optionalUnsafe}static void {logLevel}(this LogContextWithLock dec, {paramListDeclarationThatUserSees})
        {{
            try
            {{
                if (dec.Lock.IsValid == false) return;
                ref var logController = ref dec.Lock.GetLogController();
                if (logController.HasSinksFor(LogLevel.{logLevel}) == false) return;
                {sbConvert}
                WriteBursted{logLevel}{uniqPostfix}({paramListCallThatWeSendToBurst}, ref logController, ref dec.Lock);
            }}
            finally
            {{
                dec.Lock.Dispose();
                PerThreadData.ThreadLoggerHandle = default;
            }}
        }}

        ////////////////////////////////////////////
");
            }
        }

        private string GenerateUniqPostfix(string paramList, LogCallData currMethod, HashSet<string> uniqHashSet)
        {
            string Gen(int n)
            {
                return Common.CreateMD5String(paramList + uniqHashSet.Count + currMethod + n.ToString());
            }

            var uniqPostfix = Gen(0);

            for (var guard = 1; guard < 100; ++guard)
            {
                if (uniqHashSet.Add(uniqPostfix))
                    return uniqPostfix;

                uniqPostfix = Gen(guard);
            }

            throw new Exception("Something is wrong with GenerateUniqPostfix - unable to generate unique string in 100 tries");
        }

        // Parameters visible to user or burst
        private static StringBuilder EmitLogMethodParameterList(in LogCallData currInstance, bool visibleToBurst = false)
        {
            var sb = new StringBuilder();

            // First comes the message parameter if not omitted

            var needComma = false;
            if (currInstance.MessageData.Omitted == false)
            {
                needComma = true;

                sb = currInstance.MessageData.AppendUserOrBurstVisibleParameter(sb, visibleToBurst);
            }

            for (var i = 0; i < currInstance.ArgumentData.Count; i++)
            {
                var arg = currInstance.ArgumentData[i];

                if (needComma)
                    sb.Append(", ");

                sb = arg.AppendUserVisibleParameter(sb, visibleToBurst, i);

                needComma = true;
            }

            return sb;
        }

        private static StringBuilder EmitLogMethodParameterListCall(in LogCallData currInstance)
        {
            var sb = new StringBuilder();

            var needComma = false;
            if (currInstance.MessageData.Omitted == false)
            {
                needComma = true;

                sb = currInstance.MessageData.AppendCallParameterForBurst(sb);
            }

            for (var i = 0; i < currInstance.ArgumentData.Count; i++)
            {
                if (needComma)
                    sb.Append(", ");

                var arg = currInstance.ArgumentData[i];

                sb = arg.AppendCallParameterForBurst(sb, i);

                needComma = true;
            }

            return sb;
        }

        private static StringBuilder EmitLogHandles(in LogCallData currInstance, bool emitMessage)
        {
            var sbHandles = new StringBuilder();

            if (emitMessage)
                sbHandles.Append(currInstance.MessageData.GetHandlesBuildCode());

            for (var i = 0; i < currInstance.ArgumentData.Count; i++)
            {
                var arg = currInstance.ArgumentData[i];

                sbHandles = arg.AppendHandlesBuildCode(sbHandles, i);
            }
            return sbHandles;
        }

        private static string EmitLogBuilders(in LogCallData currInstance, LogCallKind callKind)
        {
            var fixedListSize = currInstance.ArgumentData.Count < 512 ? 512 : 4096;

            return $@"
            FixedList{fixedListSize}Bytes<PayloadHandle> handles = new FixedList{fixedListSize}Bytes<PayloadHandle>();
            PayloadHandle handle;

            ref var memManager = ref logController.MemoryManager;

            // Build payloads for each parameter
            {currInstance.MessageData.GetHandlesBuildCode()}

            var stackTraceId = logController.NeedsStackTrace ? ManagedStackTraceWrapper.Capture() : 0;

            Unity.Logging.Builder.BuildDecorators(ref logController, @lock, ref handles);

            {EmitLogHandles(currInstance, emitMessage: false)}

            // Create disjointed buffer
            handle = memManager.CreateDisjointedPayloadBufferFromExistingPayloads(ref handles);
            if (handle.IsValid)
            {{
                // Dispatch message
                logController.DispatchMessage(handle, stackTraceId, LogLevel.{callKind});
            }}
            else
            {{
                Unity.Logging.Internal.Debug.SelfLog.OnFailedToCreateDisjointedBuffer();
                Unity.Logging.Builder.ForceReleasePayloads(handles, ref memManager);
                return;
            }}
";
        }

        private LogCallsCollection     m_InvokeData;
    }
}
