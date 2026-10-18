using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Bowling.App.Tests
{
    public class UnityConsoleGameLogTests
    {
        [Test]
        public void InfoWritesTheCategoryAndMessageAsALog()
        {
            LogAssert.Expect(LogType.Log, "[Cat] msg");

            new UnityConsoleGameLog().Info("Cat", "msg");
        }

        [Test]
        public void WarnWritesTheCategoryAndMessageAsAWarning()
        {
            LogAssert.Expect(LogType.Warning, "[Cat] msg");

            new UnityConsoleGameLog().Warn("Cat", "msg");
        }

        [Test]
        public void ErrorWithoutAnExceptionWritesOnlyTheErrorMessage()
        {
            // An unexpected exception entry would fail the test, so this also proves none is written.
            LogAssert.Expect(LogType.Error, "[Cat] msg");

            new UnityConsoleGameLog().Error("Cat", "msg");
        }

        [Test]
        public void ErrorWithAnExceptionWritesTheErrorMessageThenTheException()
        {
            LogAssert.Expect(LogType.Error, "[Cat] msg");
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException.*boom"));

            new UnityConsoleGameLog().Error("Cat", "msg", new InvalidOperationException("boom"));
        }
    }
}
