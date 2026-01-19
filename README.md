# Neonalig Core

Core utilities for Unity projects: debugging infrastructure, singleton patterns, and serialization helpers.

## Features

* **DebugMode** - Persistent runtime debug toggles
* **SingletonMB** - Domain Reload-safe MonoBehaviour singleton base
* **SerializedType** - Inspector-serializable `System.Type`
* **SerializedMethodReference** - Method references in the Inspector
- **Enum**: Utility methods for working with enums 

## Usage

### Debug Mode

```csharp
using Neonalig.Core;
using UnityEngine;

[HasDebugMode]
public class MyComponent : MonoBehaviour
{
    void Update()
    {
        if (DebugMode.IsOn<MyComponent>())
        {
            // Debug-only logic
        }
    }
}
```

Debug toggles are accessible via
**Neonalig ▸ View ▸ Debug Mode Toggles**

### SingletonMB

```csharp
using Neonalig.Core;

public class GameManager : SingletonMB<GameManager>
{
    protected override void OnAwake()
    {
        // Initialization
    }
}
```

## Dependencies

This package is dependent on [com.neonalig.polyfills](https://github.com/Neonalig/com.neonalig.polyfills).

Ensure you have installed that package prior to this one, or your project will not be able to be compiled.

## Installation

### Option 1 - Package Manager (Recommended)

1. Open **Window ▸ Package Manager**
2. Click **➕**
3. Select **Install package from Git URL…**
4. Paste:

```
https://github.com/Neonalig/com.neonalig.core.git#v1.0.0
```

Supported suffixes:

* `#v1.0.0` – tag
* `#main` – branch
* `#<commit-hash>` – exact commit

> **Tip:** Using a tag or commit hash is recommended for reproducible builds.

---

### Option 2 - `manifest.json`

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.neonalig.attributes": "https://github.com/Neonalig/com.neonalig.core.git#v1.0.0"
  }
}
```

---

### Option 3 - Scoped Dependency

If you are consuming this from a local package or a scoped registry, use the package name directly:

```json
{
  "dependencies": {
    "com.neonalig.core": "1.0.0"
  }
}
```

### Requirements

* Unity **2021.3 LTS** or newer