using System;
using System.Collections.Generic;
using System.Linq;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.MCP.Editor
{
    public static class VMFrameworkGamePrefabAuthoring
    {
        public static Dictionary<string, object> CreateOrReplace(
            VMFrameworkGamePrefabAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var warnings = new List<string>();
            VMFrameworkMcpTools.RefreshGamePrefabRegistry();

            var existingInfos = VMFrameworkMcpTools.FindGamePrefabInfos(
                request.Id, null, null, int.MaxValue);
            if (existingInfos.Count > 0 && request.Overwrite == false)
            {
                throw new InvalidOperationException(
                    $"GamePrefab id '{request.Id}' already exists in: " +
                    string.Join(", ", existingInfos.Select(info => info.wrapperPath)));
            }

            IGamePrefab gamePrefab = VMFrameworkMcpTools.CreateGamePrefab(
                request.Id, request.GamePrefabType, request.SerializedValues, warnings);
            GamePrefabGeneralSetting generalSetting =
                VMFrameworkMcpTools.ResolveGamePrefabGeneralSetting(gamePrefab);

            GamePrefabWrapper wrapper;
            bool created;
            bool replaced;
            if (existingInfos.Count > 0)
            {
                if (existingInfos.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"GamePrefab id '{request.Id}' exists in multiple wrappers. Refusing to overwrite.");
                }

                var existingInfo = existingInfos[0];
                if (!(existingInfo.wrapper is GamePrefabSingleWrapper singleWrapper))
                {
                    throw new InvalidOperationException(
                        $"Existing wrapper '{existingInfo.wrapperPath}' is not a GamePrefabSingleWrapper.");
                }

                if (string.IsNullOrWhiteSpace(request.AssetName) == false)
                    warnings.Add("assetName is ignored when overwriting an existing GamePrefab.");

                singleWrapper.InitGamePrefabs(new[] { gamePrefab });
                wrapper = singleWrapper;
                created = false;
                replaced = true;
            }
            else
            {
                wrapper = VMFrameworkMcpTools.CreateWrapper(
                    gamePrefab, generalSetting, request.AssetName);
                created = true;
                replaced = false;
            }

            VMFrameworkMcpTools.RegisterWrapper(generalSetting, wrapper);
            wrapper = VMFrameworkMcpTools.SaveAndRefresh(wrapper, generalSetting);
            VMFrameworkMcpTools.ValidateWrapperContainsGamePrefab(wrapper, request.Id);

            return new Dictionary<string, object>
            {
                { "id", request.Id },
                { "gamePrefab", VMFrameworkMcpTools.DescribeGamePrefab(gamePrefab) },
                { "wrapper", VMFrameworkMcpTools.DescribeWrapper(wrapper, true) },
                { "generalSetting", VMFrameworkMcpTools.DescribeGeneralSetting(generalSetting, false) },
                { "created", created },
                { "replaced", replaced },
                { "registered", generalSetting.initialGamePrefabProviders.Contains(wrapper) },
                { "warnings", warnings },
            };
        }
    }
}
