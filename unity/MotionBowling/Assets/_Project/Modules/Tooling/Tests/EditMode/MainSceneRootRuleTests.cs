using System.Collections.Generic;
using System.Linq;
using Bowling.Tooling.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bowling.Tooling.Tests
{
    /// <summary>
    /// M-7 (foundation spec, sections 3.5 and 8.2): a root object of the main scene is a prefab instance root or is
    /// named <c>Bootstrap</c>. Each case builds a temporary scene and points <see cref="ValidationContext.MainScenePath"/>
    /// at it.
    /// </summary>
    public class MainSceneRootRuleTests
    {
        string _folder;

        [SetUp]
        public void SetUp() => _folder = TempAssets.Folder("M7");

        [TearDown]
        public void TearDown() => TempAssets.DeleteAll();

        ValidationContext ContextFor(string scenePath)
        {
            ValidationContext context = TempAssets.Context(_folder);
            context.MainScenePath = scenePath;
            return context;
        }

        static List<Violation> Run(ValidationContext context) => new MainSceneRootRule().Run(context).ToList();

        static void Instantiate(string prefabPath, Scene scene) =>
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), scene);

        [Test]
        public void RuleIdIsM7()
        {
            Assert.That(new MainSceneRootRule().Id, Is.EqualTo("M-7"));
        }

        [Test]
        public void ALooseCubeAtTheRootFails()
        {
            string path = TempAssets.SavedScene(_folder, "Loose", _ => GameObject.CreatePrimitive(PrimitiveType.Cube));

            List<Violation> violations = Run(ContextFor(path));

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].RuleId, Is.EqualTo("M-7"));
            Assert.That(violations[0].Path, Is.EqualTo(path));
            Assert.That(violations[0].IsWarning, Is.False);
            Assert.That(violations[0].Message, Does.Contain("Cube"));
        }

        [Test]
        public void EachLooseRootIsItsOwnViolation()
        {
            string path = TempAssets.SavedScene(_folder, "Loose2", _ =>
            {
                new GameObject("Alpha");
                new GameObject("Beta");
            });

            List<Violation> violations = Run(ContextFor(path));

            Assert.That(violations, Has.Count.EqualTo(2), string.Join("\n", violations));
            Assert.That(violations.Select(v => v.Message), Has.Some.Contain("Alpha"));
            Assert.That(violations.Select(v => v.Message), Has.Some.Contain("Beta"));
        }

        [Test]
        public void ALooseRootNextToBootstrapStillFails()
        {
            string path = TempAssets.SavedScene(_folder, "Mixed", _ =>
            {
                new GameObject("Bootstrap");
                GameObject.CreatePrimitive(PrimitiveType.Sphere);
            });

            List<Violation> violations = Run(ContextFor(path));

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].Message, Does.Contain("Sphere"));
        }

        [Test]
        public void ARootThatIsAnUnpackedPrefabFails()
        {
            string prefab = TempAssets.SavedPrefab(_folder, "Thing", _ => { });
            string path = TempAssets.SavedScene(_folder, "Unpacked", scene =>
            {
                Instantiate(prefab, scene);
                PrefabUtility.UnpackPrefabInstance(scene.GetRootGameObjects()[0], PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            });

            List<Violation> violations = Run(ContextFor(path));

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
        }

        [Test]
        public void BootstrapAndAPrefabInstanceAsRootsPass()
        {
            string prefab = TempAssets.SavedPrefab(_folder, "Rendering", _ => { });
            string path = TempAssets.SavedScene(_folder, "Good", scene =>
            {
                new GameObject("Bootstrap");
                Instantiate(prefab, scene);
            });

            List<Violation> violations = Run(ContextFor(path));

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void ARenamedPrefabInstancePasses()
        {
            string prefab = TempAssets.SavedPrefab(_folder, "Thing", _ => { });
            string path = TempAssets.SavedScene(_folder, "Renamed", scene =>
            {
                Instantiate(prefab, scene);
                scene.GetRootGameObjects()[0].name = "Whatever";
            });

            List<Violation> violations = Run(ContextFor(path));

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        [Test]
        public void AMissingSceneFileIsOneViolation()
        {
            string path = _folder + "/Nope.unity";

            List<Violation> violations = Run(ContextFor(path));

            Assert.That(violations, Has.Count.EqualTo(1), string.Join("\n", violations));
            Assert.That(violations[0].RuleId, Is.EqualTo("M-7"));
            Assert.That(violations[0].Path, Is.EqualTo(path));
            Assert.That(violations[0].Message, Does.Contain("not found"));
        }

        [Test]
        public void TheRealMainScenePasses()
        {
            ValidationContext context = TempAssets.Context(_folder);

            List<Violation> violations = Run(context);

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
            Assert.That(context.MainScenePath, Is.EqualTo("Assets/_Project/Scenes/Main.unity"));
        }

        [Test]
        public void TheRuleLeavesTheOpenScenesTheActiveSceneAndTheDirtyFlagsAsItFoundThem()
        {
            ValidationContext context = TempAssets.Context(_folder);
            Scene active = SceneManager.GetActiveScene();
            int count = SceneManager.sceneCount;
            bool[] dirty = Enumerable.Range(0, count).Select(i => SceneManager.GetSceneAt(i).isDirty).ToArray();

            Run(context);

            Assert.That(SceneManager.sceneCount, Is.EqualTo(count));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
            Assert.That(Enumerable.Range(0, count).Select(i => SceneManager.GetSceneAt(i).isDirty), Is.EqualTo(dirty));
            Assert.That(SceneManager.GetSceneByPath(context.MainScenePath).isLoaded, Is.False);
        }
    }
}
