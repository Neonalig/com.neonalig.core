#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Neonalig.Core.Editor
{
    [CustomPreview(typeof(Component))]
    public class DebugModePreview : ObjectPreview
    {
        private (GUIContent name, Type type, bool isOn, float width)[] _debuggableTypes = { };

        // Cache for layout calculations
        private readonly List<List<(GUIContent name, Type type, bool isOn, float width)>> _cachedLines = new();
        private float _cachedTotalHeight;
        private Vector2 _lastRectSize = Vector2.zero;
        private bool _layoutCacheValid = false;
        private bool _initialized = false;

        // Cache for HasPreviewGUI results
        private Object? _cachedTarget;
        private bool _cachedHasPreviewGUI;

        /// <inheritdoc />
        public override bool HasPreviewGUI()
        {
            // Return cached result if target hasn't changed
            if (_cachedTarget == target)
            {
                return _cachedHasPreviewGUI;
            }

            // Debug.Log($"HasPreviewGUI called for {target.GetType().Name}");

            // Only return true if we are the *first* type on the GameObject with DebugMode
            Component[] allComponents = ((Component)target).gameObject.GetComponents<Component>();
            foreach (var c in allComponents)
            {
                if (!c) continue;
                var t = c.GetType();
                if (DebugMode.HasDebugMode(t))
                {
                    // Cache the result and target
                    _cachedTarget = target;
                    _cachedHasPreviewGUI = (t == target.GetType());
                    return _cachedHasPreviewGUI;
                }
            }

            // Cache the result and target
            _cachedTarget = target;
            _cachedHasPreviewGUI = false;
            return _cachedHasPreviewGUI;
        }

        /// <inheritdoc />
        public override GUIContent GetPreviewTitle() => new("Debug Mode");

        /// <summary>
        /// Initializes the component data
        /// </summary>
        private void InitializeComponentData()
        {
            // Find all components on the same GameObject that have DebugMode
            Component[] allComponents = ((Component)target).gameObject.GetComponents<Component>();

            _debuggableTypes = allComponents
                .Where(mb => mb != null && DebugMode.HasDebugMode(mb.GetType()))
                .GroupBy(mb => mb.GetType()) // Group by component type
                .Select(group => group.First()) // Take only the first instance of each type
                .Select(mb =>
                    {
                        var t = mb.GetType();
                        var key = t.Name;
                        var isOn = DebugMode.IsOn(key);
                        var content = new GUIContent(t.CSharpName())
                        {
                            tooltip = $"Toggle Debug Mode for {t.CSharpFileName(true)}",
                            image = AssetPreview.GetMiniTypeThumbnail(t)
                        };
                        var width = EditorStyles.toolbarButton.CalcSize(content).x;
                        return (content, t, isOn, width);
                    }
                )
                .ToArray();

            _initialized = true;
            _layoutCacheValid = false;
        }

        /// <summary>
        /// Calculates the layout for buttons and caches the result
        /// </summary>
        private void CalculateLayout(float maxWidth, float lineHeight, float buttonSpacing)
        {
            int lineCount = 0;
            float currentLineWidth = 0f;
            _cachedLines.Clear();
            List<(GUIContent name, Type type, bool isOn, float width)> currentLine = new();

            foreach ((GUIContent name, Type type, bool isOn, float width) debuggableType in _debuggableTypes)
            {
                if (currentLineWidth + debuggableType.width > maxWidth && currentLine.Count > 0)
                {
                    // Complete current line and start a new one
                    _cachedLines.Add(currentLine);
                    currentLine = new List<(GUIContent, Type, bool, float)>();
                    currentLineWidth = 0f;
                    lineCount++;
                }

                currentLine.Add(debuggableType);
                currentLineWidth += debuggableType.width + buttonSpacing;
            }

            // Add the last line if it has any buttons
            if (currentLine.Count > 0)
            {
                _cachedLines.Add(currentLine);
                lineCount++;
            }

            // Calculate total height needed
            _cachedTotalHeight = lineCount * lineHeight + ((lineCount - 1) * buttonSpacing);

            // Mark cache as valid
            _layoutCacheValid = true;
        }

        /// <inheritdoc />
        public override void Initialize(Object[] targets)
        {
            base.Initialize(targets);
            _initialized = false;
            _layoutCacheValid = false;
            _cachedTarget = null;
            _cachedHasPreviewGUI = false;

            DebugMode.IsOnChanged += OnDebugModeChanged;
        }

        /// <inheritdoc />
        public override void Cleanup()
        {
            base.Cleanup();
            DebugMode.IsOnChanged -= OnDebugModeChanged;
        }

        private bool _ignoreDebugModeChange = false;
        private void OnDebugModeChanged(string key, bool isOn)
        {
            if (_ignoreDebugModeChange) return;

            // Update the cached state if it exists
            for (int i = 0; i < _debuggableTypes.Length; i++)
            {
                if (DebugMode.GetTypeName(_debuggableTypes[i].type) == key)
                {
                    _debuggableTypes[i] = (_debuggableTypes[i].name, _debuggableTypes[i].type, isOn, _debuggableTypes[i].width);
                    _layoutCacheValid = false; // Invalidate layout cache to recalculate

                    // Debug.Log($"{(isOn ? "Enabled" : "Disabled")} Debug Mode for {key} via external change");

                    // Get the inspector window and repaint it
                    var inspectorWindow = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow"));
                    inspectorWindow?.Repaint();
                    break;
                }
            }
        }

        /// <inheritdoc />
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            const float padding = 10f;
            const float lineHeight = 20f;
            const float buttonSpacing = 2f; // Space between buttons
            float maxWidth = r.width - (2 * padding);

            // Initialize on first call
            if (!_initialized)
            {
                InitializeComponentData();
            }

            // Check if we need to recalculate the layout
            Vector2 currentRectSize = new Vector2(r.width, r.height);
            if (!_layoutCacheValid || _lastRectSize != currentRectSize)
            {
                CalculateLayout(maxWidth, lineHeight, buttonSpacing);
                _lastRectSize = currentRectSize;
            }

            // Calculate starting Y position to center vertically
            float startY = r.y + ((r.height - _cachedTotalHeight) / 2);
            float currentY = startY;

            // Draw buttons in centered wrapped layout
            foreach (List<(GUIContent name, Type type, bool isOn, float width)> line in _cachedLines)
            {
                // Calculate total width of this line
                float lineWidth = line.Sum(b => b.width) + (buttonSpacing * (line.Count - 1));
                float startX = r.x + ((r.width - lineWidth) / 2);
                float currentX = startX;

                foreach ((GUIContent name, Type type, bool isOn, float width) in line)
                {
                    Rect buttonRect = new Rect(currentX, currentY, width, lineHeight);
                    var newIsOn = GUI.Toggle(buttonRect, isOn, name, EditorStyles.toolbarButton);

                    if (newIsOn != isOn)
                    {
                        bool oldIgnore = _ignoreDebugModeChange;
                        _ignoreDebugModeChange = true;
                        DebugMode.SetIsOn(type, newIsOn);
                        _ignoreDebugModeChange = oldIgnore;
                        // Debug.Log($"{(newIsOn ? "Enabled" : "Disabled")} Debug Mode for {type.Name}");

                        // Update the cached state
                        for (int i = 0; i < _debuggableTypes.Length; i++)
                        {
                            if (_debuggableTypes[i].type == type)
                            {
                                _debuggableTypes[i].isOn = newIsOn;
                                break;
                            }
                        }
                    }

                    currentX += width + buttonSpacing;
                }

                currentY += lineHeight + buttonSpacing;
            }
        }
    }
}
