using System;
using System.Reflection;
using Bowling.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.Gameplay.Tests
{
    /// <summary><see cref="GameplayDriver"/>: it ticks the service from Update once it is initialized, and guards Initialize.</summary>
    public sealed class GameplayDriverTests
    {
        private static readonly MethodInfo UpdateMethod =
            typeof(GameplayDriver).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        private GameObject _host;
        private GameplayDriver _driver;
        private GameplayRig _rig;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Driver");
            _driver = _host.AddComponent<GameplayDriver>();
            _rig = new GameplayRig();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            _rig.Dispose();
        }

        [Test]
        public void UpdateTicksTheService()
        {
            _rig.Input.SetPresent(true);
            _driver.Initialize(_rig.Service);

            Update();

            Assert.AreEqual(GameState.Join, _rig.Service.Current.State);
        }

        [Test]
        public void UpdateBeforeInitializeDoesNothing()
        {
            _rig.Input.SetPresent(true);

            Assert.DoesNotThrow(Update);

            Assert.AreEqual(GameState.Attract, _rig.Service.Current.State);
        }

        [Test]
        public void InitializeRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => _driver.Initialize(null));
        }

        [Test]
        public void InitializeTwiceThrows()
        {
            _driver.Initialize(_rig.Service);

            Assert.Throws<InvalidOperationException>(() => _driver.Initialize(_rig.Service));
        }

        private void Update()
        {
            Assert.NotNull(UpdateMethod, "GameplayDriver has no private Update method");
            UpdateMethod.Invoke(_driver, null);
        }
    }
}
