using System;
using System.IO;

namespace SourceGenerator.Logging.Declarations
{
    public class OutputPaths
    {
        // Define to use "alternate" output path for source generation (when using outside of Unity project)
#if UNITY_LOGGING_STANDALONE_VS_PROJECT
        public const string SourceGenRootFolderPath = @"./";
#elif UNITY_LOGGING_GENERATE_TO_ASSETS_FOLDER
        public const string SourceGenOutputFolderPath = @"../../../../../Assets/LoggingGenerated";
#else
        public const string SourceGenOutputFolderPath = @"../../../../../Temp/LoggingGenerated";
#endif

        public const string SourceGenTextLoggerTypesFileName = "TextLoggerTypes_Gen.cs";
        public const string SourceGenTextLoggerMethodsFileName = "TextLoggerMethods_Gen.cs";
        public const string SourceGenTextLoggerParserFileName = "TextLoggerParser_Gen.cs";

        public static string GeneratedTypesPath(string assemblyName) => Path.Combine(SourceGenOutputFolderPath, assemblyName, SourceGenTextLoggerTypesFileName);
        public static string GeneratedMethodsPath(string assemblyName) => Path.Combine(SourceGenOutputFolderPath, assemblyName, SourceGenTextLoggerMethodsFileName);
        public static string GeneratedParserPath(string assemblyName) => Path.Combine(SourceGenOutputFolderPath, assemblyName, SourceGenTextLoggerParserFileName);
    }

    public class CompilerMessages
    {
        // Tuple compile error layout: Code, Description
        public static readonly (string, string)GeneralException = ("LSG0001", "Failed generating source file due to an unhandled exception.");
        public static readonly (string, string)ReferenceError = ("LSG0002", "Field cannot be a reference type; Must be value type.");
        public static readonly (string, string)OutputWriterError = ("LSG0003", "Field type doesn't have a default output writer and will be excluded from output; A custom write must be provided.");
        public static readonly (string, string)FieldValueTypeError = ("LSG0004", "Field cannot be confirmed as a value type.");
        public static readonly (string, string)EnumsUnsupportedError = ("LSG0005", "Enum fields aren't (yet) supported.");
        public static readonly (string, string)PublicFieldsWarning = ("LSG0006", "Fields must be publicly accessible.");
        public static readonly (string, string)UnsupportedFieldTypeError = ("LSG0007", "Field type '{0}' isn't supported.");
        public static readonly (string, string)InvalidWriteCall = ("LSG0009", "Log call was made without any arguments");
        public static readonly (string, string)FileWriteException = ("LSG0010", "Failed to write source file to disk.");
        public static readonly (string, string)UnsupportedFixedStringType = ("LSG0011", "Message is an unsupported FixedString type.");
        public static readonly (string, string)UnknownFixedStringType = ("LSG0012", "FixedString type is not one of the known variations.");
        public static readonly (string, string)InvalidArgument = ("LSG0013", "Message argument is invalid / unsupported");
        public static readonly (string, string)MessageLengthError = ("LSG0014", "Default message format length is too long");
        public static readonly (string, string)MessageFixedStringError = ("LSG0015", "Message text cannot be represented using a FixedString.");

        public static readonly (string, string)MissingDecoratePropertyName = ("LSG0018", "Log.Decorate(...) must have a name (string/FixedString) as a first argument, but is");
        public static readonly (string, string)TooMuchDecorateArguments = ("LSG0019", "Too much arguments for Decorate() call");
        public static readonly (string, string)MissingDecorateArguments = ("LSG0020", "Log.Decorate(...) must have more than one argument (name), also a value - an object or a function expected");
        public static readonly (string, string)ExpectedBoolIn3rdDecorateArgument = ("LSG0021", ".Decorate(string message, delegate Func, bool isBurstable) is expected, but 3rd argument is not bool, but is");
        public static readonly (string, string)CannotBeVoidError = ("LSG0022", "You're trying to log a 'void' type, this is not supported");
    }


