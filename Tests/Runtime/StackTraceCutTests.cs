using NUnit.Framework;
using Unity.Logging.Internal;

namespace Unity.Logging.Tests
{
    public class StackTraceCutTests
    {

        [Test]
        public void StackTraceCutCapture()
        {
            var inp = @" at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand+<ExecuteEnumerable>d__3.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at ManagedStackTraceWrapper.Capture () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MonoMethod.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MethodBase.Invoke (System.Object obj, System.Object[] parameters) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Reflect.InvokeMethod (System.Reflection.MethodInfo method, System.Object fixture, System.Object[] args) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Commands.TestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.UnityTestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Console+InternalCancelHandler.Invoke () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.CaptureException (NUnit.Framework.Internal.TestResult result, System.Action action) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand+<ExecuteEnumerable>d__3.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.ImmediateEnumerableCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.DefaultTestWorkItem+<PerformWork>d__2.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.TestRunner.PlaymodeTestsController+<TestRunnerCoroutine>d__21.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0";

            var res = StackTraceCapture.StackTraceDataAnalyzer.CutStackTrace(inp);

            Assert.IsTrue(res.StartsWith(" at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0"), res);
        }

        [Test]
        public void StackTraceCutCaptureStackTrace()
        {
            var inp = @"at ManagedStackTraceWrapper.Capture (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand+<ExecuteEnumerable>d__3.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at ManagedStackTraceWrapper.CaptureStackTrace () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MonoMethod.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MethodBase.Invoke (System.Object obj, System.Object[] parameters) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Reflect.InvokeMethod (System.Reflection.MethodInfo method, System.Object fixture, System.Object[] args) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Commands.TestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.UnityTestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Console+InternalCancelHandler.Invoke () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.CaptureException (NUnit.Framework.Internal.TestResult result, System.Action action) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand+<ExecuteEnumerable>d__3.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.ImmediateEnumerableCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.DefaultTestWorkItem+<PerformWork>d__2.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.TestRunner.PlaymodeTestsController+<TestRunnerCoroutine>d__21.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0";

            var res = StackTraceCapture.StackTraceDataAnalyzer.CutStackTrace(inp);

            Assert.IsTrue(res.StartsWith(" at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0"), res);
        }

        [Test]
        public void StackTraceCutCaptureStackTraceInverted()
        {
            var inp = @"at ManagedStackTraceWrapper.CaptureStackTrace (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand+<ExecuteEnumerable>d__3.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at ManagedStackTraceWrapper.Capture () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MonoMethod.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MethodBase.Invoke (System.Object obj, System.Object[] parameters) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Reflect.InvokeMethod (System.Reflection.MethodInfo method, System.Object fixture, System.Object[] args) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Commands.TestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.UnityTestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Console+InternalCancelHandler.Invoke () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.CaptureException (NUnit.Framework.Internal.TestResult result, System.Action action) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand+<ExecuteEnumerable>d__3.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.ImmediateEnumerableCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.DefaultTestWorkItem+<PerformWork>d__2.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.TestRunner.PlaymodeTestsController+<TestRunnerCoroutine>d__21.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0";

            var res = StackTraceCapture.StackTraceDataAnalyzer.CutStackTrace(inp);

            Assert.IsTrue(res.StartsWith(" at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0"), res);
        }

        [Test]
        public void StackTraceNoCut()
        {
            var inp = @" at StackTraceCapture.GetStackTrace () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MonoMethod.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MethodBase.Invoke (System.Object obj, System.Object[] parameters) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Reflect.InvokeMethod (System.Reflection.MethodInfo method, System.Object fixture, System.Object[] args) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Commands.TestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.UnityTestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Console+InternalCancelHandler.Invoke () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.CaptureException (NUnit.Framework.Internal.TestResult result, System.Action action) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand+<ExecuteEnumerable>d__3.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.ImmediateEnumerableCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.DefaultTestWorkItem+<PerformWork>d__2.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.TestRunner.PlaymodeTestsController+<TestRunnerCoroutine>d__21.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0";

            var res = StackTraceCapture.StackTraceDataAnalyzer.CutStackTrace(inp);

            Assert.AreEqual(res, inp);
        }

