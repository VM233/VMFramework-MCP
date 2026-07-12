using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace VMFramework.MCP.Editor.Tests
{
    public sealed class GamePrefabValueConversionTests
    {
        [Test]
        public void StructuredLocalizedString_IsConvertedBeforeEnumerableHandling()
        {
            MethodInfo convert = typeof(VMFrameworkMcpTools).GetMethod("ConvertSerializedValue",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(convert, Is.Not.Null);

            var rawValue = new Dictionary<string, object>
            {
                { "$type", typeof(LocalizedString).AssemblyQualifiedName },
                { "m_TableReference", new Dictionary<string, object>
                    {
                        { "m_TableCollectionName", "Item" },
                    }
                },
                { "m_TableEntryReference", new Dictionary<string, object>
                    {
                        { "m_KeyId", 0L },
                        { "m_Key", "FlameIngotItemName" },
                    }
                },
            };

            var localizedString = (LocalizedString)convert.Invoke(null,
                new object[] { rawValue, typeof(LocalizedString), "name" });

            Assert.That(localizedString, Is.InstanceOf<IEnumerable>());

            Type localizedReferenceType = typeof(LocalizedString).BaseType;
            var tableReference = (TableReference)localizedReferenceType
                .GetField("m_TableReference", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(localizedString);
            var entryReference = (TableEntryReference)localizedReferenceType
                .GetField("m_TableEntryReference", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(localizedString);

            tableReference.OnAfterDeserialize();
            entryReference.OnAfterDeserialize();
            Assert.That(tableReference.TableCollectionName, Is.EqualTo("Item"));
            Assert.That(entryReference.Key, Is.EqualTo("FlameIngotItemName"));
        }

        [Test]
        public void ListValues_StillUseCollectionConversion()
        {
            MethodInfo convert = typeof(VMFrameworkMcpTools).GetMethod("ConvertSerializedValue",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(convert, Is.Not.Null);

            var result = (List<string>)convert.Invoke(null,
                new object[] { new[] { "first", "second" }, typeof(List<string>), "values" });

            CollectionAssert.AreEqual(new[] { "first", "second" }, result);
        }
    }
}
