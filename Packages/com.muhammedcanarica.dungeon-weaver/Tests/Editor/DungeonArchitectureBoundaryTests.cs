using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ProceduralDungeon.Tests
{
    public sealed class DungeonArchitectureBoundaryTests
    {
        [Test]
        public void CoreAssembly_DoesNotReferenceSamplesInputUiOrEditor()
        {
            Assembly core = typeof(DungeonGenerator).Assembly;
            string[] references = core.GetReferencedAssemblies().Select(value => value.Name).ToArray();

            Assert.That(core.GetName().Name, Is.EqualTo("DungeonWeaver.Core"));
            Assert.That(references, Does.Not.Contain("DungeonWeaver.Samples.Prototype"));
            Assert.That(references, Does.Not.Contain("Unity.InputSystem"));
            Assert.That(references, Does.Not.Contain("UnityEngine.UI"));
            Assert.That(references, Does.Not.Contain("UnityEditor"));
            Assert.That(GetType().Assembly.GetName().Name,
                Is.EqualTo("DungeonWeaver.Core.Tests.Editor"));
        }

        [Test]
        public void CoreSources_DoNotUsePrototypeCombatOrDirectInputTypes()
        {
            string root = Path.Combine(PackageRoot(), "Runtime");
            string[] forbidden =
            {
                "UnityEngine.InputSystem", "UnityEngine.UI", "Keyboard",
                "InputField", "TopDownPlayerController", "TopDownPlayerAttack",
                "DungeonEnemyEncounterRegistry", "DungeonEnemy", "DungeonRuntimeInputFocus"
            };

            string[] coreFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path).ToArray();
            Assert.That(coreFiles, Is.Not.Empty);
            for (int fileIndex = 0; fileIndex < coreFiles.Length; fileIndex++)
            {
                string source = File.ReadAllText(coreFiles[fileIndex]);
                for (int tokenIndex = 0; tokenIndex < forbidden.Length; tokenIndex++)
                    Assert.That(source, Does.Not.Contain(forbidden[tokenIndex]),
                        $"{Path.GetFileName(coreFiles[fileIndex])} uses {forbidden[tokenIndex]}.");
            }
        }

        [Test]
        public void PackageManifestAndAssemblyDefinitions_AreRuntimeSafe()
        {
            string root = PackageRoot();
            string manifest = File.ReadAllText(Path.Combine(root, "package.json"));
            string core = File.ReadAllText(Path.Combine(root, "Runtime",
                "DungeonWeaver.Core.asmdef"));
            string editor = File.ReadAllText(Path.Combine(root, "Editor",
                "DungeonWeaver.Editor.asmdef"));
            string tests = File.ReadAllText(Path.Combine(root, "Tests", "Editor",
                "DungeonWeaver.Core.Tests.Editor.asmdef"));

            Assert.That(manifest, Does.Contain("com.muhammedcanarica.dungeon-weaver"));
            Assert.That(manifest, Does.Contain("\"version\": \"0.1.0\""));
            Assert.That(core, Does.Not.Contain("DungeonWeaver.Samples.Prototype"));
            Assert.That(core, Does.Not.Contain("Unity.InputSystem"));
            Assert.That(core, Does.Not.Contain("Unity.ugui"));
            Assert.That(editor, Does.Contain("DungeonWeaver.Core"));
            Assert.That(editor, Does.Contain("\"Editor\""));
            Assert.That(tests, Does.Contain("DungeonWeaver.Core"));
            Assert.That(tests, Does.Not.Contain("DungeonWeaver.Samples.Prototype"));
        }

        private static string PackageRoot()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DungeonGenerator).Assembly);
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo("com.muhammedcanarica.dungeon-weaver"));
            return package.resolvedPath;
        }
    }
}