    internal class EmitStrings
    {
        // https://jira.unity3d.com/browse/DST-390
        //  (DisableDirectCall = true) is added to workaround Burst 1.5 CompileAsyncDelegateMethod exception.
        public const string BurstCompileAttr = "[BurstCompile(DisableDirectCall = true)]";

        /// <summary>
        /// Source emitted at the top of generated source files.
        // CS8123 - The tuple element name 'XXX' is ignored because a different name or no name is specified by the target type
        // CS0105 - The using directive for 'YYY' appeared previously in this namespace
        // CS0436 - The type 'ZZZ' conflicts with the imported type 'QQQ'
        /// </summary>
        public const string SourceFileHeader = @"#pragma warning disable CS8123, CS0105, CS0436

using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Logging;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
";

        /// <summary>
        /// Source emitted at the bottom of generated source files.
        /// </summary>
        public const string SourceFileFooter = @"
#pragma warning restore CS8123, CS0105, CS0436
";

        /// <summary>
        /// Emits the Log "extension" which holds definitions for all variations of Log.Info() and others.
        /// </summary>
        /// <remarks>
        /// Format 0: Implementations for all Log.Info method variants.
        /// Format 1: TextLoggerParserOutputHandlers' suffix to make burst happy
        /// </remarks>
        public const string TextLoggerDefinitionEnclosure = @"
namespace Unity.Logging
{{
    " + BurstCompileAttr + @"
    internal static class Log
    {{
        public static Logger Logger {{
            get => Unity.Logging.Internal.LoggerManager.Logger;
            set => Unity.Logging.Internal.LoggerManager.Logger = value;
        }}

        internal struct LogContextWithLock {{
            public LogControllerScopedLock Lock;
        }}

        public static LogContextWithLock To(in Logger logger) {{
            return new LogContextWithLock {{
                Lock = LogControllerScopedLock.Create(logger.Handle)
            }};
        }}

        public static LogContextWithLock To(in LoggerHandle handle) {{
            return new LogContextWithLock {{
                Lock = LogControllerScopedLock.Create(handle)
            }};
        }}

        public static LogContextWithDecorator To(in LogContextWithDecorator handle) {{
            return handle;
        }}

        static Log() {{
            ManagedStackTraceWrapper.Initialize();
            TimeStampWrapper.Initialize();
            TextLoggerParserOutputHandlers{1}.RegisterTextLoggerParserOutputHandlers();
        }}


        // to add OutputWriterDecorateHandler globally
		// Example:
		//   using var a = Log.Decorate('ThreadId', DecoratorFunctions.DecoratorThreadId, true);
        public static LogDecorateHandlerScope Decorate(FixedString512Bytes message, LoggerManager.OutputWriterDecorateHandler Func, bool isBurstable)
        {{
            return TextLoggerParser.AddDecorateHandler(Func, isBurstable);
        }}

        // to add OutputWriterDecorateHandler to dec.Lock's handle
        // Example:
        //   using var threadIdDecor = Log.To(log2).Decorate('ThreadId', DecoratorFunctions.DecoratorThreadId, false);
        public static LogDecorateHandlerScope Decorate(this LogContextWithLock dec, FixedString512Bytes message, LoggerManager.OutputWriterDecorateHandler Func, bool isBurstable)
        {{
            return TextLoggerParser.AddDecorateHandler(dec.Lock, Func, isBurstable);
        }}

{0}    }}
}}
";

        /// <summary>
        /// Emits the definition for an individual Write method for a given set of structure parameters.
        /// </summary>
        /// <remarks>
        /// Format 0: Declares parameter list, which includes the Message and any/all structs.
        /// Format 1: Implementation of the Write method
        /// Format 2: UniquePostfix to guarantee unique name of bursted function
        /// Format 3: Call parameter list (see Format 0)
        /// Format 4: Log level (Verbose, Info, Warning, Error, etc)
        /// Format 5: Burst friendly version of Format 0 -- no char, no bool, only blittable types
        /// Format 6: arg{n} = iarg{n} cast back to non-blittable
        /// </remarks>
        public const string LogCallMethodDefinitionBursted = @"
        " + BurstCompileAttr + @"
        private static void WriteBursted{4}{2}({5}, ref LogController logController, ref LogControllerScopedLock @lock)
        {{
{6}{1}        }}

