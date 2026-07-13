using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.MCP.Editor.Tests
{
    public sealed class GameTagToolTests
    {
        [Test]
        public void BuildDefaultGameTagKey_UsesFrameworkNamingConvention()
        {
            MethodInfo method = typeof(VMFrameworkMcpTools).GetMethod("BuildDefaultGameTagKey",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(null, new object[] { "rotate_burst_item", "TagName" }),
                Is.EqualTo("RotateBurstItemTagName"));
            Assert.That(method.Invoke(null, new object[] { "unique_item", "TagDescription" }),
                Is.EqualTo("UniqueItemTagDescription"));
        }

        [Test]
        public void UpsertGameTagInfo_CreatesThenUpdatesOneEntryWithLocalizedReferences()
        {
            MethodInfo method = typeof(VMFrameworkMcpTools).GetMethod("UpsertGameTagInfo",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var group = ScriptableObject.CreateInstance<GameTagGroup>();
            try
            {
                var created = (GameTagInfo)method.Invoke(null, new object[]
                {
                    group, "test_tag", true, true, "GameTag", "TestTagTagName",
                    "TestTagTagDescription"
                });
                Assert.That(group.gameTagInfos, Has.Count.EqualTo(1));
                Assert.That(created.name.TableReference.TableCollectionName, Is.EqualTo("GameTag"));
                Assert.That(created.name.TableEntryReference.Key, Is.EqualTo("TestTagTagName"));
                Assert.That(created.description.TableEntryReference.Key,
                    Is.EqualTo("TestTagTagDescription"));

                var updated = (GameTagInfo)method.Invoke(null, new object[]
                {
                    group, "test_tag", true, false, "OtherTable", "RenamedTagName", ""
                });
                Assert.That(group.gameTagInfos, Has.Count.EqualTo(1));
                Assert.That(updated, Is.SameAs(created));
                Assert.That(updated.name.TableReference.TableCollectionName, Is.EqualTo("OtherTable"));
                Assert.That(updated.name.TableEntryReference.Key, Is.EqualTo("RenamedTagName"));
                Assert.That(updated.hasDescription, Is.False);
                Assert.That(updated.description.IsEmpty, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(group);
            }
        }
    }
}
