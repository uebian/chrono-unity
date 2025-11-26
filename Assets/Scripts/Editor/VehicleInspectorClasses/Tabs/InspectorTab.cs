// =============================================================================
// PROJECT CHRONO - http://projectchrono.org
//
// Copyright (c) 2024 projectchrono.org
// All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found
// in the LICENSE file at the top level of the distribution.
//
// =============================================================================
// Authors: Josh Diyn
// =============================================================================
// Base class for all vehicle inspector tabs
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace VehicleBuilder.Editor
{
    public abstract class InspectorTab
    {
        protected VehicleInspectorContext Context { get; private set; }
        
        public InspectorTab(VehicleInspectorContext context)
        {
            Context = context;
        }
        
        /// <summary>
        /// Override to perform tab-specific initialization or data loading
        /// </summary>
        public virtual void OnTabEnter()
        {
            // Default: do nothing
        }
        
        /// <summary>
        /// Called when leaving this tab for another
        /// </summary>
        public virtual void OnTabExit()
        {
            // Default: do nothing
        }
        
        /// <summary>
        /// Draw the tab's GUI content
        /// This is the main method each tab must implement
        /// </summary>
        public abstract void DrawTab();
        
        /// <summary>
        /// Helper to draw a consistent section header
        /// </summary>
        protected void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
        }
        
        /// <summary>
        /// Helper to draw a horizontal divider
        /// </summary>
        protected void DrawDivider()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(8);
        }
        
        /// <summary>
        /// Helper to get responsive inspector width
        /// </summary>
        protected float GetInspectorWidth()
        {
            return EditorGUIUtility.currentViewWidth - 32f;
        }
        
        /// <summary>
        /// Helper to draw button row
        /// </summary>
        protected void DrawButtonRow(params (string label, System.Action action)[] buttons)
        {
            float inspectorWidth = GetInspectorWidth();
            EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(inspectorWidth));
            GUILayout.FlexibleSpace();
            
            float buttonWidth = Mathf.Min(100f, (inspectorWidth - 20f * buttons.Length) / buttons.Length);
            
            foreach (var button in buttons)
            {
                if (GUILayout.Button(button.label, GUILayout.Width(buttonWidth), GUILayout.Height(30)))
                {
                    // Use delayCall to avoid GUI layout errors when dialogs are shown
                    var actionToInvoke = button.action;
                    EditorApplication.delayCall += () => actionToInvoke?.Invoke();
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }
}
