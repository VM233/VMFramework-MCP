#if UNITY_EDITOR
using System.Collections.Generic;
using UnityMCP.Editor;

namespace VMFramework.MCP.Editor
{
    public static class VMFrameworkMcpConfigurationTool
    {
        private const string GET_CONFIGURATION_TOOL_NAME = "vmframework/get-configuration";
        private const string EMPTY_INPUT_SCHEMA =
            "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";

        [MCPProjectTool(GET_CONFIGURATION_TOOL_NAME,
            Description = "Read effective VMFramework MCP project settings, user preferences, and the shared Unity MCP result-budget preference.",
            InputSchemaJson = EMPTY_INPUT_SCHEMA,
            ReadOnly = true)]
        public static object GetConfiguration(Dictionary<string, object> args)
        {
            return VMFrameworkMcpSettingsManager.GetConfigurationSnapshot();
        }
    }
}
#endif
