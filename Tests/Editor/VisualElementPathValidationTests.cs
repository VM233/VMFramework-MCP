using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
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
    }
}
