using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.App.Tests
{
    /// <summary>
    /// Foundation spec, section 7: a module cannot reference App, so each module type that sets an update order writes
    /// the literal number in its own <c>[DefaultExecutionOrder]</c>, and this test checks it against the
    /// <see cref="ExecutionOrder"/> constant named after the module. It reads every type of every
    /// <c>Bowling.&lt;Module&gt;</c> runtime assembly, not only types called <c>*Driver</c>. App is the one module with
    /// no constant of its own name: its <see cref="GameBootstrap"/> uses <see cref="ExecutionOrder.Bootstrap"/>.
    /// </summary>
    public class ExecutionOrderTests
    {
        const string ModuleAssemblyPrefix = "Bowling.";
        const string AppModule = "App";
        const string DriverSuffix = "Driver";

        [Test]
        public void ConstantsMatchTheSpecTable()
        {
            Assert.That(ExecutionOrder.Bootstrap, Is.EqualTo(-1000));
            Assert.That(ExecutionOrder.PoseInput, Is.EqualTo(-300));
            Assert.That(ExecutionOrder.ThrowDetection, Is.EqualTo(-200));
            Assert.That(ExecutionOrder.Gameplay, Is.EqualTo(-100));
            Assert.That(ExecutionOrder.Presentation, Is.EqualTo(0));
            Assert.That(ExecutionOrder.UI, Is.EqualTo(100));
        }

        [Test]
        public void GameBootstrapRunsAtTheBootstrapOrder()
        {
            var attribute = typeof(GameBootstrap).GetCustomAttribute<DefaultExecutionOrder>(false);

            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.order, Is.EqualTo(ExecutionOrder.Bootstrap));
        }

        [Test]
        public void EveryTypeWithAnExecutionOrderRunsAtTheOrderOfItsModule()
        {
            // Module runtime assemblies only: Bowling.<Module>, not Bowling.<Module>.Editor or .Tests.*.
            Assembly[] moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => IsModuleAssembly(assembly.GetName().Name))
                .ToArray();
            // Prove the scan ran: App itself must have been found, and its one such type is GameBootstrap.
            Assert.That(moduleAssemblies.Select(assembly => assembly.GetName().Name), Does.Contain("Bowling.App"));

            var problems = new List<string>();
            var checkedTypes = new List<Type>();
            foreach (Assembly assembly in moduleAssemblies)
            {
                string module = assembly.GetName().Name.Substring(ModuleAssemblyPrefix.Length);
                Type[] types = assembly.GetTypes();
                problems.AddRange(Problems(module, types));
                checkedTypes.AddRange(types.Where(type => type.GetCustomAttribute<DefaultExecutionOrder>(false) != null));
            }

            Assert.That(checkedTypes, Does.Contain(typeof(GameBootstrap)), "the scan did not reach GameBootstrap");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // --- The check itself, on synthetic types: each way of failing, and each way of passing ---

        [DefaultExecutionOrder(-300)]
        class RightPoseInput : MonoBehaviour { }

        [DefaultExecutionOrder(-300)]
        class RightPoseInputDriver : MonoBehaviour { }

        [DefaultExecutionOrder(5)]
        class WrongPoseInput : MonoBehaviour { }

        [DefaultExecutionOrder(-200)]
        class PoseInputDriverAtTheNextModulesOrder : MonoBehaviour { }

        [DefaultExecutionOrder(0)]
        class PlatformRecorder : MonoBehaviour { }

        [DefaultExecutionOrder(-1000)]
        class RightBootstrap : MonoBehaviour { }

        [DefaultExecutionOrder(-300)]
        class WrongBootstrap : MonoBehaviour { }

        class UnorderedPoseInputDriver : MonoBehaviour { }

        abstract class AbstractPoseInputDriver : MonoBehaviour { }

        interface IPoseInputDriver { }

        class PlainHelper { }

        static List<string> Problems(string module, params Type[] types) => Problems(module, (IEnumerable<Type>)types);

        // The rule under test. Every type that carries [DefaultExecutionOrder] must equal the constant named after
        // its module (App: Bootstrap). A concrete MonoBehaviour called *Driver must carry one at all.
        static List<string> Problems(string module, IEnumerable<Type> types)
        {
            string constantName = module == AppModule ? nameof(ExecutionOrder.Bootstrap) : module;
            FieldInfo constant = typeof(ExecutionOrder).GetField(constantName, BindingFlags.Public | BindingFlags.Static);
            var problems = new List<string>();
            foreach (Type type in types)
            {
                var attribute = type.GetCustomAttribute<DefaultExecutionOrder>(false);
                if (attribute == null)
                {
                    if (IsConcreteDriver(type))
                        problems.Add($"{type.FullName}: no [DefaultExecutionOrder]");
                }
                else if (constant == null)
                {
                    problems.Add($"{type.FullName}: ExecutionOrder has no constant named {constantName}");
                }
                else if (attribute.order != (int)constant.GetRawConstantValue())
                {
                    problems.Add($"{type.FullName}: order {attribute.order}, expected {constant.GetRawConstantValue()} (ExecutionOrder.{constantName})");
                }
            }
            return problems;
        }

        static bool IsConcreteDriver(Type type)
        {
            return type.Name.EndsWith(DriverSuffix, StringComparison.Ordinal)
                && typeof(MonoBehaviour).IsAssignableFrom(type)
                && !type.IsAbstract;
        }

        [Test]
        public void ATypeAtTheOrderOfItsModulePasses()
        {
            Assert.That(Problems("PoseInput", typeof(RightPoseInput), typeof(RightPoseInputDriver)), Is.Empty);
        }

        [Test]
        public void AWrongOrderFailsWhateverTheTypeIsCalled()
        {
            List<string> problems = Problems("PoseInput", typeof(WrongPoseInput));

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("WrongPoseInput").And.Contain("order 5").And.Contain("expected -300"));
        }

        [Test]
        public void ADriverAtAnotherModulesOrderFails()
        {
            List<string> problems = Problems("PoseInput", typeof(PoseInputDriverAtTheNextModulesOrder));

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("order -200").And.Contain("expected -300"));
        }

        [Test]
        public void ATypeInAModuleWithNoConstantFails()
        {
            List<string> problems = Problems("Platform", typeof(PlatformRecorder));

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("PlatformRecorder").And.Contain("no constant named Platform"));
        }

        [Test]
        public void TheAppModuleUsesTheBootstrapConstant()
        {
            Assert.That(Problems("App", typeof(RightBootstrap)), Is.Empty);

            List<string> problems = Problems("App", typeof(WrongBootstrap));

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("order -300").And.Contain("expected -1000").And.Contain("ExecutionOrder.Bootstrap"));
        }

        [Test]
        public void AConcreteDriverWithoutAnOrderFails()
        {
            List<string> problems = Problems("PoseInput", typeof(UnorderedPoseInputDriver));

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("UnorderedPoseInputDriver").And.Contain("no [DefaultExecutionOrder]"));
        }

        [Test]
        public void AbstractDriversInterfacesAndPlainTypesNeedNoOrder()
        {
            Assert.That(Problems("PoseInput", typeof(AbstractPoseInputDriver), typeof(IPoseInputDriver), typeof(PlainHelper)), Is.Empty);
        }

        [Test]
        public void EveryProblemIsReportedNotOnlyTheFirst()
        {
            List<string> problems = Problems("PoseInput", typeof(WrongPoseInput), typeof(UnorderedPoseInputDriver), typeof(RightPoseInput));

            Assert.That(problems, Has.Count.EqualTo(2));
        }

        static bool IsModuleAssembly(string name)
        {
            return name.StartsWith(ModuleAssemblyPrefix, StringComparison.Ordinal)
                && name.IndexOf('.', ModuleAssemblyPrefix.Length) < 0;
        }
    }
}