        public static void {4}({0})
        {{
            if (Unity.Logging.Internal.LoggerManager.CurrentLoggerHandle.IsValid == false) return;
            var scopedLock = LogControllerScopedLock.Create();
            try
            {{
                ref var logController = ref scopedLock.GetLogController();
                if (logController.HasSinksFor(LogLevel.{4}) == false) return;
                WriteBursted{4}{2}({3}, ref logController, ref scopedLock);
            }}
            finally
            {{
                scopedLock.Dispose();
            }}
        }}

        public static void {4}(this LogContextWithLock dec, {0})
        {{
            try
            {{
                if (dec.Lock.IsValid == false) return;
                ref var logController = ref dec.Lock.GetLogController();
                if (logController.HasSinksFor(LogLevel.{4}) == false) return;
                WriteBursted{4}{2}({3}, ref logController, ref dec.Lock);
            }}
            finally
            {{
                dec.Lock.Dispose();
            }}
        }}
";

        /// <summary>
        /// Emits the definition for an individual Write method for a given set of structure parameters.
        /// </summary>
        /// <remarks>
        /// Format 0: Declares parameter list, which includes the Message and any/all structs.
        /// Format 1: Implementation of the Write method
        /// Format 2: Log level (Verbose, Info, Warning, Error, etc)
        /// Format 3: Call parameter list (see Format 0)
        /// </remarks>
        public const string LogCallMethodDefinitionNotBursted = @"
        private static void WriteManaged{2}({0}, ref LogController logController, ref LogControllerScopedLock @lock)
        {{
{1}        }}

        public static void {2}({0})
        {{
            if (Unity.Logging.Internal.LoggerManager.CurrentLoggerHandle.IsValid == false) return;
            var scopedLock = LogControllerScopedLock.Create();
            try
            {{
                ref var logController = ref scopedLock.GetLogController();
                if (logController.HasSinksFor(LogLevel.{2}) == false) return;
                WriteManaged{2}({3}, ref logController, ref scopedLock);
            }}
            finally
            {{
                scopedLock.Dispose();
            }}
        }}

        public static void {2}(this LogContextWithLock dec, {0})
        {{
            try
            {{
                if (dec.Lock.IsValid == false) return;
                ref var logController = ref dec.Lock.GetLogController();
                if (logController.HasSinksFor(LogLevel.{2}) == false) return;
                WriteManaged{2}({3}, ref logController, ref dec.Lock);
            }}
            finally
            {{
                dec.Lock.Dispose();
            }}
        }}
";


        /// <summary>
        /// Emits the definition for an individual Write method for a given set of structure parameters.
        /// </summary>
        /// <remarks>
        /// Format 0: Declares parameter list, which includes the Message and any/all structs.
        /// Format 1: Implementation of the Write method
        /// Format 2: UniquePostfix to guarantee unique name of bursted function
        /// Format 3: Call parameter list (see Format 0)
        /// Format 4: Burst friendly version of Format 0 -- no char, no bool, only blittable types
        /// Format 5: arg{n} = iarg{n} cast back to non-blittable
        /// </remarks>
        public const string LogCallDecorateMethodDefinitionBursted = @"
        " + BurstCompileAttr + @"
        private static void WriteBurstedDecorate{2}({4}, ref LogContextWithDecorator handles)
        {{
            ref var memManager = ref LogContextWithDecorator.GetMemoryManagerNotThreadSafe(ref handles);

            var handle = Unity.Logging.Builder.BuildMessage(msg, ref memManager);
            if (handle.IsValid)
                handles.Add(handle);

{5}{1}
        }}

        // to add a constant key-value to a global decoration. When Log call is done - a snapshot will be taken
        public static LogDecorateScope Decorate({0})
        {{
            ref var handles = ref LoggerManager.BeginEditDecoratePayloadHandles(out var nBefore);

            unsafe
            {{
                fixed (FixedList4096Bytes<PayloadHandle>* ptr = &handles)
                {{
                    var dec = LogContextWithDecorator.From4096(ptr);
                    WriteBurstedDecorate{2}({3}, ref dec);
                }}
            }}

            var payloadHandles = LoggerManager.EndEditDecoratePayloadHandles(nBefore);

            return new LogDecorateScope(default, payloadHandles);
        }}

