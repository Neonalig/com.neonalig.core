#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Neonalig.Core
{
    public static class DebugMode
    {
        private static readonly Dictionary<string, bool> _cache = new();

        public readonly struct PredefinedDebugType
        {
            public readonly string Name;
            public readonly string Namespace;
            public readonly string Assembly;

            public string FullName => $"{Namespace}.{Name}, {Assembly}";
            public Type? Type
            {
                get
                {
                    var type = Type.GetType(FullName);
                    if (type == null)
                    {
                        Debug.LogError($"[DebugMode] Predefined debug type '{FullName}' not found.");

                        // // Debug: See if assembly exists
                        // Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                        // string @as = Assembly;
                        // if (assemblies.All(a => a.GetName().Name != @as))
                        // {
                        //     Debug.LogWarning($"[DebugMode] Assembly '{Assembly}' not found in current AppDomain.");
                        //     return null;
                        // }
                        // else
                        // {
                        //     Debug.LogWarning($"[DebugMode] Assembly '{Assembly}' found in current AppDomain.");
                        // }
                        //
                        // // Debug: See if type exists in any loaded assembly
                        // string ns = Namespace;
                        // string nm = Name;
                        // var foundType = assemblies.SelectMany(a => a.GetTypes())
                        //     .FirstOrDefault(t => t.FullName == $"{ns}.{nm}");
                        // if (foundType != null)
                        // {
                        //     Debug.LogWarning($"[DebugMode] Type '{Namespace}.{Name}' found in assembly '{foundType.Assembly.GetName().Name}' instead of expected '{Assembly}'.");
                        // }
                        // else
                        // {
                        //     Debug.LogWarning($"[DebugMode] Type '{Namespace}.{Name}' not found in any loaded assembly.");
                        // }
                    }
                    return type;
                }
            }

            public PredefinedDebugType(string name, string @namespace, string assembly)
            {
                Name = name;
                Namespace = @namespace;
                Assembly = assembly;
            }
        }

        private static readonly PredefinedDebugType[] _predefinedDebugKeys =
        {
            // new("DebugLogManager", "IngameDebugConsole", "IngameDebugConsole.Runtime"),
        };

        private static Type[] _predefinedDebugTypes = Array.Empty<Type>();
        private static bool _foundPredefinedTypes = false;

        private static Type[] GetPredefinedDebugTypes()
        {
            if (_foundPredefinedTypes) return _predefinedDebugTypes;
            _foundPredefinedTypes = true;

            return _predefinedDebugTypes = _predefinedDebugKeys
                .Select(pdk => pdk.Type)
                .Where(t => t != null)
                .ToArray()!;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            _cache.Clear();
            _predefinedDebugTypes = Array.Empty<Type>();
            _foundPredefinedTypes = false;
            IsOnChanged = null;
        }

        [Conditional("DEBUG")]
        private static void SetPersistentBool(string key, bool value)
        {
#if DEBUG && UNITY_EDITOR
            EditorPrefs.SetInt($"DebugMode_{key}", value ? 1 : 0);
#elif DEBUG
            PlayerPrefs.SetInt($"DebugMode_{key}", value ? 1 : 0);
            PlayerPrefs.Save();
#endif
        }

        private static bool GetPersistentBool(string key, bool defaultValue = false)
        {
#if DEBUG && UNITY_EDITOR
            return EditorPrefs.GetInt($"DebugMode_{key}", defaultValue ? 1 : 0) != 0;
#elif DEBUG
            return PlayerPrefs.GetInt($"DebugMode_{key}", defaultValue ? 1 : 0) != 0;
#else
            return false;
#endif
        }

        public static string GetTypeName(Type type) => type.Name;
        public delegate void IsOnChangedEventHandler(string key, bool isOn);
        public static event IsOnChangedEventHandler? IsOnChanged;

        public static bool IsOn(string key, bool @default = false)
        {
            if (_cache.TryGetValue(key, out bool value))
            {
                return value;
            }
            value = Debug.isDebugBuild && GetPersistentBool(key, @default);
            _cache[key] = value;
            return value;
        }
        public static bool IsOn(Type type, bool @default = false) => IsOn(GetTypeName(type), @default);
        public static bool IsOn<T>(bool @default = false) => IsOn(typeof(T), @default);
        public static bool IsOn(object obj, bool @default = false) => IsOn(obj.GetType(), @default);

        public static void SetIsOn(string key, bool isOn)
        {
            bool oldValue = IsOn(key);
            if (oldValue == isOn) return;

            _cache[key] = isOn;
            SetPersistentBool(key, isOn);
            IsOnChanged?.Invoke(key, isOn);
        }
        public static void SetIsOn(Type type, bool isOn) => SetIsOn(GetTypeName(type), isOn);
        public static void SetIsOn<T>(bool isOn) => SetIsOn(typeof(T), isOn);
        public static void SetIsOn(object obj, bool isOn) => SetIsOn(obj.GetType(), isOn);

        public static bool HasDebugMode(Type type)
        {
            return Attribute.IsDefined(type, typeof(HasDebugModeAttribute)) || GetPredefinedDebugTypes().Contains(type);
        }

#if UNITY_EDITOR
        public static IEnumerable<Type> Editor_FindAllDebuggableTypes() => TypeCache.GetTypesWithAttribute<HasDebugModeAttribute>().Concat(GetPredefinedDebugTypes()).Distinct();
#endif
    }

    [Conditional("DEBUG")]
    [AttributeUsage(AttributeTargets.Class)]
    public class HasDebugModeAttribute : Attribute
    {
    }

}
