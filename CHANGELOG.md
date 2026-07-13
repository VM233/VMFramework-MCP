# Changelog

## 1.0.5

- Add first-class GameTag listing, upsert, localization maintenance, and validation tools.
- Stop VisualElementPath scanning at nested Unity object references so missing Transforms and other enumerable Unity objects cannot abort validation.

## 1.0.4

- Inspect LocalizedString values as structured localized references instead of empty enumerable collections.

## 1.0.3

- Added HashSet and generic ICollection support to GamePrefab collection conversion, append, remove, clear, and indexed replacement operations.

## 1.0.2

- Convert structured object dictionaries before enumerable values so LocalizedString and other serialized objects that implement IEnumerable are updated correctly.

## 1.0.1

- Treat empty VisualElementPath fields as valid when they are optional, while preserving errors for fields marked with IsNotNullOrEmpty.

## 1.0.0

- Added VMFramework MCP project tools for GamePrefab creation/inspection, general settings inspection, UI panel inspection, bind object inspection, VisualElementPath validation, container panel inspection, and property manager inspection.