		// adds a constant key-value to the decoration in dec's logger. When Log call is done - a snapshot will be taken
		// example:
		//   using var decorConst1 = Log.To(log).Decorate('ConstantExampleLog1', 999999);
        public static LogDecorateScope Decorate(in this LogContextWithLock ctx, {0})
        {{
            if (ctx.Lock.IsValid == false) return default;
            ref var logController = ref ctx.Lock.GetLogController();

            ref var data = ref LogController.BeginEditDecoratePayloadHandles(ref logController, out var nBefore);

            unsafe
            {{
                fixed (FixedList4096Bytes<PayloadHandle>* ptr = &data)
                {{
                    var dec = LogContextWithDecorator.From4096(ptr, ctx.Lock);
                    WriteBurstedDecorate{2}({3}, ref dec);
                }}
            }}

            var payloadHandles = LogController.EndEditDecoratePayloadHandles(ref logController, nBefore);

            return new LogDecorateScope(ctx.Lock, payloadHandles);
        }}

        // called from the OutputWriterDecorateHandler inside Log call, so it will write directly to the log message
        public static void Decorate(this LogContextWithDecorator dec, {0})
        {{
            if (dec.Lock.IsValid == false) return;
            WriteBurstedDecorate{2}({3}, ref dec);
        }}
";

        /// <summary>
        /// Emits the definition for an individual Write method for a given set of structure parameters.
        /// </summary>
        /// <remarks>
        /// Format 0: Declares parameter list, which includes the Message and any/all structs.
        /// Format 1: Implementation of the Write method
        /// Format 2: Call parameter list (see Format 0)
        /// </remarks>
        public const string LogCallDecorateMethodDefinitionNotBursted = @"
        private static void WriteManagedDecorate({0}, ref LogContextWithDecorator handles)
        {{
            ref var memManager = ref LogContextWithDecorator.GetMemoryManagerNotThreadSafe(ref handles);

            var handle = Unity.Logging.Builder.BuildMessage(msg, ref memManager);
            if (handle.IsValid)
                handles.Add(handle);

{1}
        }}

        public static LogDecorateScope Decorate({0})
        {{
            ref var handles = ref LoggerManager.BeginEditDecoratePayloadHandles(out var nBefore);

            unsafe
            {{
                fixed (FixedList4096Bytes<PayloadHandle>* ptr = &handles)
                {{
                    var dec = LogContextWithDecorator.From4096(ptr);
                    WriteManagedDecorate({2}, ref dec);
                }}
            }}

            var payloadHandles = new LoggerManager.EndEditDecoratePayloadHandles(nBefore);

            return new LogDecorateScope(default, payloadHandles);
        }}

        public static LogDecorateScope Decorate(in this LogContextWithLock ctx, {0})
        {{
            if (ctx.Lock.IsValid == false) return default;
            ref var logController = ref ctx.Lock.GetLogController();

            ref var data = ref LogController.BeginEditDecoratePayloadHandles(ref logController, out var nBefore);

            unsafe
            {{
                fixed (FixedList4096Bytes<PayloadHandle>* ptr = &data)
                {{
                    var dec = LogContextWithDecorator.From4096(ptr, ctx.Lock);
                    WriteManagedDecorate({2}, ref dec);
                }}
            }}

            var payloadHandles = LogController.EndEditDecoratePayloadHandles(ref logController, nBefore);

            return new LogDecorateScope(ctx.Lock, payloadHandles);
        }}

        // called from the OutputWriterDecorateHandler inside Log call, so it will write directly to the log message
        public static void Decorate(this LogContextWithDecorator dec, {0})
        {{
            if (dec.Lock.IsValid == false) return;
            WriteManagedDecorate({2}, ref dec);
        }}
";


