using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Neonalig.Core.Editor
{
    public static class DebugModeContextMenu
    {
        [InitializeOnLoadMethod]
        private static void SubscribeToEditorEvents()
        {
            EditorApplication.contextualPropertyMenu -= ContextualPropertyMenu;
            EditorApplication.contextualPropertyMenu += ContextualPropertyMenu;
        }

        private static void ContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
        {
            // Debug.Log($"ContextualPropertyMenu called - type: {property.propertyType}, name: {property.name}");
            if (property.propertyType != SerializedPropertyType.ObjectReference) return;
            var obj = property.objectReferenceValue;
            if (obj == null)
            {
                // Debug.Log("Object reference is null.");
                return;
            }
            var type = obj.GetType();
            if (!DebugMode.HasDebugMode(type))
            {
                // Debug.Log($"Type {type.Name} does not have HasDebugModeAttribute.");
                return;
            }

            var key = type.Name;
            var isOn = DebugMode.IsOn(key);
            menu.AddItem(new GUIContent($"{(isOn ? "Disable" : "Enable")} Debug Mode"), isOn, () =>
            {
                DebugMode.SetIsOn(key, !isOn);
                Debug.Log($"{(isOn ? "Disabled" : "Enabled")} Debug Mode for {key}");
            });
        }
    }
}
