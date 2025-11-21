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
// Detailed editor for vehicle-specific JSON parameters
// =============================================================================

using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace VehicleBuilder.Editor
{

    public class VehicleJsonTab : InspectorTab
    {
        private bool showJsonEditor = false;
        
        public VehicleJsonTab(VehicleInspectorContext context) : base(context)
        {
        }
        
        public override void DrawTab()
        {
            DrawSectionHeader("Vehicle JSON Configuration");
            
            // File selector dropdown
            DrawQuickJsonSelector(
                "Vehicle JSON:",
                Context.Vehicle.topLevelVehicleJSON,
                Context.VehicleJsonFiles,
                (newPath) => {
                    Context.Vehicle.topLevelVehicleJSON = newPath;
                    Context.JsonState.SetVehiclePath(newPath);
                });
            
            EditorGUILayout.Space();
            
            // Load/Save buttons
            DrawButtonRow(
                ("Load JSON", LoadVehicleJson),
                ("Save JSON", SaveVehicleJson),
                ("Save As...", SaveVehicleJsonAs)
            );
            
            DrawDivider();
            
            // Syntax-highlighted JSON editor
            showJsonEditor = EditorGUILayout.Foldout(showJsonEditor, "Edit Vehicle JSON (Syntax Highlighted)", true);
            if (showJsonEditor && Context.JsonState.VehicleData != null)
            {
                // Initialise text editor if needed
                if (Context.TextEditor.GetJson() == null || 
                    Context.TextEditor.GetJson().ToString() != Context.JsonState.VehicleData.ToString())
                {
                    Context.TextEditor.SetJson(Context.JsonState.VehicleData);
                }
                
                // Draw syntax-highlighted text editor
                EditorGUILayout.BeginVertical("box");
                Context.TextEditor.Draw(400f);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space();
                
                // Update and format buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply Changes", GUILayout.Height(25)))
                {
                    EditorApplication.delayCall += () => {
                        JObject updated = Context.TextEditor.GetJson();
                        if (updated != null)
                        {
                            Context.JsonState.LoadVehicle(updated, Context.ParsedVehicleData);
                            EditorUtility.DisplayDialog("Changes Applied", "JSON has been updated. Click 'Save JSON' to persist to file.", "OK");
                        }
                    };
                }
                if (GUILayout.Button("Format JSON", GUILayout.Width(100), GUILayout.Height(25)))
                {
                    EditorApplication.delayCall += () => {
                        Context.TextEditor.SetJson(Context.JsonState.VehicleData); // Reformat
                    };
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private void LoadVehicleJson()
        {
            EditorApplication.delayCall += () => {
                if (Context.Vehicle != null)
                {
                    try
                    {
                        JObject data = Context.BuilderCore.LoadJson(Context.JsonState.VehiclePath);
                        Context.JsonState.LoadVehicle(data, null); // Tab doesn't need parsed data
                        Debug.Log($"Loaded vehicle JSON: {Context.JsonState.VehiclePath}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to load vehicle JSON: {ex.Message}");
                    }
                    finally
                    {
                        EditorUtility.SetDirty(Context.Vehicle);
                    }
                }
            };
        }
        
        private void SaveVehicleJson()
        {
            if (Context.JsonState.VehicleData != null && !string.IsNullOrEmpty(Context.JsonState.VehiclePath))
            {
                if (!JsonSaveUtility.ConfirmSave("vehicle JSON", Context.JsonState.VehiclePath))
                    return;

                Context.BuilderCore.SaveJson(Context.JsonState.VehicleData, Context.JsonState.VehiclePath);
                EditorUtility.DisplayDialog("Saved", $"Vehicle JSON saved to:\n{Context.JsonState.VehiclePath}", "OK");
            }
        }
        
        private void SaveVehicleJsonAs()
        {
            if (Context.JsonState.VehicleData == null)
            {
                EditorUtility.DisplayDialog("Error", "No JSON data to save.", "OK");
                return;
            }
            
            string path = EditorUtility.SaveFilePanel("Save Vehicle JSON", Context.BuilderCore.ChronoVehicleDataRoot, "NewVehicle.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = Context.BuilderCore.ToRelativePath(path);
                if (!string.IsNullOrEmpty(relativePath))
                {
                    if (!JsonSaveUtility.ConfirmSaveAs("vehicle JSON", relativePath))
                        return;

                    Context.BuilderCore.SaveJson(Context.JsonState.VehicleData, relativePath);
                    Context.Vehicle.topLevelVehicleJSON = relativePath;
                    Context.JsonState.SetVehiclePath(relativePath);
                    Context.BuilderCore.RebuildCache();
                    Context.RefreshFileLists();
                    EditorUtility.DisplayDialog("Saved", $"Vehicle JSON saved to:\n{relativePath}", "OK");
                }
            }
        }
        
        private void DrawQuickJsonSelector(string label, string currentPath, System.Collections.Generic.List<string> files, System.Action<string> onChanged)
        {
            if (files.Count == 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label, GUILayout.Width(120));
                EditorGUILayout.LabelField("[No files found]");
                EditorGUILayout.EndHorizontal();
                return;
            }
            
            Context.BuilderCore.BuildDisplayListFromFilenames(files, out var displayNames, out var actualPaths);
            
            int selectedIndex = actualPaths.IndexOf(currentPath);
            if (selectedIndex < 0 && actualPaths.Count > 0)
                selectedIndex = 0;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120));
            int newIndex = EditorGUILayout.Popup(selectedIndex, displayNames.ToArray());
            EditorGUILayout.EndHorizontal();
            
            if (newIndex >= 0 && newIndex < actualPaths.Count && newIndex != selectedIndex)
            {
                onChanged?.Invoke(actualPaths[newIndex]);
            }
        }
    }
}
