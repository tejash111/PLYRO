using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// M-1 (foundation spec, sections 4 and 8.2). The pass fixture is a miniature project with every kind of
    /// module. Each fail fixture breaks one thing in an otherwise valid module, so each case must produce
    /// exactly one violation, and it must name the offending file or folder.
    /// </summary>
    public class ModuleStructureRuleTests
    {
        static List<Violation> Run(ValidationContext context) => new ModuleStructureRule().Run(context).ToList();

        [Test]
        public void RuleIdIsM1()
        {
            Assert.That(new ModuleStructureRule().Id, Is.EqualTo("M-1"));
        }

        [Test]
        public void CompleteProjectPasses()
        {
            List<Violation> violations = Run(ValidatorFixtures.M1("pass"));
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        // The brief's nine cases.
        [TestCase("missing-readme", "README.md", "missing")]
        [TestCase("readme-headings", "README.md", "headings")]
        [TestCase("missing-asmdef", "Bowling.Contracts.Tests.EditMode.asmdef", "missing")]
        [TestCase("wrong-root-namespace", "Bowling.Contracts.asmdef", "rootNamespace")]
        [TestCase("auto-referenced", "Bowling.Contracts.asmdef", "autoReferenced")]
        [TestCase("override-references-false", "Bowling.Contracts.asmdef", "overrideReferences")]
        [TestCase("editmode-platforms", "Bowling.Contracts.Tests.EditMode.asmdef", "includePlatforms")]
        [TestCase("testing-define-constraint", "Bowling.Testing.asmdef", "UNITY_INCLUDE_TESTS")]
        [TestCase("unlisted-module", "Rogue", "not listed")]
        // The rest of the kind table and the folder checks.
        [TestCase("wrong-name", "Bowling.Contracts.asmdef", "name")]
        [TestCase("runtime-platforms", "Bowling.Contracts.asmdef", "includePlatforms")]
        [TestCase("no-nunit-reference", "Bowling.Contracts.Tests.EditMode.asmdef", "nunit.framework.dll")]
        [TestCase("no-test-define-constraint", "Bowling.Contracts.Tests.EditMode.asmdef", "UNITY_INCLUDE_TESTS")]
        [TestCase("unexpected-asmdef", "Bowling.Contracts.Extra.asmdef", "unexpected")]
        [TestCase("malformed-asmdef", "Bowling.Contracts.asmdef", "cannot read")]
        [TestCase("missing-module-folder", "Gameplay", "missing")]
        public void FailureReportsExactlyOneViolationNamingTheFile(string fixture, string name, string messagePart)
        {
            List<Violation> violations = Run(ValidatorFixtures.M1("fail/" + fixture));

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Violation violation = violations[0];
            Assert.That(violation.RuleId, Is.EqualTo("M-1"));
            Assert.That(violation.IsWarning, Is.False);
            Assert.That(violation.Path, Does.EndWith("/" + name));
            Assert.That(violation.Message, Does.Contain(messagePart));
        }

        [Test]
        public void MissingModulesRootIsOneViolation()
        {
            ValidationContext context = ValidatorFixtures.M1("pass");
            context.ModulesRoot = Path.Combine(ValidatorFixtures.Root, "M-1", "does-not-exist");

            List<Violation> violations = Run(context);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].Message, Does.Contain("not found"));
        }
    }
}
