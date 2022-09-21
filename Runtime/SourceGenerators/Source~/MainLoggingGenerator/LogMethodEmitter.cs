using System;
using System.Collections.Generic;
using System.Text;
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

        public static StringBuilder Emit(in GeneratorExecutionContext context, in LogCallsCollection invokeData, ulong assemblyHash)
        {
            var emitter = new LogMethodEmitter
            {
                m_InvokeData = invokeData,
            };

            var sb = new StringBuilder();

            sb.Append($@"{EmitStrings.SourceFileHeader}
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
");

            return sb;
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
            var paramListDeclarationThatUserSees = EmitLogMethodParameterList(currMethod).ToString();
            var paramListDeclarationBurstedFunction = EmitLogMethodParameterList(currMethod, blittableOnly: true).ToString();

            var paramListCallThatWeSendToBurst = EmitLogMethodParameterListCall(currMethod);

            var uniqPostfix = GenerateUniqPostfix(paramListDeclarationThatUserSees, currMethod, uniqHashSet);

            var castCode = new StringBuilder();
            var sbConvert = new StringBuilder();
            var sbConvertGlobal = logLevel == LogCallKind.Decorate ? new StringBuilder() : null;

            if (currMethod.MessageData.Omitted == false && currMethod.ShouldUsePayloadHandleForMessage)
            {
                const string name = "msg";
                var call = (currMethod.MessageData.MessageType == "object") ? ".ToString()" : "";

                sbConvert.Append($@"
                PayloadHandle payloadHandle_{name} = Unity.Logging.Builder.BuildMessage({name + call}, ref logController.MemoryManager);
");

                sbConvertGlobal?.Append($@"
                PayloadHandle payloadHandle_{name} = Unity.Logging.Builder.BuildMessage({name + call}, ref Unity.Logging.Internal.LoggerManager.GetGlobalDecoratorMemoryManager());
");
            }

            for (var i = 0; i < currMethod.ArgumentData.Count; i++)
            {
                var arg = currMethod.ArgumentData[i];

                if (arg.ShouldUsePayloadHandle)
                {
                    static string EmitCopyStringToPayloadBuffer(string dst, string src, bool globalMemManager, bool prependTypeId = false, bool prependLength = false, bool deferredRelease = false)
                    {
                        var sbOptParams = new StringBuilder();
                        if (prependTypeId)
                            sbOptParams.Append(", prependTypeId: true");
                        if (prependLength)
                            sbOptParams.Append(", prependLength: true");
                        if (deferredRelease)
                            sbOptParams.Append(", deferredRelease: true");

                        var memManager = "ref logController.MemoryManager";
                        if (globalMemManager)
                            memManager = "ref Unity.Logging.Internal.LoggerManager.GetGlobalDecoratorMemoryManager()";

                        return $"var payloadHandle_{dst} = Unity.Logging.Builder.CopyStringToPayloadBuffer({src}, {memManager}{sbOptParams});";
                    }

                    var call = (arg.IsConvertibleToString && !arg.IsNonLiteralString) ? ".ToString()" : "";
                    sbConvert.AppendLine(EmitCopyStringToPayloadBuffer($"arg{i}", $"arg{i}" + call, globalMemManager: false, prependTypeId: true, prependLength: true));
                    sbConvertGlobal?.AppendLine(EmitCopyStringToPayloadBuffer($"arg{i}", $"arg{i}" + call, globalMemManager: true, prependTypeId: true, prependLength: true));
                }

                var castString = arg.GetCastCode(i);
                if (string.IsNullOrEmpty(castString) == false)
                    castCode.AppendLine(castString);
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
            {EmitLogHandles(currMethod, emitMessage: true, burstCompatible: true)}
        }}

        // to add a constant key-value to a global decoration. When Log call is done - a snapshot will be taken
        // example:
        //  Log.Decorate('ConstantExampleLog1', 999999);
        public static LogDecorateScope Decorate({paramListDeclarationThatUserSees})
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
        public static LogDecorateScope Decorate(in this LogContextWithLock ctx, {paramListDeclarationThatUserSees})
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
        public static void Decorate(in this LogContextWithDecoratorLogTo dec, {paramListDeclarationThatUserSees})
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
            {EmitLogBuilders(currMethod, logLevel, burstCompatible: true)}
        }}

        public static void {logLevel}({paramListDeclarationThatUserSees})
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

        public static void {logLevel}(this LogContextWithLock dec, {paramListDeclarationThatUserSees})
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
            string Gen()
            {
                return Common.CreateMD5String(paramList + uniqHashSet.Count + currMethod.GetHashCode()) + Common.CreateUniqueCompilableString();
            }

            var uniqPostfix = Gen();

            for (var guard = 0; guard < 100; ++guard)
            {
                if (uniqHashSet.Add(uniqPostfix))
                    return uniqPostfix;

                uniqPostfix = Gen();
            }

            throw new Exception("Something is wrong with GenerateUniqPostfix - unable to generate unique string in 100 tries");
        }

        // Parameters visible to user
        private static StringBuilder EmitLogMethodParameterList(in LogCallData currInstance, bool blittableOnly = false)
        {
            var sb = new StringBuilder();

            // First comes the message parameter if not omitted

            var needComma = false;
            if (currInstance.MessageData.Omitted == false)
            {
                needComma = true;

                var msgType = currInstance.MessageData.GetParameterTypeForUser(blittable: blittableOnly);

                if (currInstance.MessageData.IsNativeText)
                    sb.Append($"in NativeTextBurstWrapper msg");
                else if (msgType == "string" || msgType == "global::System.String")
                    sb.Append($"string msg");
                else
                    sb.Append($"in {msgType} msg");
            }

            for (var i = 0; i < currInstance.ArgumentData.Count; i++)
            {
                var arg = currInstance.ArgumentData[i];
                var argTypeName = arg.GetParameterTypeForUser(blittableOnly, i);

                if (needComma)
                    sb.Append(", ");

                if (arg.IsNativeText)
                    sb.Append($"in NativeTextBurstWrapper {argTypeName.name}");
                else if (argTypeName.type == "string" || argTypeName.type == "global::System.String")
                    sb.Append($"string {argTypeName.name}");
                else
                    sb.Append($"in {argTypeName.type} {argTypeName.name}");

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

                if (currInstance.ShouldUsePayloadHandleForMessage)
                    sb.Append("payloadHandle_msg");
                else
                    sb.Append("msg");
            }

            for (var i = 0; i < currInstance.ArgumentData.Count; i++)
            {
                if (needComma)
                    sb.Append(", ");

                var arg = currInstance.ArgumentData[i];
                if (arg.ShouldUsePayloadHandle)
                {
                    sb.Append($"payloadHandle_{arg.GetInvocationParam(i)}");
                }
                else
                {
                    sb.Append($"{arg.GetInvocationParam(i)}");
                }
                needComma = true;
            }

            return sb;
        }

        private static StringBuilder EmitLogHandles(in LogCallData currInstance, bool emitMessage, bool burstCompatible)
        {
            var sbHandles = new StringBuilder();

            if (emitMessage)
                sbHandles.Append(EmitMessageBuilder(currInstance, burstCompatible));

            for (var i = 0; i < currInstance.ArgumentData.Count; i++)
            {
                var arg = currInstance.ArgumentData[i];
                if (arg.IsSpecialSerializableType())
                {
                    sbHandles.Append($@"
            handle = Unity.Logging.Builder.BuildContextSpecialType(arg{i}, ref memManager);
            if (handle.IsValid)
                handles.Add(handle);
");
                }
                else if (burstCompatible && arg.ShouldUsePayloadHandle)
                {
                    sbHandles.Append($@"
            if (arg{i}.IsValid)
                handles.Add(arg{i});
");
                }
                else
                {
                    sbHandles.Append($@"
            handle = Unity.Logging.Builder.BuildContext(arg{i}, ref memManager);
            if (handle.IsValid)
                handles.Add(handle);
");
                }
            }
            return sbHandles;
        }

        private static string EmitMessageBuilder(in LogCallData currInstance, bool burstCompatible)
        {
            if (currInstance.MessageData.Omitted)
            {
                return $@"
            handle = Unity.Logging.Builder.BuildMessage(""{currInstance.MessageData.LiteralValue}"", ref memManager);
            if (handle.IsValid)
                handles.Add(handle);";
            }

            if (burstCompatible && currInstance.ShouldUsePayloadHandleForMessage)
            {
                return $@"
            if (msg.IsValid)
                handles.Add(msg);";
            }

            return $@"
            handle = Unity.Logging.Builder.BuildMessage(msg, ref memManager);
            if (handle.IsValid)
                handles.Add(handle);";
        }

        private static string EmitLogBuilders(in LogCallData currInstance, LogCallKind callKind, bool burstCompatible)
        {
            var fixedListSize = currInstance.ArgumentData.Count < 512 ? 512 : 4096;

            return $@"
            FixedList{fixedListSize}Bytes<PayloadHandle> handles = new FixedList{fixedListSize}Bytes<PayloadHandle>();
            PayloadHandle handle;

            ref var memManager = ref logController.MemoryManager;

            // Build payloads for each parameter
            {EmitMessageBuilder(currInstance, burstCompatible)}

            var stackTraceId = logController.NeedsStackTrace ? ManagedStackTraceWrapper.Capture() : 0;

            Unity.Logging.Builder.BuildDecorators(ref logController, @lock, ref handles);

            {EmitLogHandles(currInstance, emitMessage: false, burstCompatible)}

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
