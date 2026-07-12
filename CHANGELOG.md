# Changelog

## 1.0.3

- Added HashSet and generic ICollection support to GamePrefab collection conversion, append, remove, clear, and indexed replacement operations.

## 1.0.2

- Convert structured object dictionaries before enumerable values so LocalizedString and other serialized objects that implement IEnumerable are updated correctly.

## 1.0.1

- Treat empty VisualElementPath fields as valid when they are optional, while preserving errors for fields marked with IsNotNullOrEmpty.

## 1.0.0

- Added VMFramework MCP project tools for GamePrefab creation/inspection, general settings inspection, UI panel inspection, bind object inspection, VisualElementPath validation, container panel inspection, and property manager inspection.
