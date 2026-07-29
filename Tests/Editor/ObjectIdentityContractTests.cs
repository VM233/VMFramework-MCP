#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace VMFramework.MCP.Editor.Tests
{
    public class ObjectIdentityContractTests
    {
        [Test]
        public void RuntimeObjectDescriptionUsesCoreStringIdentity()
        {
            var temporary = new GameObject(
                "VMFramework MCP Object Identity");
            try
            {
                MethodInfo describeRuntimeObject =
                    typeof(VMFrameworkMcpTools).GetMethod(
                        "DescribeRuntimeObject",
                        BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(describeRuntimeObject, Is.Not.Null);

                var description =
                    (Dictionary<string, object>)describeRuntimeObject.Invoke(
                        null, new object[] { temporary });

                Assert.That(description["instanceID"],
                    Is.EqualTo(MCPObjectId.Get(temporary)));
                Assert.That(description["instanceID"],
                    Is.TypeOf<string>());
            }
            finally
            {
                Object.DestroyImmediate(temporary);
            }
        }
    }
}
#endif
