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
            Assert.That(typeof(DungeonEnemy).Assembly.GetName().Name,
                Is.EqualTo("DungeonWeaver.Samples.Prototype"));
            Assert.That(GetType().Assembly.GetName().Name,
                Is.EqualTo("DungeonWeaver.Tests.Editor"));
        }

        [Test]
        public void CoreSources_DoNotUsePrototypeCombatOrDirectInputTypes()
        {
            string root = Path.Combine(Application.dataPath, "Scripts", "ProceduralDungeon");
            string sampleRoot = Path.Combine(root, "Samples") + Path.DirectorySeparatorChar;
            string editorRoot = Path.Combine(root, "Editor") + Path.DirectorySeparatorChar;
            string[] forbidden =
            {
                "UnityEngine.InputSystem", "UnityEngine.UI", "Keyboard",
                "InputField", "TopDownPlayerController", "TopDownPlayerAttack",
                "DungeonEnemyEncounterRegistry", "DungeonEnemy", "DungeonRuntimeInputFocus"
            };

            string[] coreFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.StartsWith(sampleRoot) && !path.StartsWith(editorRoot))
                .OrderBy(path => path)
                .ToArray();
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
        public void AssemblyDefinitions_ExpressOneWayRuntimeBoundary()
        {
            string root = Path.Combine(Application.dataPath, "Scripts", "ProceduralDungeon");
            string core = File.ReadAllText(Path.Combine(root, "DungeonWeaver.Core.asmdef"));
            string sample = File.ReadAllText(Path.Combine(root, "Samples", "Prototype",
                "DungeonWeaver.Samples.Prototype.asmdef"));
            string editor = File.ReadAllText(Path.Combine(root, "Editor",
                "DungeonWeaver.Editor.asmdef"));

            Assert.That(core, Does.Not.Contain("DungeonWeaver.Samples.Prototype"));
            Assert.That(core, Does.Not.Contain("Unity.InputSystem"));
            Assert.That(core, Does.Not.Contain("Unity.ugui"));
            Assert.That(sample, Does.Contain("DungeonWeaver.Core"));
            Assert.That(sample, Does.Contain("Unity.InputSystem"));
            Assert.That(sample, Does.Contain("Unity.ugui"));
            Assert.That(editor, Does.Contain("DungeonWeaver.Core"));
            Assert.That(editor, Does.Contain("\"Editor\""));
        }
    }
}
