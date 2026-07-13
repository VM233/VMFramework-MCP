#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;
using VMFramework.GameLogicArchitecture;
using Object = UnityEngine.Object;

namespace VMFramework.MCP.Editor
{
    public static partial class VMFrameworkMcpTools
    {
        private const string INSPECT_GAME_PREFAB_TOOL_NAME = "vmframework/inspect-game-prefab";
        private const string UPDATE_GAME_PREFAB_TOOL_NAME = "vmframework/update-game-prefab";

        private const string INSPECT_GAME_PREFAB_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"id\":{\"type\":\"string\",\"description\":\"Exact GamePrefab id.\"}," +
            "\"maxDepth\":{\"type\":\"integer\",\"description\":\"Maximum nested depth, 1-16. Defaults to 8.\"}," +
            "\"maxCollectionItems\":{\"type\":\"integer\",\"description\":\"Maximum items per collection, 1-1000. Defaults to 100.\"}" +
            "},\"required\":[\"id\"]}";

        private const string UPDATE_GAME_PREFAB_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"id\":{\"type\":\"string\",\"description\":\"Exact existing GamePrefab id.\"}," +
            "\"operations\":{\"type\":\"array\",\"description\":\"Ordered atomic operations. Types: set, append, insert, remove, clear. Paths support members and [index].\",\"items\":{\"type\":\"object\",\"additionalProperties\":true}}," +
            "\"maxDepth\":{\"type\":\"integer\",\"description\":\"Inspection and diff depth. Defaults to 8.\"}," +
            "\"maxCollectionItems\":{\"type\":\"integer\",\"description\":\"Inspection collection limit. Defaults to 100.\"}" +
            "},\"required\":[\"id\",\"operations\"]}";

        [MCPProjectTool(INSPECT_GAME_PREFAB_TOOL_NAME,
            Description = "Inspect the full serialized contents of one VMFramework GamePrefab, including nested configs, lists, arrays, Odin fields, and Unity asset references.",
            InputSchemaJson = INSPECT_GAME_PREFAB_SCHEMA,
            ReadOnly = true)]
        public static object InspectGamePrefab(Dictionary<string, object> args)
        {
            args ??= new();
            var info = GetSingleGamePrefabInfo(GetRequiredString(args, "id"));
            var maxDepth = Math.Max(1, Math.Min(16, GetInt(args, "maxDepth", 8)));
            var maxItems = Math.Max(1, Math.Min(1000, GetInt(args, "maxCollectionItems", 100)));
            return new Dictionary<string, object>
            {
                { "gamePrefab", DescribeSerializedValue(info.gamePrefab, 0, maxDepth, maxItems,
                    new HashSet<object>(ReferenceComparer.Instance)) },
                { "wrapper", DescribeWrapper(info.wrapper, includeGamePrefabs: false) },
                { "generalSetting", DescribeGeneralSetting(GetGamePrefabGeneralSetting(info.gamePrefab.GetType()), false) }
            };
        }

        [MCPProjectTool(UPDATE_GAME_PREFAB_TOOL_NAME,
            Description = "Atomically update an existing GamePrefab inside its Wrapper with nested paths, list/array/set edits, Unity asset references, Odin-serialized objects, and before/after diff.",
            InputSchemaJson = UPDATE_GAME_PREFAB_SCHEMA,
            MutatesAssets = true)]
        public static object UpdateGamePrefab(Dictionary<string, object> args)
        {
            args ??= new();
            var id = GetRequiredString(args, "id");
            var operations = GetDictionaryListValue(args, "operations");
            if (operations.Count == 0)
            {
                throw new ArgumentException("operations must contain at least one operation.");
            }

            var info = GetSingleGamePrefabInfo(id);
            var wrapperPath = info.wrapperPath;
            var absolutePath = Path.GetFullPath(wrapperPath);
            var snapshot = File.ReadAllBytes(absolutePath);
            var maxDepth = Math.Max(1, Math.Min(16, GetInt(args, "maxDepth", 8)));
            var maxItems = Math.Max(1, Math.Min(1000, GetInt(args, "maxCollectionItems", 100)));
            var before = DescribeSerializedValue(info.gamePrefab, 0, maxDepth, maxItems,
                new HashSet<object>(ReferenceComparer.Instance));
            var summaries = new List<Dictionary<string, object>>();

