using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Bowling.Contracts.Tests
{
    public class ReadonlyStructConstructorTests
    {
        private const BindingFlags InstanceFields =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void EveryReadonlyStructHasFullConstructor()
        {
            List<Type> structs = ReadonlyStructs().ToList();
            Assert.IsNotEmpty(structs, "no readonly structs found, so the reflection filter is wrong");

            foreach (Type type in structs)
            {
                AssertFullConstructor(type);
            }
        }

        private static IEnumerable<Type> ReadonlyStructs()
        {
            return typeof(LaneSpec).Assembly.GetTypes()
                .Where(t => t.IsValueType && !t.IsEnum && t.IsPublic && IsMarkedReadOnly(t));
        }

        // The compiler may embed its own copy of IsReadOnlyAttribute (internal), so match it by name.
        private static bool IsMarkedReadOnly(Type type)
        {
            return type.GetCustomAttributes(false).Any(a => a.GetType().Name == "IsReadOnlyAttribute");
        }

        private static void AssertFullConstructor(Type type)
        {
            ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(1, constructors.Length, $"{type.Name} needs exactly one public constructor");

            FieldInfo[] fields = type.GetFields(InstanceFields)
                .Where(f => !f.Name.Contains("k__BackingField"))
                .ToArray();
            ParameterInfo[] parameters = constructors[0].GetParameters();
            Assert.AreEqual(fields.Length, parameters.Length,
                $"{type.Name} constructor must take all {fields.Length} fields");

            for (int i = 0; i < fields.Length; i++)
            {
                Assert.AreEqual(fields[i].FieldType, parameters[i].ParameterType,
                    $"{type.Name} parameter {i} type");
                Assert.AreEqual(LowerFirst(fields[i].Name), parameters[i].Name,
                    $"{type.Name} parameter {i} name");
            }
        }

        private static string LowerFirst(string name)
        {
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
