using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using VMFramework.OdinExtensions;
using VMFramework.UI;

namespace VMFramework.MCP.Editor.Tests
{
    public sealed class VisualElementPathValidationTests
    {
        private sealed class Fixture
        {
            public VisualElementPath optionalPath = new();

            [IsNotNullOrEmpty]
            public VisualElementPath requiredPath = new();
        }

        private sealed class MissingUnityObjectFixture
        {
            public Transform customTransform;
            public VisualElementPath path = new();
        }

        [Test]
        public void EmptyPath_IsValidOnlyWhenFieldIsOptional()
        {
            Type toolsType = typeof(VMFrameworkMcpTools);
            MethodInfo isRequired = toolsType.GetMethod("IsVisualElementPathRequired",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo validate = toolsType.GetMethod("ValidateVisualElementPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Type recordType = toolsType.GetNestedType("VisualElementPathRecord", BindingFlags.NonPublic);

            Assert.That(isRequired, Is.Not.Null);
            Assert.That(validate, Is.Not.Null);
            Assert.That(recordType, Is.Not.Null);

            AssertEmptyPathResult(nameof(Fixture.optionalPath), expectedRequired: false, expectedValid: true);
            AssertEmptyPathResult(nameof(Fixture.requiredPath), expectedRequired: true, expectedValid: false);

            void AssertEmptyPathResult(string fieldName, bool expectedRequired, bool expectedValid)
            {
                FieldInfo field = typeof(Fixture).GetField(fieldName);
                bool required = (bool)isRequired.Invoke(null, new object[] { field });
                Assert.That(required, Is.EqualTo(expectedRequired));

                object record = Activator.CreateInstance(recordType, nonPublic: true);
                recordType.GetField("owner").SetValue(record, "Fixture");
                recordType.GetField("member").SetValue(record, fieldName);
                recordType.GetField("path").SetValue(record, field.GetValue(new Fixture()));
                recordType.GetField("required").SetValue(record, required);
                recordType.GetField("allowedTypes").SetValue(record, new List<Type>());

                var result = (Dictionary<string, object>)validate.Invoke(null,
                    new[] { (object)new VisualElement(), record });
                Assert.That(result["required"], Is.EqualTo(expectedRequired));
                Assert.That(result["valid"], Is.EqualTo(expectedValid));
                Assert.That(result.ContainsKey("skipped"), Is.EqualTo(expectedValid));
            }
        }

        [Test]
        public void ScanVisualElementPaths_DoesNotEnumerateMissingUnityObjectReferences()
        {
            Type toolsType = typeof(VMFrameworkMcpTools);
            MethodInfo scan = toolsType.GetMethod("ScanVisualElementPaths",
                BindingFlags.Static | BindingFlags.NonPublic);
            Type recordType = toolsType.GetNestedType("VisualElementPathRecord", BindingFlags.NonPublic);
            Assert.That(scan, Is.Not.Null);
            Assert.That(recordType, Is.Not.Null);

            var gameObject = new GameObject("Missing Transform Fixture");
            var fixture = new MissingUnityObjectFixture
            {
                customTransform = gameObject.transform
            };
            UnityEngine.Object.DestroyImmediate(gameObject);

            Type listType = typeof(List<>).MakeGenericType(recordType);
            var records = (IList)Activator.CreateInstance(listType);
            Assert.DoesNotThrow(() => scan.Invoke(null, new object[]
            {
                fixture,
                "Fixture",
                records,
                new HashSet<object>(),
                0,
                null
            }));
            Assert.That(records.Count, Is.EqualTo(1));
        }
    }
}
