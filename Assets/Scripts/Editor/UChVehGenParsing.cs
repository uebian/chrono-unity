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
// Static handling of drawing and parsing JSON objects in the EditorWindow
// =============================================================================

using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

public static class UChVehGenParsing
{

    // draw the entire JObject
    public static void DrawFullJsonObject(JObject root, string dataRootDir, UChVehGenMapping mapDrawer)
    {
        // If the root object is null, show a warning in the inspector UI
        if (root == null)
        {
            EditorGUILayout.HelpBox("No JSON root provided.", MessageType.Warning);
            return;
        }

        // Begin the recursion by calling DrawGenericJToken on the root JObject
        DrawGenericJToken(root, "", dataRootDir, propertyName: "", mapDrawer);
    }

    // universal switch for JObject / JArray / scalar
    private static void DrawGenericJToken(JToken token, string prefix, string dataRootDir, string propertyName, UChVehGenMapping mapDrawer)
    {
        // call DrawJObject
        if (token is JObject obj)
        {
            DrawJObject(obj, prefix, dataRootDir, parentKey: propertyName, mapDrawer);
        }
        // check for special cases (Map, Vector3, Orientation, etc.)
        else if (token is JArray arr)
        {
            // if propertyName suggests it's a "Map"
            if (!string.IsNullOrEmpty(propertyName)
                && (propertyName.Contains("Map", StringComparison.OrdinalIgnoreCase)
                    || propertyName.Contains("Curve Data", StringComparison.OrdinalIgnoreCase)))
            {
                // Treat it as a map-based data array and draw graph + pairs
                DrawMapArray(arr, prefix, propertyName, mapDrawer);
            }
            // if the array has exactly 3 elements, treat it as a 3D vector
            else if (IsLikelyVector3(arr))
            {
                DrawVector3Inline(arr, prefix, propertyName);
            }
            // if the array has exactly 4 elements and property name is "Orientation"
            else if (arr.Count == 4 && propertyName.Equals("Orientation", StringComparison.OrdinalIgnoreCase))
            {
                // treat as a quaternion
                DrawQuaternionArrayField(prefix + propertyName, arr);
            }
            // normal array fallback
            else
            {
                DrawJArray(arr, prefix, dataRootDir, parentKey: propertyName, mapDrawer);
            }
        }
        else
        {
            // scalar or string values display as aq label
            EditorGUILayout.LabelField($"{prefix}{token}");
        }
    }