        [Test]
        public void StackTraceCanCutBurstedInfo()
        {
            var inp = @" at StackTraceCapture.GetStackTrace () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.AsyncTestDelegate.Invoke () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Log.WriteBurstedInfowp6epF2wLnwQKx2rFXyGwA__ (Unity.Collections.FixedString64Bytes& msg, System.ValueTuple_qmpIs71w53mmWIYZ09gU3Q__& arg0, Unity.Logging.LogController& logController, Unity.Logging.Internal.LogControllerScopedLock& lock) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Log.Info (Unity.Collections.FixedString64Bytes& msg, System.ValueTuple_qmpIs71w53mmWIYZ09gU3Q__& arg0) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MonoMethod.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MethodBase.Invoke (System.Object obj, System.Object[] parameters) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Reflect.InvokeMethod (System.Reflection.MethodInfo method, System.Object fixture, System.Object[] args) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Commands.TestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.UnityTestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Console+InternalCancelHandler.Invoke () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.CaptureException (NUnit.Framework.Internal.TestResult result, System.Action action) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand+<ExecuteEnumerable>d__3.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.ImmediateEnumerableCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.DefaultTestWorkItem+<PerformWork>d__2.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.TestRunner.PlaymodeTestsController+<TestRunnerCoroutine>d__21.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0";

            var res = StackTraceCapture.StackTraceDataAnalyzer.CutStackTrace(inp);

            Assert.IsTrue(res.StartsWith(" at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0"), res);
        }

        [Test]
        public void StackTraceCanCutLogInfo()
        {
            var inp = @" at System.Linq.Enumerable+<UnionIterator>d__71`1[TSource].<>m__Finally2 () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at StackTraceCapture.GetStackTrace () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Constraints.ActualValueDelegate`1[TActual].Invoke () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Log.WriteBurstedInfowp6epF2wLnwQKx2rFXyGwA__ (Unity.Collections.FixedString64Bytes& msg, System.ValueTuple_qmpIs71w53mmWIYZ09gU3Q__& arg0, Unity.Logging.LogController& logController, Unity.Logging.Internal.LogControllerScopedLock& lock) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Log.Info (Unity.Collections.FixedString64Bytes& msg, System.ValueTuple_qmpIs71w53mmWIYZ09gU3Q__& arg0) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MonoMethod.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MethodBase.Invoke (System.Object obj, System.Object[] parameters) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Reflect.InvokeMethod (System.Reflection.MethodInfo method, System.Object fixture, System.Object[] args) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Commands.TestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.UnityTestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.TestDelegate.Invoke () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.CaptureException (NUnit.Framework.Internal.TestResult result, System.Action action) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand+<ExecuteEnumerable>d__3.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.ImmediateEnumerableCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.DefaultTestWorkItem+<PerformWork>d__2.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.TestRunner.PlaymodeTestsController+<TestRunnerCoroutine>d__21.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0";

            var res = StackTraceCapture.StackTraceDataAnalyzer.CutStackTrace(inp);

            Assert.IsTrue(res.StartsWith(" at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0"), res);
        }

        [Test]
        public void StackTraceCanCutLogBurstedMissingInfo()
        {
            var inp = @" at System.Linq.Enumerable+<UnionIterator>d__71`1[TSource].<>m__Finally2 () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at StackTraceCapture.GetStackTrace () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Constraints.ActualValueDelegate`1[TActual].Invoke () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Log.WriteBurstedInfowp6epF2wLnwQKx2rFXyGwA__ (Unity.Collections.FixedString64Bytes& msg, System.ValueTuple_qmpIs71w53mmWIYZ09gU3Q__& arg0, Unity.Logging.LogController& logController, Unity.Logging.Internal.LogControllerScopedLock& lock) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MonoMethod.Invoke (System.Object obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, System.Object[] parameters, System.Globalization.CultureInfo culture) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at System.Reflection.MethodBase.Invoke (System.Object obj, System.Object[] parameters) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Reflect.InvokeMethod (System.Reflection.MethodInfo method, System.Object fixture, System.Object[] args) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.Internal.Commands.TestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.UnityTestMethodCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at NUnit.Framework.TestDelegate.Invoke () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.CaptureException (NUnit.Framework.Internal.TestResult result, System.Action action) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand+<ExecuteEnumerable>d__3.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.ImmediateEnumerableCommand.Execute (NUnit.Framework.Internal.ITestExecutionContext context) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.BeforeAfterTestCommandBase`1+<ExecuteEnumerable>d__15[T].MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.DefaultTestWorkItem+<PerformWork>d__2.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<RunChildren>d__16.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestRunner.NUnitExtensions.Runner.CompositeWorkItem+<PerformWork>d__12.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.TestTools.TestRunner.PlaymodeTestsController+<TestRunnerCoroutine>d__21.MoveNext () <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0
 at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0";

            var res = StackTraceCapture.StackTraceDataAnalyzer.CutStackTrace(inp);

            Assert.IsTrue(res.StartsWith(" at Unity.Logging.Tests.DOTSRuntime.DotsRuntimeRollingTest.LogRollingSize (System.Boolean requireStackTrace) <0x00000 + 0xffffffff> 0 in <00000000000000000000000000000000>:0"), res);
        }
    }
}
