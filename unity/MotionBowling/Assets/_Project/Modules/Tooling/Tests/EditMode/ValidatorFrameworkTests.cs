using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// The validator framework: the output format, the batch exit decision, and how a run collects the
    /// violations of several rules, including a rule that throws.
    /// </summary>
    public class ValidatorFrameworkTests
    {
        sealed class FixedRule : IValidationRule
        {
            readonly Violation[] _violations;

            public FixedRule(string id, params Violation[] violations)
            {
                Id = id;
                _violations = violations;
            }

            public string Id { get; }

            public IEnumerable<Violation> Run(ValidationContext context) => _violations;
        }

        sealed class ThrowingRule : IValidationRule
        {
            public string Id => "X-9";

            public IEnumerable<Violation> Run(ValidationContext context) => throw new InvalidOperationException("boom");
        }

        [Test]
        public void EveryReportedLineIsOneLogMessageWithoutAStackTrace()
        {
            // The menu entry runs the rules on the real project and reports through the same code as RunBatch.
            // Whatever the project's state, it ends with the summary line, and no line may carry a stack trace.
            var lines = new List<(string Text, string StackTrace)>();
            void Capture(string text, string stackTrace, LogType type)
            {
                if (Regex.IsMatch(text, @"^([A-Z]-\d|WARN |Source-of-truth validation:)")) lines.Add((text, stackTrace));
            }

            Application.logMessageReceived += Capture;
            try
            {
                SourceOfTruthValidator.RunFromMenu();
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }

            Assert.That(lines, Is.Not.Empty);
            Assert.That(lines.Last().Text, Does.StartWith("Source-of-truth validation:"));
            Assert.That(lines.All(l => string.IsNullOrEmpty(l.StackTrace)), Is.True, "a reported line has a stack trace");
            Assert.That(lines.Any(l => l.Text.Contains("\n")), Is.False, "a reported line spans several lines");
        }

        [Test]
        public void ViolationPrintsRuleIdPathLineAndMessage()
        {
            var violation = new Violation("M-1", "Assets/_Project/Modules/A/README.md", 12, "missing heading", false);

            Assert.That(violation.ToString(), Is.EqualTo("M-1 Assets/_Project/Modules/A/README.md:12 missing heading"));
        }

        [Test]
        public void ViolationOmitsTheLineWhenItIsZero()
        {
            var violation = new Violation("M-2", "Assets/X.asmdef", 0, "bad reference", false);

            Assert.That(violation.ToString(), Is.EqualTo("M-2 Assets/X.asmdef bad reference"));
        }

        [Test]
        public void ViolationKeepsItsFields()
        {
            var violation = new Violation("M-1", "p", 3, "m", true);

            Assert.That(violation.RuleId, Is.EqualTo("M-1"));
            Assert.That(violation.Path, Is.EqualTo("p"));
            Assert.That(violation.Line, Is.EqualTo(3));
            Assert.That(violation.Message, Is.EqualTo("m"));
            Assert.That(violation.IsWarning, Is.True);
        }

        [Test]
        public void ExitCodeIsOneOnlyForNonWarningViolations()
        {
            var warning = new Violation("M-4", "p", 0, "long file", true);
            var error = new Violation("M-1", "p", 0, "broken", false);

            Assert.That(SourceOfTruthValidator.ExitCodeFor(new Violation[0]), Is.EqualTo(0));
            Assert.That(SourceOfTruthValidator.ExitCodeFor(new[] { warning }), Is.EqualTo(0));
            Assert.That(SourceOfTruthValidator.ExitCodeFor(new[] { warning, error }), Is.EqualTo(1));
        }

        [Test]
        public void CreateRulesHoldsTheEditorRules()
        {
            IReadOnlyList<IValidationRule> rules = SourceOfTruthValidator.CreateRules();

            Assert.That(rules.Select(r => r.Id), Is.EqualTo(new[] { "M-1", "M-2", "G-4", "G-5", "M-7" }));
        }

        [Test]
        public void CreateRulesReturnsNewInstancesEachTime()
        {
            Assert.That(SourceOfTruthValidator.CreateRules()[0], Is.Not.SameAs(SourceOfTruthValidator.CreateRules()[0]));
        }

        [Test]
        public void RunCollectsViolationsFromEveryRuleInOrder()
        {
            var first = new Violation("A-1", "a", 0, "one", false);
            var second = new Violation("B-1", "b", 0, "two", true);
            var rules = new IValidationRule[] { new FixedRule("A-1", first), new FixedRule("B-1", second) };

            IReadOnlyList<Violation> violations = SourceOfTruthValidator.Run(rules, new ValidationContext());

            Assert.That(violations.Select(v => v.RuleId), Is.EqualTo(new[] { "A-1", "B-1" }));
        }

        [Test]
        public void ARuleThatThrowsBecomesAViolationAndTheOtherRulesStillRun()
        {
            var after = new Violation("B-1", "b", 0, "two", false);
            var rules = new IValidationRule[] { new ThrowingRule(), new FixedRule("B-1", after) };

            IReadOnlyList<Violation> violations = SourceOfTruthValidator.Run(rules, new ValidationContext());

            Assert.That(violations, Has.Count.EqualTo(2));
            Assert.That(violations[0].RuleId, Is.EqualTo("X-9"));
            Assert.That(violations[0].IsWarning, Is.False);
            Assert.That(violations[0].Message, Does.Contain("InvalidOperationException").And.Contain("boom"));
            Assert.That(violations[1].RuleId, Is.EqualTo("B-1"));
            Assert.That(SourceOfTruthValidator.ExitCodeFor(violations), Is.EqualTo(1));
        }
    }
}