    // draw a map array with pairs on left, graph on right
    private static void DrawMapArray(JArray mapArray, string prefix, string label, UChVehGenMapping mapDrawer)
    {
        // section box
        EditorGUILayout.BeginVertical("box");
        {
            EditorGUILayout.LabelField($"{prefix}{label} (Graph)", EditorStyles.boldLabel);

            // If no data in the array, show a help box
            if (mapArray == null || mapArray.Count == 0)
            {
                EditorGUILayout.HelpBox("No data in graph.", MessageType.Info);
            }
            else
            {
                // If data, draw it side by side: pairs on the left, graph on the right
                EditorGUILayout.BeginHorizontal("box", GUILayout.ExpandWidth(false));
                {
                    // Left vertical layout: draws all the numeric pairs
                    EditorGUILayout.BeginVertical(GUILayout.Width(300));
                    {
                        mapDrawer.DrawPairs(mapArray, label);
                    }
                    EditorGUILayout.EndVertical();

                    // spacing before the graph
                    EditorGUILayout.Space(5);

                    // Right vertical layout: draws the graph
                    EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    {
                        mapDrawer.DrawGraph(mapArray, label);
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndVertical();
    }

    // JObject pull data to draw depending on categorisation
    private static void DrawJObject(JObject obj, string prefix, string dataRootDir, string parentKey, UChVehGenMapping mapDrawer)
    {
        // standard widths
        float labelWidth = 150f;
        float fieldWidth = 200f;

        // extract all properties
        var props = new List<JProperty>(obj.Properties());
        // store any properties to remove if the user clicks to remove them
        List<string> propertiesToRemove = new List<string>();

        // iterate over each property
        foreach (var prop in props)
        {
            string key = prop.Name;
            var val = prop.Value;

            // If it's a subobject call the relevant function
            if (val is JObject subObj)
            {
                DrawSubObjectProperty(obj, prefix, dataRootDir, parentKey, key, subObj, propertiesToRemove, labelWidth, mapDrawer);
            }
            // for jArray, handle special cases
            else if (val is JArray subArr)
            {
                DrawArrayProperty(obj, prefix, dataRootDir, parentKey, key, subArr, propertiesToRemove, labelWidth, mapDrawer);
            }
            else
            {
                // detect if it's a "forced subcomponent reference" (like an engine or something)
                string forcedType = UChVehGenJSONUtils.GetForcedType(parentKey, key);
                if (!string.IsNullOrEmpty(forcedType))
                {
                    DrawForcedSubcomponentProperty(obj, prefix, dataRootDir, parentKey, key, val, forcedType, propertiesToRemove, labelWidth, fieldWidth);
                }
                else
                {
                    // Otherwise, treat as normal scalar
                    DrawNormalScalarProperty(obj, prefix, key, val, propertiesToRemove, labelWidth, fieldWidth);
                }
            }
        }

        // after iterating remove any flagged properties
        RemoveProperties(obj, propertiesToRemove);
        GUILayout.Space(10);
    }

    // ------------------------------------------------------------------------
    // Functions for Different Property Types
    // ------------------------------------------------------------------------

    // handles drawing a sub-object (JObject) property
    private static void DrawSubObjectProperty(
        JObject parentObj,
        string prefix,
        string dataRootDir,
        string parentKey,
        string key,
        JObject subObj,
        List<string> propertiesToRemove,
        float labelWidth,
        UChVehGenMapping mapDrawer
    )
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(prefix + key + ":", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
        EditorGUILayout.EndHorizontal();

        // recursively draw this sub-object with an increased prefix
        DrawJObject(subObj, prefix + "  ", dataRootDir, key, mapDrawer);
    }

    // Handles drawing a JArray
    private static void DrawArrayProperty(
        JObject parentObj,
        string prefix,
        string dataRootDir,
        string parentKey,
        string key,
        JArray subArr,
        List<string> propertiesToRemove,
        float labelWidth,
        UChVehGenMapping mapDrawer
    )
    {
        EditorGUILayout.BeginHorizontal();
        // property name in bold
        EditorGUILayout.LabelField(prefix + key + ":", EditorStyles.boldLabel, GUILayout.Width(labelWidth));

        EditorGUILayout.EndHorizontal();

        // delegate to DrawGenericJToken so it can handle the array (possibly a map, vector, etc.)
        DrawGenericJToken(subArr, prefix + "  ", dataRootDir, propertyName: key, mapDrawer);
    }

    // Handles a "forced subcomponent reference" property (e.g., for engine/transmission references)
    private static void DrawForcedSubcomponentProperty(
        JObject parentObj,
        string prefix,
        string dataRootDir,
        string parentKey,
        string key,
        JToken val,
        string forcedType,
        List<string> propertiesToRemove,
        float labelWidth,
        float fieldWidth
    )
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(prefix + key + ":", GUILayout.Width(labelWidth));

        // convert to a string
        string currentPath = val.ToString();
        // custom reference dropdown to change the path
        string newPath = DrawJsonReferenceDropdown(
            label: "",
            currentPath: currentPath,
            labelWidth: 0f,
            fieldWidth: fieldWidth,
            dataRootDir: dataRootDir,
            forcedType: forcedType,
            forcedSubfolder: ""
        );

        // if user changed the reference, update the JObject
        if (newPath != currentPath)
        {
            parentObj[key] = newPath;
        }
        EditorGUILayout.EndHorizontal();
    }

    // Handle normal scalar property
    private static void DrawNormalScalarProperty(
        JObject parentObj,
        string prefix,
        string key,
        JToken val,
        List<string> propertiesToRemove,
        float labelWidth,
        float fieldWidth
    )
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(prefix + key + ":", GUILayout.Width(labelWidth));

        // existing value as text
        string oldVal = val.ToString();
        // Let the user edit the value with a text input
        string newVal = EditorGUILayout.TextField(oldVal, GUILayout.Width(fieldWidth));

        // If the user changed the value, try to parse it
        if (newVal != oldVal)
        {
            if (float.TryParse(newVal, out float f))
                parentObj[key] = f;
            else if (int.TryParse(newVal, out int iv))
                parentObj[key] = iv;
            else if (bool.TryParse(newVal, out bool bv))
                parentObj[key] = bv;
            else
                parentObj[key] = newVal;
        }
        EditorGUILayout.EndHorizontal();
    }

    // removes any properties from the JObject that the user has marked for removal
    // TODO: still used for graph dot points?
    private static void RemoveProperties(JObject obj, List<string> propertiesToRemove)
    {
        if (propertiesToRemove.Count == 0) return;
        foreach (string propName in propertiesToRemove)
        {
            obj.Remove(propName);
        }
    }

    // fallback for arrays not recognised
    private static void DrawJArray(JArray arr, string prefix, string dataRootDir, string parentKey, UChVehGenMapping mapDrawer)
    {
        // keep track of any indices that need to be removed
        List<int> indicesToRemove = new List<int>();

        // Iterate over each element in the array
        for (int i = 0; i < arr.Count; i++)
        {
            var el = arr[i];

            // If the element is a sub-object (JObject), we draw it recursively
            if (el is JObject subObj)
            {
                EditorGUILayout.LabelField($"{prefix}[{i}]:", EditorStyles.boldLabel, GUILayout.Width(160));
                DrawJObject(subObj, prefix + "  ", dataRootDir, parentKey, mapDrawer);
            }
            // If the element is another array let DrawGenericJToken handle it
            else if (el is JArray subArr)
            {
                EditorGUILayout.LabelField($"{prefix}[{i}]:", EditorStyles.boldLabel, GUILayout.Width(160));
                DrawGenericJToken(subArr, prefix + "  ", dataRootDir, propertyName: parentKey, mapDrawer);
            }
            else
            {
                // otherwise a scalar 
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"{prefix}[{i}]:", GUILayout.Width(160));

                // edit the value
                string oldVal = el.ToString();
                string newVal = EditorGUILayout.TextField(oldVal, GUILayout.Width(300));
                if (newVal != oldVal)
                {
                    if (float.TryParse(newVal, out float f))
                        arr[i] = f;
                    else if (int.TryParse(newVal, out int iv))
                        arr[i] = iv;
                    else if (bool.TryParse(newVal, out bool bv))
                        arr[i] = bv;
                    else
                        arr[i] = newVal;
                }

                // Button to remove this element from the array
                if (GUILayout.Button("-", GUILayout.Width(30)))
                {
                    indicesToRemove.Add(i);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // Remove any elements that were flagged for removal
        if (indicesToRemove.Count > 0)
        {
            // Sort descending to remove safely
            indicesToRemove.Sort((a, b) => b.CompareTo(a));
            foreach (int index in indicesToRemove)
            {
                if (index >= 0 && index < arr.Count)
                    arr.RemoveAt(index);
            }
        }
    }

    // Recognising and drawing 3-value arrays as Vector3 so they don't make an extremely long text display
    private static bool IsLikelyVector3(JArray arr)
    {
        // Check if exactly 3 elements
        if (arr.Count != 3) return false;

        // aklso check each element to see if it's numeric
        foreach (var el in arr)
        {
            if (!(el.Type == JTokenType.Integer || el.Type == JTokenType.Float))
                return false;
        }
        return true;
    }

    private static void DrawVector3Inline(JArray arr, string prefix, string propertyName)
    {
        // Parse the three elements into x, y, z
        float x = arr[0].ToObject<float>();
        float y = arr[1].ToObject<float>();
        float z = arr[2].ToObject<float>();

        // Display side by side
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"{prefix}{propertyName}:  X", GUILayout.Width(100));
        float newX = EditorGUILayout.FloatField(x, GUILayout.Width(60));
        GUILayout.Label("Y", GUILayout.Width(15));
        float newY = EditorGUILayout.FloatField(y, GUILayout.Width(60));
        GUILayout.Label("Z", GUILayout.Width(15));
        float newZ = EditorGUILayout.FloatField(z, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        // If user changed any value, update the JArray
        if (!Mathf.Approximately(newX, x)
            || !Mathf.Approximately(newY, y)
            || !Mathf.Approximately(newZ, z))
        {
            arr[0] = newX;
            arr[1] = newY;
            arr[2] = newZ;
        }
    }

    // Drawing 4-value array as quat
    private static void DrawQuaternionArrayField(string label, JArray arr)
    {
        // check at least 4 elements
        while (arr.Count < 4)
            arr.Add(0f);

        float x = arr[0].ToObject<float>();
        float y = arr[1].ToObject<float>();
        float z = arr[2].ToObject<float>();
        float w = arr[3].ToObject<float>();

        Vector4 oldV4 = new Vector4(x, y, z, w);
        Vector4 newV4 = EditorGUILayout.Vector4Field("", oldV4, GUILayout.Width(400)); // no label needed

        // If the user changed it, update the array
        if (newV4 != oldV4)
        {
            arr[0] = newV4.x;
            arr[1] = newV4.y;
            arr[2] = newV4.z;
            arr[3] = newV4.w;
        }
    }

    // Subcomponent reference dropdown - static. TODO: perhaps move to the uchvehiclegenerator
    // Draws a popup listing all JSONs of a given subcomponent type, constrained
    // to a specific subfolder if set, otherwise defaults to the root data dir
    // returns the newly selected path
    public static string DrawJsonReferenceDropdown(
        string label,
        string currentPath,
        float labelWidth,
        float fieldWidth,
        string dataRootDir,
        string forcedType,
        string forcedSubfolder
    )
    {
        EditorGUILayout.BeginHorizontal();
        try
        {
            if (!string.IsNullOrEmpty(label))
            {
                GUILayout.Label(label, GUILayout.Width(labelWidth));
            }

            // build two lists: one for display names, one for actual paths
            List<string> displayNames = new List<string>();
            List<string> actualPaths = new List<string>();

            // Decide which subfolder to look in
            string subFolder = !string.IsNullOrEmpty(forcedSubfolder)
                               ? forcedSubfolder
                               : dataRootDir; // fallback to the data root

            // If forcedSubfolder wasn't specified, try to guess from currentPath
            if (string.IsNullOrEmpty(forcedSubfolder) && !string.IsNullOrEmpty(currentPath))
            {
                string[] parts = currentPath.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                    subFolder = parts[0];
            }

            // fetch the subcomponent cache
            var fullMap = UChVehGenJSONUtils.GetCacheBySubfolderAndType();
            // try to find fullMap[subFolder][forcedType]
            if (fullMap.TryGetValue(subFolder, out var byType))
            {
                if (byType.TryGetValue(forcedType, out var fileList))
                {
                    // For each file path in that list, build a display name
                    foreach (var sp in fileList)
                    {
                        string disp = Path.GetFileName(sp);
                        displayNames.Add(disp);
                        actualPaths.Add(sp);
                    }
                }
            }

            // If no items, show an empty popup
            if (displayNames.Count == 0)
            {
                EditorGUILayout.Popup(0, new string[0], GUILayout.Width(fieldWidth));
                return currentPath;
            }

            // Find the currently selected index based on currentPath
            int selectedIndex = 0;
            for (int i = 0; i < actualPaths.Count; i++)
            {
                if (actualPaths[i] == currentPath)
                {
                    selectedIndex = i;
                    break;
                }
            }

            // Draw the popup
            int newIndex = EditorGUILayout.Popup(selectedIndex, displayNames.ToArray(), GUILayout.Width(fieldWidth));

            // edit button to open the file in the editor window
            if (GUILayout.Button("Edit", GUILayout.Width(50)))
            {
                if (newIndex >= 0 && newIndex < actualPaths.Count)
                {
                    string chosenFile = actualPaths[newIndex];
                    UChEditJSONValues.OpenEditWindow(chosenFile, dataRootDir);
                }
            }

            // return newly chosen path
            return actualPaths[newIndex];
        }
        finally
        {
            EditorGUILayout.EndHorizontal();
        }
    }
}
