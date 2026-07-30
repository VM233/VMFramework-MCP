#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityMCP.Editor;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.MCP.Editor
{
    /// <summary>
    /// Project-owned extension point for facts that VMFramework does not own, such as factions or abilities.
    /// Implementations must read and mutate the domain's authoritative components instead of inferring facts
    /// from names, tags, prefab paths, or UI state.
    /// </summary>
    public interface IVMFrameworkMcpRuntimeGameItemDomainAdapter
    {
        int Priority { get; }

        bool CanHandle(IGameItem gameItem);

        void SetFaction(IGameItem gameItem, string factionID);

        void AddInspectionSections(IGameItem gameItem,
            IDictionary<string, object> sections);
    }

    internal static class VMFrameworkMcpRuntimeGameItemDomain
    {
        private static IReadOnlyList<IVMFrameworkMcpRuntimeGameItemDomainAdapter> adapters;

        internal static IVMFrameworkMcpRuntimeGameItemDomainAdapter GetRequiredAdapter(
            IGameItem gameItem)
        {
            List<IVMFrameworkMcpRuntimeGameItemDomainAdapter> matches = GetAdapters()
                .Where(adapter => adapter.CanHandle(gameItem))
                .OrderByDescending(adapter => adapter.Priority)
                .ThenBy(adapter => adapter.GetType().FullName, StringComparer.Ordinal)
                .ToList();
            if (matches.Count == 0)
            {
                throw new MCPProjectToolException(
                    "runtime_game_item_domain_adapter_not_found",
                    $"No project domain adapter handles runtime GameItem '{gameItem?.GetType().FullName}'.");
            }

            if (matches.Count > 1 && matches[0].Priority == matches[1].Priority)
            {
                throw new MCPProjectToolException(
                    "runtime_game_item_domain_adapter_ambiguous",
                    $"Multiple project domain adapters with priority {matches[0].Priority} handle " +
                    $"runtime GameItem '{gameItem?.GetType().FullName}': " +
                    string.Join(", ", matches
                        .Where(adapter => adapter.Priority == matches[0].Priority)
                        .Select(adapter => adapter.GetType().FullName)));
            }

            return matches[0];
        }

        internal static bool TryGetAdapter(IGameItem gameItem,
            out IVMFrameworkMcpRuntimeGameItemDomainAdapter adapter)
        {
            try
            {
                adapter = GetRequiredAdapter(gameItem);
                return true;
            }
            catch (MCPProjectToolException exception)
                when (exception.ErrorCode == "runtime_game_item_domain_adapter_not_found")
            {
                adapter = null;
                return false;
            }
        }

        internal static IReadOnlyList<IVMFrameworkMcpRuntimeGameItemDomainAdapter> GetAdapters()
        {
            if (adapters != null)
                return adapters;

            var discovered = new List<IVMFrameworkMcpRuntimeGameItemDomainAdapter>();
            foreach (Type type in TypeCache
                         .GetTypesDerivedFrom<IVMFrameworkMcpRuntimeGameItemDomainAdapter>()
                         .Where(type => !type.IsAbstract && !type.IsInterface)
                         .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException(
                        $"Runtime GameItem domain adapter '{type.FullName}' needs a public parameterless constructor.");
                }

                discovered.Add(
                    (IVMFrameworkMcpRuntimeGameItemDomainAdapter)Activator.CreateInstance(type));
            }

            adapters = discovered;
            return adapters;
        }
    }
}
#endif
