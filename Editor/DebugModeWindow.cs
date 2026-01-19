using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Neonalig.Core.Editor
{
    public class DebuggableType
    {
        public readonly GUIContent Name;
        public readonly GUIContent Namespace;
        public readonly Type Type;
        public bool IsOn;

        private float _lastCalculatedMaxWidth = -1f;
        public float ToggleWidth { get; private set; }
        public float NamespaceWidth { get; private set; }

        public static DebuggableType From(Type type, bool isOn)
        {
            string niceName = type.CSharpName();
            string assemblyName = type.Assembly.GetName().Name;
            var content = new GUIContent(niceName)
            {
                tooltip = $"Toggle Debug Mode for {niceName}\n\nType: {type.AssemblyQualifiedName}",
                image = AssetPreview.GetMiniTypeThumbnail(type)
            };
            var nsContent = new GUIContent(assemblyName)
            {
                tooltip = type.Assembly.FullName
            };
            return new DebuggableType(content, nsContent, type, isOn);
        }

        private DebuggableType(GUIContent name, GUIContent @namespace, Type type, bool isOn)
        {
            Name = name;
            Namespace = @namespace;
            Type = type;
            IsOn = isOn;
        }

        public void CalculateWidths(float maxWidth)
        {
            if (Mathf.Approximately(maxWidth, _lastCalculatedMaxWidth)) return;
            _lastCalculatedMaxWidth = maxWidth;

            ToggleWidth = EditorStyles.toggle.CalcSize(Name).x - 200f;
            NamespaceWidth = Mathf.Min(EditorStyles.label.CalcSize(Namespace).x, maxWidth - ToggleWidth);
        }
    }

    public class DebugModeWindow : EditorWindow
    {
        private readonly List<DebuggableType> _debuggableTypes = new();
        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private bool _showOnlyEnabled = false;
        private bool _initialized = false;

        // Colors for alternate rows
        private Color _evenRowColor;
        private Color _oddRowColor;
        private GUIStyle _rowStyle;

        [MenuItem("Neonalig/View/Debug Mode Toggles")]
        public static void ShowWindow()
        {
            var window = GetWindow<DebugModeWindow>();
            window.titleContent = EditorGUIUtility.TrTextContentWithIcon("Debug Mode Toggles", "Debug");
            window.minSize = new Vector2(550, 200);
            window.ScanForDebuggableTypes();
        }

        private void OnEnable()
        {
            ScanForDebuggableTypes();

            // Initialize row colors
            _evenRowColor = EditorGUIUtility.isProSkin
                ? new Color(0.25f, 0.25f, 0.25f, 0.5f)
                : new Color(0.85f, 0.85f, 0.85f, 0.5f);
            _oddRowColor = EditorGUIUtility.isProSkin
                ? new Color(0.2f, 0.2f, 0.2f, 0.5f)
                : new Color(0.8f, 0.8f, 0.8f, 0.5f);

            // Initialize row style
            _rowStyle = new GUIStyle
            {
                padding = new RectOffset(5, 5, 2, 2),
            };

            DebugMode.IsOnChanged += OnDebugModeChanged;
        }

        private void OnDisable()
        {
            DebugMode.IsOnChanged -= OnDebugModeChanged;
        }

        private void OnDebugModeChanged(string key, bool isOn)
        {
            // Update the cached state if it exists
            foreach (DebuggableType debuggableType in _debuggableTypes)
            {
                if (DebugMode.GetTypeName(debuggableType.Type) == key)
                {
                    debuggableType.IsOn = isOn;
                    Repaint();
                    break;
                }
            }
        }

        private void OnGUI()
        {
            if (!_initialized)
            {
                ScanForDebuggableTypes();
            }

            DrawToolbar();
            DrawTypesList();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search field
            string newSearch = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            _searchFilter = newSearch;
            GUIContent clearButtonContent = EditorGUIUtility.TrIconContent("CrossIcon", "Clear Search");
            if (!string.IsNullOrEmpty(_searchFilter) && GUILayout.Button(clearButtonContent, EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                _searchFilter = "";
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();

            // Show only enabled toggle
            bool newShowOnlyEnabled = GUILayout.Toggle(_showOnlyEnabled, "Show Only Enabled", EditorStyles.toolbarButton);
            _showOnlyEnabled = newShowOnlyEnabled;

            // Refresh button
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                ScanForDebuggableTypes();
            }

            // Enable/Disable All buttons
            if (GUILayout.Button("Enable All", EditorStyles.toolbarButton))
            {
                SetAllDebugModes(true);
            }

            if (GUILayout.Button("Disable All", EditorStyles.toolbarButton))
            {
                SetAllDebugModes(false);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTypesList()
        {
            EditorGUILayout.Space();

            if (_debuggableTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("No debuggable types found.", MessageType.Info);
                return;
            }

            // Filter types based on search and only-enabled filter
            List<DebuggableType> filteredTypes = _debuggableTypes
                .Where(item =>
                    (string.IsNullOrEmpty(_searchFilter) ||
                     item.Name.text.ToLower().Contains(_searchFilter.ToLower())) &&
                    (!_showOnlyEnabled || item.IsOn))
                .ToList();

            if (filteredTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("No types match the current filters.", MessageType.Info);
                return;
            }

            // Create a scrollable area with fixed height
            Rect listArea = EditorGUILayout.GetControlRect(false, GUILayout.ExpandHeight(true));
            _scrollPosition = GUI.BeginScrollView(listArea, _scrollPosition,
                new Rect(0, 0, listArea.width - 16, filteredTypes.Count * EditorGUIUtility.singleLineHeight * 1.5f));

            float lineHeight = EditorGUIUtility.singleLineHeight * 1.5f;
            Color defaultBgColor = GUI.backgroundColor;

            for (int i = 0; i < filteredTypes.Count; i++)
            {
                DebuggableType item = filteredTypes[i];
                Rect rowRect = new Rect(0, i * lineHeight, listArea.width - 16, lineHeight);

                // Alternate row colors
                GUI.backgroundColor = i % 2 == 0 ? _evenRowColor : _oddRowColor;
                GUI.Box(rowRect, GUIContent.none, _rowStyle);
                GUI.backgroundColor = defaultBgColor;

                // Create toggle with type name and icon
                Rect toggleRect = new Rect(rowRect.x + 5, rowRect.y + (rowRect.height - EditorGUIUtility.singleLineHeight) / 2,
                    rowRect.width - 10, EditorGUIUtility.singleLineHeight);

                const float spacing = 5f;
                item.CalculateWidths(toggleRect.width - spacing);
                float toggleWidth = item.ToggleWidth;
                Rect actualToggleRect = new Rect(toggleRect.x, toggleRect.y, toggleWidth, toggleRect.height);
                var oldContentColor = GUI.contentColor;
                GUI.contentColor = item.IsOn ? oldContentColor : Color.grey;
                bool newIsOn = EditorGUI.ToggleLeft(actualToggleRect, item.Name, item.IsOn);
                float availableWidth = toggleRect.width - toggleWidth - spacing;
                float nsWidth = Mathf.Min(item.NamespaceWidth, availableWidth);
                Rect namespaceRect = new Rect(toggleRect.xMax - nsWidth, toggleRect.y, nsWidth, toggleRect.height);
                GUI.contentColor = Color.grey;
                EditorGUI.LabelField(namespaceRect, item.Namespace);
                GUI.contentColor = oldContentColor;

                if (newIsOn != item.IsOn)
                {
                    DebugMode.SetIsOn(item.Type, newIsOn);
                    item.IsOn = newIsOn;
                }
            }

            GUI.EndScrollView();
        }

        private void ScanForDebuggableTypes()
        {
            _debuggableTypes.Clear();

            foreach (var debugType in DebugMode.Editor_FindAllDebuggableTypes()
                .OrderBy(t => t.CSharpName()))
            {
                bool isOn = DebugMode.IsOn(debugType);
                _debuggableTypes.Add(DebuggableType.From(debugType, isOn));
            }

            _initialized = true;
        }

        private void SetAllDebugModes(bool enabled)
        {
            foreach (DebuggableType debuggableType in _debuggableTypes)
            {
                DebugMode.SetIsOn(debuggableType.Type, enabled);
                debuggableType.IsOn = enabled;
            }
        }
    }
}
