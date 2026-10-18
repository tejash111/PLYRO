using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Bowling.App.Tests
{
    public class BootstrapSceneTests
    {
        [UnityTest]
        public IEnumerator LoadingMainBootsExactlyOneGameBootstrapAndLogsBoot()
        {
            // Awake logs while the scene loads, and LogAssert only matches logs that come after Expect.
            LogAssert.Expect(LogType.Log, "[App] Boot");

            SceneManager.LoadScene("Main");
            yield return null;

            GameBootstrap[] bootstraps = Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None);
            Assert.That(bootstraps, Has.Length.EqualTo(1));
            Assert.That(bootstraps[0].Log, Is.Not.Null);
            Assert.That(bootstraps[0].Clock, Is.Not.Null);
            Assert.That(bootstraps[0].Clock.Now, Is.GreaterThanOrEqualTo(0.0));
        }
    }
}