        /// <summary>
        /// Emits the namespace enclosure for all TextLogger types/structures.
        /// </summary>
        /// <remarks>
        /// Format 0: Namespace containing structs (extracted from original user's structs).
        /// Format 1: Definition for all the individual TextWriter types within a given namespace.
        /// </remarks>
        public const string TextLoggerTypesNamespaceEnclosure = @"
namespace {0}
{{{1}}}
";

        /// <summary>
        /// Emits the type/struct definition for a given TextWriter type.
        /// </summary>
        /// <remarks>
        /// Format 0: Name of the struct.
        /// Format 1: Members of the struct.
        /// </remarks>
        public const string TextLoggerTypesDefinition = @"
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct {0}
    {{{1}
    }}
";

        /// <summary>
        /// [MarshalAs(UnmanagedType.U1)] to make the bool blittable, so burst is happy
        /// </summary>
        public const string TextLoggerTypesFieldMemberAttributeForBool = @"
        [MarshalAs(UnmanagedType.U1)]";

        /// <summary>
        /// Emits a single field for the containing struct; all fields are public.
        /// </summary>
        /// Format 0: Type name for the field.
        /// Format 1: Name of the field.
        public const string TextLoggerTypesFieldMember = @"
        public {0} {1};";

        /// <summary>
        /// Emits a const value holding the TypeId value for the containing struct
        /// </summary>
        /// <remarks>
        /// Format 0: Type name of the field (generated struct type name).
        /// Format 1: TypeId value for the struct.
        /// </remarks>
        public const string TextLoggerTypesIdValue = @"
        public const ulong {0}_TypeIdValue = {1};";


        /// <summary>
        /// Emits the definition for the Writer method, which outputs formatted
        /// strings for the struct's fields.
        /// </summary>
        /// <remarks>
        /// Format 0: Content of the method; individual Write calls for each field.
        /// Format 1: Open context char(s), used to delineate context data from message
        /// Format 2: Close context char(s), used to delineate context data from message
        /// </remarks>
        public const string TextLoggerTypesFormatterDefinition = @"

        public unsafe bool WriteFormattedOutput(ref UnsafeText output)
        {{
            bool success = true;

            success = output.Append((FixedString32Bytes)""{1}"") == FormatError.None && success;{0}
            success = output.Append((FixedString32Bytes)""{2}"") == FormatError.None && success;

            return success;
        }}
";

        /// <summary>
        /// Emits an expression to invoke the Writer method (recursively) on a field in the current strut context
        /// that's also a generated struct type.
        /// </summary>
        /// <remarks>
        /// Format 0: Name of the field on which to invoke the WriteFormattedOutput method.
        /// </remarks>
        public const string TextLoggerTypesFormatterInvokeOnStructField = @"
            success = {0}.WriteFormattedOutput(ref output) && success;";

        /// <summary>
        /// Emits an expression to output a given struct (primitive) field using the default formatter.
        /// </summary>
        /// <remarks>
        /// Format 0: Name of the field (in current struct context) to write to the steam.
        /// Format 1: Explicitly cast of the field, if necessary
        /// </remarks>
        public const string TextLoggerTypesFormatterWritePrimitiveFieldWithDefaultFormat = @"
            success = output.Append({1}{0}) == FormatError.None && success;";

        /// <summary>
        /// Emits the delimiter char(s) that proceed each field (except the last one)
        /// </summary>
        /// <remarks>
        /// Format 0: Delimiter char(s) used to separate the previous field
        /// </remarks>
        public const string TextLoggerTypesFormatterWritePrimitiveDelimiter = @"
            success = output.Append((FixedString32Bytes)""{0}"") == FormatError.None && success;";

        /// <summary>
        /// Emits an expression to output a given struct (boolean) field using the default formatter.
        /// </summary>
        /// <remarks>
        /// Format 0: Name of the boolean field (in current struct context) to write to the steam.
        ///
        /// NOTE: Using capital 'T' and 'F' instead of the (proper) lowercase format. This is because
        /// Boolean.ToString() returns the capital form (for reasons) and should be consistent with it.
        /// </remarks>
        public const string TextLoggerTypesFormatterWriteBooleanFieldWithDefaultFormat = @"
            success = output.Append(new FixedString32Bytes({0} ? (FixedString32Bytes)""True"" : (FixedString32Bytes)""False"")) == FormatError.None && success;";

