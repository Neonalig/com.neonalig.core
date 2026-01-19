# Neonalig Core

Core utilities for Unity including debug management, singleton patterns, and serialization helpers.

## Features

- **DebugMode**: Persistent debug toggle system with editor window
- **SingletonMB**: Base class for singleton MonoBehaviours
- **SerializedType**: Serialize Type references in the inspector
- **SerializedMethodReference**: Serialize method references
- **Enum**: Utility methods for working with enums

## Usage

### Debug Mode

```csharp
using Neonalig.Core;

[HasDebugMode]
public class MyComponent : MonoBehaviour
{
    void Update()
    {
        if (DebugMode.IsOn<MyComponent>())
        {
            // Debug-only code
        }
    }
}
```

Access debug toggles via `Neonalig > View > Debug Mode Toggles` menu.

### Singleton

```csharp
using Neonalig.Core;

public class GameManager : SingletonMB<GameManager>
{
    protected override void OnAwake()
    {
        // Initialization code
    }
}

// Access from anywhere
GameManager.Instance.DoSomething();
```

## Installation

```json
{
  "dependencies": {
    "com.neonalig.core": "1.0.0",
    "com.neonalig.polyfills": "1.0.0"
  }
}
```