            try
            {
                for (var i = 0; i < operations.Count; i++)
                {
                    summaries.Add(ApplyGamePrefabOperation(info.gamePrefab, operations[i], i));
                }

                EditorUtility.SetDirty(info.wrapper);
                AssetDatabase.SaveAssetIfDirty(info.wrapper);
                AssetDatabase.ImportAsset(wrapperPath, ImportAssetOptions.ForceUpdate);
                RefreshGamePrefabRegistry();

                var refreshedInfo = GetSingleGamePrefabInfo(id);
                var after = DescribeSerializedValue(refreshedInfo.gamePrefab, 0, maxDepth, maxItems,
                    new HashSet<object>(ReferenceComparer.Instance));
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "id", id },
                    { "wrapperPath", wrapperPath },
                    { "operationCount", operations.Count },
                    { "operations", summaries },
                    { "before", before },
                    { "after", after },
                    { "diff", BuildValueDiff(before, after) }
                };
            }
            catch
            {
                File.WriteAllBytes(absolutePath, snapshot);
                AssetDatabase.ImportAsset(wrapperPath, ImportAssetOptions.ForceUpdate);
                RefreshGamePrefabRegistry();
                throw;
            }
        }

        private static GamePrefabInfo GetSingleGamePrefabInfo(string id)
        {
            RefreshGamePrefabRegistry();
            var infos = FindGamePrefabInfos(id, null, null, int.MaxValue);
            if (infos.Count != 1)
            {
                throw new InvalidOperationException(infos.Count == 0
                    ? $"GamePrefab '{id}' was not found."
                    : $"GamePrefab '{id}' exists in {infos.Count} wrappers; an exact single target is required.");
            }

            return infos[0];
        }

        private static Dictionary<string, object> ApplyGamePrefabOperation(object root,
            Dictionary<string, object> operation, int index)
        {
            var type = GetFirstStringValue(operation, "type", "op", "action").ToLowerInvariant();
            var path = GetRequiredString(operation, "path");
            object before;
            object after;
            switch (type)
            {
                case "set":
                    before = GetPathValue(root, path);
                    SetPathValue(root, path, operation.TryGetValue("value", out var setValue) ? setValue : null);
                    after = GetPathValue(root, path);
                    break;
                case "append":
                    before = DescribeSimpleCollection(GetPathValue(root, path));
                    InsertCollectionValue(root, path, int.MaxValue,
                        operation.TryGetValue("value", out var appendValue) ? appendValue : null);
                    after = DescribeSimpleCollection(GetPathValue(root, path));
                    break;
                case "insert":
                    before = DescribeSimpleCollection(GetPathValue(root, path));
                    InsertCollectionValue(root, path, GetInt(operation, "index", -1),
                        operation.TryGetValue("value", out var insertValue) ? insertValue : null);
                    after = DescribeSimpleCollection(GetPathValue(root, path));
                    break;
                case "remove":
                    before = DescribeSimpleCollection(GetPathValue(root, path));
                    RemoveCollectionValue(root, path, GetInt(operation, "index", -1));
                    after = DescribeSimpleCollection(GetPathValue(root, path));
                    break;
                case "clear":
                    before = DescribeSimpleCollection(GetPathValue(root, path));
                    ClearCollection(root, path);
                    after = DescribeSimpleCollection(GetPathValue(root, path));
                    break;
                default:
                    throw new InvalidOperationException($"Operation {index}: unsupported type '{type}'.");
            }

            return new Dictionary<string, object>
            {
                { "index", index },
                { "type", type },
                { "path", path },
                { "before", DescribeLeaf(before) },
                { "after", DescribeLeaf(after) }
            };
        }

        private static object GetPathValue(object root, string path)
        {
            object current = root;
            foreach (var segment in ParsePath(path))
            {
                current = GetMemberValue(current, segment.Name);
                if (segment.Index.HasValue)
                {
                    current = GetListItem(current, segment.Index.Value, path);
                }
            }

            return current;
        }

        private static void SetPathValue(object root, string path, object rawValue)
        {
            var segments = ParsePath(path);
            if (segments.Count == 0)
            {
                throw new ArgumentException("path is empty.");
            }

            SetPathValueRecursive(root, segments, 0, rawValue, path);
        }

        private static object SetPathValueRecursive(object current, IReadOnlyList<PathSegment> segments,
            int segmentIndex, object rawValue, string path)
        {
            var segment = segments[segmentIndex];
            var isLast = segmentIndex == segments.Count - 1;
            if (segment.Index.HasValue)
            {
                var collection = GetMemberValue(current, segment.Name);
                if (isLast)
                {
                    SetListItem(collection, segment.Index.Value, rawValue, path);
                    return current;
                }

                var item = GetListItem(collection, segment.Index.Value, path);
                var updatedItem = SetPathValueRecursive(item, segments, segmentIndex + 1, rawValue, path);
                if ((item != null && item.GetType().IsValueType) || !ReferenceEquals(item, updatedItem))
                {
                    SetListItem(collection, segment.Index.Value, updatedItem, path);
                }

                return current;
            }

            if (isLast)
            {
                SetMemberValue(current, segment.Name, rawValue, path);
                return InvokeAfterDeserialize(current);
            }

            var child = GetMemberValue(current, segment.Name);
            var updatedChild = SetPathValueRecursive(child, segments, segmentIndex + 1, rawValue, path);
            if ((child != null && child.GetType().IsValueType) || !ReferenceEquals(child, updatedChild))
            {
                SetMemberValue(current, segment.Name, updatedChild, path);
            }

            return current;
        }

        private static void InsertCollectionValue(object root, string path, int index, object rawValue)
        {
            var collection = GetPathValue(root, path);
            var elementType = GetCollectionElementType(collection.GetType());
            var converted = ConvertSerializedValue(rawValue, elementType, path);
            if (collection is Array array)
            {
                var targetIndex = index == int.MaxValue ? array.Length : index;
                if (targetIndex < 0 || targetIndex > array.Length)
                {
                    throw new IndexOutOfRangeException($"Index {targetIndex} is invalid for '{path}'.");
                }

                var replacement = Array.CreateInstance(elementType, array.Length + 1);
                Array.Copy(array, 0, replacement, 0, targetIndex);
                replacement.SetValue(converted, targetIndex);
                Array.Copy(array, targetIndex, replacement, targetIndex + 1, array.Length - targetIndex);
                SetPathValue(root, path, replacement);
                return;
            }

            if (collection is not IList list)
            {
                if (index != int.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"'{path}' is an unordered collection and does not support indexed insert.");
                }

                AddCollectionItem(collection, converted, elementType, path);
                return;
            }

            if (index == int.MaxValue)
            {
                list.Add(converted);
            }
            else
            {
                if (index < 0 || index > list.Count)
                {
                    throw new IndexOutOfRangeException($"Index {index} is invalid for '{path}'.");
                }

                list.Insert(index, converted);
            }
        }

        private static void RemoveCollectionValue(object root, string path, int index)
        {
            var collection = GetPathValue(root, path);
            if (collection is Array array)
            {
                if (index < 0 || index >= array.Length)
                {
                    throw new IndexOutOfRangeException($"Index {index} is invalid for '{path}'.");
                }

                var arrayElementType = GetCollectionElementType(array.GetType());
                var replacement = Array.CreateInstance(arrayElementType, array.Length - 1);
                Array.Copy(array, 0, replacement, 0, index);
                Array.Copy(array, index + 1, replacement, index, array.Length - index - 1);
                SetPathValue(root, path, replacement);
                return;
            }

            if (collection is IList list)
            {
                if (index < 0 || index >= list.Count)
                {
                    throw new IndexOutOfRangeException($"Index {index} is invalid for '{path}'.");
                }

                list.RemoveAt(index);
                return;
            }

            var elementType = GetCollectionElementType(collection.GetType());
            var item = GetCollectionItem(collection, index, path);
            RemoveCollectionItem(collection, item, elementType, path);
        }

        private static void ClearCollection(object root, string path)
        {
            var collection = GetPathValue(root, path);
            if (collection is Array array)
            {
                SetPathValue(root, path, Array.CreateInstance(GetCollectionElementType(array.GetType()), 0));
            }
            else if (collection is IList list)
            {
                list.Clear();
            }
            else
            {
                ClearCollectionItems(collection, GetCollectionElementType(collection.GetType()), path);
            }
        }

        private static object GetMemberValue(object target, string name)
        {
            if (target == null)
            {
                throw new NullReferenceException($"Cannot read member '{name}' from null.");
            }

            var member = FindMember(target.GetType(), name);
            return member switch
            {
                FieldInfo field => field.GetValue(target),
                PropertyInfo property => property.GetValue(target),
                _ => throw new MissingMemberException(target.GetType().FullName, name)
            };
        }

        private static void SetMemberValue(object target, string name, object rawValue, string path)
        {
            var member = FindMember(target.GetType(), name);
            switch (member)
            {
                case FieldInfo field:
                    field.SetValue(target, ConvertSerializedValue(rawValue, field.FieldType, path));
                    return;
                case PropertyInfo property when property.CanWrite:
                    property.SetValue(target, ConvertSerializedValue(rawValue, property.PropertyType, path));
                    return;
                default:
                    throw new MissingMemberException(target.GetType().FullName, name);
            }
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                var property = current.GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property;
                }
            }

            return null;
        }

        private static object ConvertSerializedValue(object value, Type targetType, string path)
        {
            if (value == null)
            {
                return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null
                    ? Activator.CreateInstance(targetType)
                    : null;
            }

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                targetType = nullableType;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (typeof(Object).IsAssignableFrom(targetType))
            {
                var assetPath = value is string stringPath
                    ? stringPath
                    : value is Dictionary<string, object> reference && reference.TryGetValue("assetPath", out var rawPath)
                        ? rawPath?.ToString()
                        : null;
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    throw new InvalidOperationException($"Unity Object '{path}' requires an asset path string or {{assetPath}}.");
                }

                var candidates = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var asset = candidates.FirstOrDefault(targetType.IsInstanceOfType);
                return asset ?? throw new InvalidOperationException(
                    $"No asset at '{assetPath}' is assignable to '{targetType.FullName}'.");
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value.ToString(), true);
            }

            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            if (targetType.IsPrimitive || targetType == typeof(decimal))
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            if (value is Dictionary<string, object> objectValues)
            {
                var concreteType = targetType;
                if (objectValues.TryGetValue("$type", out var typeName))
                {
                    concreteType = ResolveAnyType(typeName?.ToString());
                    if (concreteType == null || !targetType.IsAssignableFrom(concreteType))
                    {
                        throw new InvalidOperationException($"Type '{typeName}' is not assignable to '{targetType.FullName}'.");
                    }
                }

                var instance = Activator.CreateInstance(concreteType);
                foreach (var pair in objectValues)
                {
                    if (pair.Key == "$type") continue;
                    SetMemberValue(instance, pair.Key, pair.Value, $"{path}.{pair.Key}");
                }

                return InvokeAfterDeserialize(instance);
            }

            if (value is IEnumerable enumerable && value is not string && typeof(IEnumerable).IsAssignableFrom(targetType))
            {
                var elementType = GetCollectionElementType(targetType);
                var converted = enumerable.Cast<object>().Select(item => ConvertSerializedValue(item, elementType, path)).ToList();
                if (targetType.IsArray)
                {
                    var array = Array.CreateInstance(elementType, converted.Count);
                    for (var i = 0; i < converted.Count; i++) array.SetValue(converted[i], i);
                    return array;
                }

                var collection = CreateCollectionInstance(targetType, elementType, path);
                foreach (var item in converted)
                {
                    AddCollectionItem(collection, item, elementType, path);
                }

                return collection;
            }

            throw new InvalidOperationException($"Cannot convert '{path}' to '{targetType.FullName}'.");
        }

        private static object InvokeAfterDeserialize(object value)
        {
            if (value is ISerializationCallbackReceiver receiver)
            {
                receiver.OnAfterDeserialize();
            }

            return value;
        }

        private static Type ResolveAnyType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            var direct = Type.GetType(typeName, false, true);
            if (direct != null) return direct;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName, false, true) ??
                           GetLoadableTypes(assembly).FirstOrDefault(candidate =>
                               string.Equals(candidate.Name, typeName, StringComparison.OrdinalIgnoreCase));
                if (type != null) return type;
            }

            return null;
        }

        private static object DescribeSerializedValue(object value, int depth, int maxDepth, int maxItems,
            ISet<object> visited)
        {
            if (value == null) return null;
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string || value is decimal) return value;
            if (value is Object unityObject)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(unityObject, out var guid, out long fileID);
                return new Dictionary<string, object>
                {
                    { "$type", type.FullName }, { "name", unityObject.name },
                    { "assetPath", AssetDatabase.GetAssetPath(unityObject) }, { "guid", guid }, { "fileID", fileID }
                };
            }

            if (depth >= maxDepth) return new Dictionary<string, object> { { "$type", type.FullName }, { "$truncated", true } };
            if (!type.IsValueType && !visited.Add(value))
                return new Dictionary<string, object> { { "$type", type.FullName }, { "$cycle", true } };

            if (value is IEnumerable enumerable && IsLocalizedReference(type) == false)
            {
                var items = new List<object>();
                var total = 0;
                foreach (var item in enumerable)
                {
                    if (items.Count < maxItems)
                        items.Add(DescribeSerializedValue(item, depth + 1, maxDepth, maxItems, visited));
                    total++;
                }

                return new Dictionary<string, object>
                {
                    { "$type", type.FullName }, { "count", total }, { "items", items },
                    { "truncated", total > items.Count }
                };
            }

            var result = new Dictionary<string, object> { { "$type", type.FullName } };
            foreach (var field in GetGamePrefabSerializableFields(type))
            {
                object fieldValue;
                try { fieldValue = field.GetValue(value); }
                catch (Exception ex) { result[field.Name] = new Dictionary<string, object> { { "$error", ex.Message } }; continue; }
                result[field.Name] = DescribeSerializedValue(fieldValue, depth + 1, maxDepth, maxItems, visited);
            }

            return result;
        }

        private static bool IsLocalizedReference(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (string.Equals(current.FullName, "UnityEngine.Localization.LocalizedReference",
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<FieldInfo> GetGamePrefabSerializableFields(Type type)
        {
            var names = new HashSet<string>();
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                         BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsNotSerialized || field.Name.Contains("k__BackingField") ||
                        !names.Add(field.Name)) continue;
                    var attributes = field.GetCustomAttributesData();
                    var odinSerialized = attributes.Any(attribute => attribute.AttributeType.Name == "OdinSerializeAttribute");
                    if (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null || odinSerialized)
                        yield return field;
                }
            }
        }

        private static List<Dictionary<string, object>> BuildValueDiff(object before, object after)
        {
            var beforeFlat = new Dictionary<string, string>();
            var afterFlat = new Dictionary<string, string>();
            FlattenValue("$", before, beforeFlat);
            FlattenValue("$", after, afterFlat);
            var keys = new HashSet<string>(beforeFlat.Keys);
            keys.UnionWith(afterFlat.Keys);
            return keys.OrderBy(key => key)
                .Where(key => beforeFlat.GetValueOrDefault(key) != afterFlat.GetValueOrDefault(key))
                .Select(key => new Dictionary<string, object>
                {
                    { "path", key }, { "before", beforeFlat.GetValueOrDefault(key) },
                    { "after", afterFlat.GetValueOrDefault(key) }
                }).ToList();
        }

        private static void FlattenValue(string path, object value, IDictionary<string, string> output)
        {
            if (value is Dictionary<string, object> dictionary)
            {
                foreach (var pair in dictionary) FlattenValue($"{path}.{pair.Key}", pair.Value, output);
            }
            else if (value is IEnumerable enumerable && value is not string)
            {
                var index = 0;
                foreach (var item in enumerable) FlattenValue($"{path}[{index++}]", item, output);
                if (index == 0) output[path] = "[]";
            }
            else output[path] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
        }

        private static object DescribeSimpleCollection(object collection)
        {
            return collection is ICollection values ? new Dictionary<string, object>
            {
                { "type", collection.GetType().FullName }, { "count", values.Count }
            } : collection;
        }

        private static object DescribeLeaf(object value)
        {
            if (value == null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum) return value;
            if (value is IDictionary || value is IEnumerable && value is not string) return value;
            if (value is Object unityObject) return AssetDatabase.GetAssetPath(unityObject);
            return value.ToString();
        }

        private static object GetListItem(object collection, int index, string path)
        {
            return GetCollectionItem(collection, index, path);
        }

        private static void SetListItem(object collection, int index, object rawValue, string path)
        {
            var elementType = GetCollectionElementType(collection.GetType());
            var converted = ConvertSerializedValue(rawValue, elementType, path);
            if (collection is IList list)
            {
                if (index < 0 || index >= list.Count)
                    throw new IndexOutOfRangeException($"Index {index} is invalid in '{path}'.");
                list[index] = converted;
                return;
            }

            var previous = GetCollectionItem(collection, index, path);
            RemoveCollectionItem(collection, previous, elementType, path);
            AddCollectionItem(collection, converted, elementType, path);
        }

        private static Type GetCollectionElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            var generic = type.IsGenericType && type.GetGenericArguments().Length == 1
                ? type
                : type.GetInterfaces().FirstOrDefault(candidate => candidate.IsGenericType &&
                    (candidate.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                     candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>)));
            return generic?.GetGenericArguments()[0] ?? typeof(object);
        }

        private static object CreateCollectionInstance(Type targetType, Type elementType, string path)
        {
            if (targetType.IsInterface || targetType.IsAbstract)
            {
                bool isSet = targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(ISet<>) ||
                             targetType.GetInterfaces().Any(candidate => candidate.IsGenericType &&
                                 candidate.GetGenericTypeDefinition() == typeof(ISet<>));
                return Activator.CreateInstance((isSet ? typeof(HashSet<>) : typeof(List<>))
                    .MakeGenericType(elementType));
            }

            try
            {
                return Activator.CreateInstance(targetType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Collection '{path}' of type '{targetType.FullName}' requires a parameterless constructor.", ex);
            }
        }

        private static object GetCollectionItem(object collection, int index, string path)
        {
            if (collection is IList list)
            {
                if (index < 0 || index >= list.Count)
                    throw new IndexOutOfRangeException($"Index {index} is invalid in '{path}'.");
                return list[index];
            }

            if (index < 0 || collection is not IEnumerable enumerable)
                throw new IndexOutOfRangeException($"Index {index} is invalid in '{path}'.");

            var currentIndex = 0;
            foreach (var item in enumerable)
            {
                if (currentIndex == index) return item;
                currentIndex++;
            }

            throw new IndexOutOfRangeException($"Index {index} is invalid in '{path}'.");
        }

        private static void AddCollectionItem(object collection, object item, Type elementType, string path)
        {
            if (collection is IList list)
            {
                list.Add(item);
                return;
            }

            InvokeGenericCollectionMethod(collection, elementType, "Add", new[] { item }, path);
        }

        private static void RemoveCollectionItem(object collection, object item, Type elementType, string path)
        {
            InvokeGenericCollectionMethod(collection, elementType, "Remove", new[] { item }, path);
        }

        private static void ClearCollectionItems(object collection, Type elementType, string path)
        {
            InvokeGenericCollectionMethod(collection, elementType, "Clear", Array.Empty<object>(), path);
        }

        private static object InvokeGenericCollectionMethod(object collection, Type elementType,
            string methodName, object[] arguments, string path)
        {
            var collectionInterface = typeof(ICollection<>).MakeGenericType(elementType);
            if (!collectionInterface.IsInstanceOfType(collection))
            {
                throw new InvalidOperationException(
                    $"'{path}' of type '{collection.GetType().FullName}' is not a writable collection.");
            }

            try
            {
                return collectionInterface.GetMethod(methodName).Invoke(collection, arguments);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        private static List<PathSegment> ParsePath(string path)
        {
            var result = new List<PathSegment>();
            foreach (var part in path.Split('.'))
            {
                var open = part.IndexOf('[');
                if (open < 0)
                {
                    result.Add(new PathSegment(part, null));
                    continue;
                }

                var close = part.IndexOf(']', open + 1);
                if (close < 0 || !int.TryParse(part.Substring(open + 1, close - open - 1), out var index))
                    throw new FormatException($"Invalid path segment '{part}'.");
                result.Add(new PathSegment(part.Substring(0, open), index));
            }

            return result;
        }

        private readonly struct PathSegment
        {
            public readonly string Name;
            public readonly int? Index;
            public PathSegment(string name, int? index) { Name = name; Index = index; }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private static List<Dictionary<string, object>> GetDictionaryListValue(
            IReadOnlyDictionary<string, object> args, string key)
        {
            var result = new List<Dictionary<string, object>>();
            if (!args.TryGetValue(key, out var raw) || raw is not IEnumerable values) return result;
            foreach (var value in values)
            {
                if (value is Dictionary<string, object> dictionary) result.Add(dictionary);
            }

            return result;
        }

        private static string GetFirstStringValue(IReadOnlyDictionary<string, object> args, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (args.TryGetValue(key, out var value) && value != null &&
                    !string.IsNullOrWhiteSpace(value.ToString())) return value.ToString();
            }

            return "";
        }
    }
}
#endif