        /// <summary>
        /// Defines a specific conversion between a user struct and generated struct types.
        /// </summary>
        /// <remarks>
        /// Format 0: Generated struct type returned by conversion.
        /// Format 1: User struct type inputted into conversion operator.
        /// Format 2: Implementation that copies fields from input type to returned output type.
        /// </remarks>
        public const string TextLoggerImplicitConversionDefinition = @"
        public static implicit operator {0}(in {1} arg)
        {{
            return new {0}
            {{{2}
            }};
        }}";

        /// <summary>
        /// Defines the initializer for the TypeId field.
        /// </summary>
        /// <remarks>
        /// Format 0: Generated struct's TypeId field name.
        /// Format 1: Generated struct's type name.
        /// </remarks>
        public const string TextLoggerImplicitConversionTypeIdCopy = @"
                {0} = {1}.{1}_TypeIdValue,";

        /// <summary>
        /// Defines the PayloadHandles using the Builder to set up the payload and dispatching of a log message.
        /// </summary>
        /// <remarks>
        /// Format 0: Size of FixedList - either 512 or 4096.
        /// Format 1: Builder invocations for each generated struct type.
        /// Format 2: Log Level
        /// </remarks>
        public const string LogBuilderInvocationSetup = @"
            var timestamp = TimeStampWrapper.GetTimeStamp();

            FixedList{0}Bytes<PayloadHandle> handles = new FixedList{0}Bytes<PayloadHandle>();
            PayloadHandle handle;

            ref var memManager = ref logController.MemoryManager;

            // Build payloads for each parameter
            handle = Unity.Logging.Builder.BuildMessage(msg, ref memManager);
            if (handle.IsValid)
                handles.Add(handle);

            var stackTraceId = logController.NeedsStackTrace ? ManagedStackTraceWrapper.Capture() : 0;

            Unity.Logging.Builder.BuildDecorators(ref logController, @lock, ref handles);

{1}
            // Create disjointed buffer
            handle = memManager.CreateDisjointedPayloadBufferFromExistingPayloads(ref handles);
            if (handle.IsValid)
            {{
                // Dispatch message
                var lm = new LogMessage(handle, timestamp, stackTraceId, LogLevel.{2});
                logController.DispatchMessage(ref lm);
            }}
            else
            {{
                Unity.Logging.Internal.Debug.SelfLog.OnFailedToCreateDisjointedBuffer();
                Unity.Logging.Builder.ForceReleasePayloads(handles, ref memManager);
                return;
            }}
";

        /// <summary>
        /// Invocation of the Builder to get the payload for each generated struct type.
        /// </summary>
        /// <remarks>
        /// Format 0: Parameter name corresponding to the argument number
        /// </remarks>
        public const string LogBuilderHandleAllocation = @"
            handle = Unity.Logging.Builder.BuildContext(arg{0}, ref memManager);
            if (handle.IsValid)
                handles.Add(handle);
";

        /// <summary>
        /// Invocation of the Builder to get the payload for each special type / FixedString
        /// </summary>
        /// <remarks>
        /// Format 0: Parameter name corresponding to the argument number
        /// </remarks>
        public const string LogBuilderHandleSpecialTypeAllocation = @"
            handle = Unity.Logging.Builder.BuildContextSpecialType(arg{0}, ref memManager);
            if (handle.IsValid)
                handles.Add(handle);
";

        /// <summary>
        /// Defines and individual field copy expression.
        /// </summary>
        /// <remarks>
        /// Format 0: Name of the field
        /// Format 1: Either the parameter name or the struct type depending on if the field is static or not.
        /// Format 2: Name of the field in user's type (can be Item1, Item2, etc in case of ValueTuple)
        /// </remarks>
        public const string TextLoggerImplicitConversionFieldCopy = @"
                {0} = {1}.{2},";

