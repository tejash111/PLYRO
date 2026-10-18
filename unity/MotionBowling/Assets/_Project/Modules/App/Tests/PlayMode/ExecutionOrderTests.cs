using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.App.Tests
{
    public class ExecutionOrderTests
    {
        const string ModuleAssemblyPrefix = "Bowling.";
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
        public void EveryDriverRunsAtTheOrderOfItsModule()
        {
            // Module runtime assemblies only: Bowling.<Module>, not Bowling.<Module>.Editor or .Tests.*.
            Assembly[] moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => IsModuleAssembly(assembly.GetName().Name))
                .ToArray();
            // There are no drivers yet, so prove the scan ran: App itself must have been found.
            Assert.That(moduleAssemblies.Select(assembly => assembly.GetName().Name), Does.Contain("Bowling.App"));

            var problems = new List<string>();
            foreach (Assembly assembly in moduleAssemblies)
            {
                string module = assembly.GetName().Name.Substring(ModuleAssemblyPrefix.Length);
                FieldInfo constant = typeof(ExecutionOrder).GetField(module, BindingFlags.Public | BindingFlags.Static);
                foreach (Type driver in assembly.GetTypes().Where(type => type.Name.EndsWith(DriverSuffix, StringComparison.Ordinal)))
                {
                    var attribute = driver.GetCustomAttribute<DefaultExecutionOrder>(false);
                    if (constant == null)
                        problems.Add($"{driver.FullName}: ExecutionOrder has no constant named {module}");
                    else if (attribute == null)
                        problems.Add($"{driver.FullName}: no [DefaultExecutionOrder]");
                    else if (attribute.order != (int)constant.GetRawConstantValue())
                        problems.Add($"{driver.FullName}: order {attribute.order}, expected {constant.GetRawConstantValue()}");
                }
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        static bool IsModuleAssembly(string name)
        {
            return name.StartsWith(ModuleAssemblyPrefix, StringComparison.Ordinal)
                && name.IndexOf('.', ModuleAssemblyPrefix.Length) < 0;
        }
    }
}
