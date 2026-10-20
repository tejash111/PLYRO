using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// The committed config assets hold the defaults of their types. Each public instance field of the loaded asset is
    /// compared with the same field of a freshly created instance, so a hand edit of an asset or a changed default
    /// fails with the field's name.
    /// </summary>
    public sealed class ConfigAssetTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject instance in _created)
                UnityEngine.Object.DestroyImmediate(instance);
            _created.Clear();
        }

        [Test]
        public void TuningAssetHoldsTheDefaults()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameplayTuning>(GameplayTuning.AssetPath);

            int compared = AssertHoldsTheDefaults(asset, GameplayTuning.AssetPath);

            Assert.GreaterOrEqual(compared, 17, "GameplayTuning fields compared");
        }

        [Test]
        public void CatalogAssetHoldsTheDefaults()
        {
            var asset = AssetDatabase.LoadAssetAtPath<DifficultyCatalog>(DifficultyCatalog.AssetPath);

            int compared = AssertHoldsTheDefaults(asset, DifficultyCatalog.AssetPath);

            Assert.GreaterOrEqual(compared, 6, "DifficultyCatalog fields compared");
        }

        // Compares every public instance field of the asset with a fresh instance of its type; returns how many.
        private int AssertHoldsTheDefaults<T>(T asset, string path) where T : ScriptableObject
        {
            Assert.IsNotNull(asset, $"{path} does not exist or is not a {typeof(T).Name}");

            var fresh = ScriptableObject.CreateInstance<T>();
            _created.Add(fresh);

            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
                Assert.AreEqual(field.GetValue(fresh), field.GetValue(asset), $"{typeof(T).Name}.{field.Name}");
            return fields.Length;
        }
    }
}