        /// <summary>
        /// Emits the enclosure, invoked by the Sink, to parse struct message buffer and write formatted output.
        /// </summary>
        /// <remarks>
        /// Format 0: Content (methods) of the parser.
        /// Format 1: Postfix for the function name to make it unique (see TextLoggerStructureFormatWriterMethodDefinition)
        /// Format 2: isBursted: true / false
        /// Format 3: TextLoggerParserOutputHandlers' suffix to make burst happy
        /// </remarks>
        public const string TextLoggerStructureParserEnclosure = @"
namespace Unity.Logging
{{
    " + BurstCompileAttr + @"
    internal unsafe struct TextLoggerParserOutputHandlers{3}
    {{
        public static IntPtr HandlerToken;
        internal static void RegisterTextLoggerParserOutputHandlers()
        {{
            HandlerToken = TextLoggerParser.AddOutputHandler(WriteContextFormattedOutput{1}, {2});
        }}
{0}
    }}
}}
";

        /// <summary>
        /// Emits the method definition to write formatted output for a single generated structure instance
        /// from a serialized data buffer.
        /// </summary>
        /// <remarks>
        /// Format 0: Method attributes (if any) e.g. [BurstCompile].
        /// Format 1: Postfix for the function name to make it unique (see TextLoggerStructureParserEnclosure)
        /// Format 2: Individual cases for the switch statement that invoke WriteFormattedOutput on a specific type.
        /// </remarks>
        public const string TextLoggerStructureFormatWriterMethodDefinition = @"
        {0}
        [AOT.MonoPInvokeCallback(typeof(Unity.Logging.TextLoggerParser.OutputWriterHandler))]
        unsafe static TextLoggerParser.ContextWriteResult WriteContextFormattedOutput{1}(ref UnsafeText hstring, byte* buffer, int length)
        {{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (hstring.IsCreated == false || buffer == null || length < UnsafeUtility.SizeOf<ulong>())
            {{
                return TextLoggerParser.ContextWriteResult.Failed;
            }}
#endif

            bool success = false;
            int typeLength = 0;

            var headerSize = UnsafeUtility.SizeOf<ulong>();
            var header = (ulong*) buffer;
            void* data = &header[1];

            // Each generated struct holds a 'TypeId' as its first field identifying the struct's type
            switch (*header)
            {{{2}

                default:
                    return TextLoggerParser.ContextWriteResult.UnknownType;
            }}

            return success ? TextLoggerParser.ContextWriteResult.Success : TextLoggerParser.ContextWriteResult.Failed;
        }}
";

        /// <summary>
        /// Emits a switch case to call WriteFormattedOutput on a specific generated struct type according to typeId.
        /// </summary>
        /// <remarks>
        /// Format 0: TypeId value held within the first field of the generated struct
        /// Format 1: Generated struct type name matching the TypeId value
        /// </remarks>
        public const string TextLoggerStructureFormatWriterMethodCase = @"
                case {0}:
                    typeLength = UnsafeUtility.SizeOf<{1}>();
                    success = length >= typeLength && (({1}*)buffer)->WriteFormattedOutput(ref hstring);
                    break;
";
        public const string TextLoggerStructureFormatWriterSpecialTypeMethodCase = @"
                case {0}:
                    typeLength = UnsafeUtility.SizeOf<{1}>() + headerSize;
                    success = length >= typeLength && hstring.Append(*({1}*) data) == FormatError.None;
                    break;
";

        public const string TextLoggerStructureFormatWriterSpecialTypeDoubleMethod = @"
                case {0}:
                    typeLength = UnsafeUtility.SizeOf<{1}>() + headerSize;
                    success = length >= typeLength && hstring.Append((float)*({1}*) data) == FormatError.None;
                    break;
";

        public const string TextLoggerStructureFormatWriterSpecialTypeBoolMethod = @"
                case {0}:
                    typeLength = UnsafeUtility.SizeOf<{1}>() + headerSize;
                    success = length >= typeLength && hstring.Append(*({1}*) data ? (FixedString32Bytes)""True"" : (FixedString32Bytes)""False"") == FormatError.None;
                    break;
";
    }
}
