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
// A separate EditorWindow for advanced JSON editing where main window
// calls this window whenever the user clicks "Edit" on a detected json file
// This will also display graphs, and allow user to choose editing of the raw
// json text. Also provides a close-back stack that rolls upwards if multiple
// edits and thne child edits have gone on. i.e. closing will return to the
// previous edit window
// =============================================================================

using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;


public class UChEditJSONValues : EditorWindow
{
    // JSON currently loaded in memory
    private JObject m_loadedObject;
    // Indicates if this is a newly created file (so we don't overwrite an existing one by mistake)
    private bool m_isNewFile;
    // root folder for Chrono Vehicle data
    private string m_chronoVehicleDataRoot;
    // relative path of the JSON file within the chronoVehicleDataRoot <-- important to have this correctly set!
    private string m_currentRelativePath;

    // For drawing map data (graphs, etc.)
    private UChVehGenMapping mapDrawer;

    // Scroll position for the EditorWindow
    private Vector2 scrollPos;

    // Static method to open this window with a given file path + root
    // keep it to just one edit window only
    public static void OpenEditWindow(string fileSubPath, string chronoVehicleDataRoot)
    {
        // new edit window instance
        var newWindow = CreateInstance<UChEditJSONValues>();
        newWindow.titleContent = new GUIContent("JSON Editor"); // put chrono icon in the window?
        newWindow.InitWithExistingFile(fileSubPath, chronoVehicleDataRoot);

        // rebuild cache
        UChVehGenJSONUtils.BuildFileMetaCache(chronoVehicleDataRoot);

        // Show the window
        newWindow.Show();
    }

    // Called when creating a brand new subcomponent
    public static void OpenEditWindowForNewSubcomponent(
        string subsystemType,
        string templateName,
        string chronoVehicleDataRoot
    )
    {
        var window = GetWindow<UChEditJSONValues>("JSON Editor");
        JObject def = UChVehGenJSONUtils.GetDefaultSubcomponentJson(subsystemType, templateName);
        if (def == null)
        {
            // fallback barebones fields if no template is found
            def = new JObject
            {
                ["Type"] = subsystemType,
                ["Template"] = templateName,
                ["Name"] = $"New {subsystemType} {templateName}"
            };
        }

        // initialise in new file mode
        window.InitInMemoryNewFile(def, chronoVehicleDataRoot);
        // set up the subcomponent drop down checking
        UChVehGenJSONUtils.BuildFileMetaCache(chronoVehicleDataRoot);

        // show the window
        window.Show();
    }

    private void OnEnable()
    {
        // create a new map drawer instance (dicard any previously set/loaded), let garbage collector handle the cleanup
        mapDrawer = new UChVehGenMapping();
        mapDrawer.ClearAllMapStates();

        // minimum size of the window
        this.minSize = new Vector2(500, 700);
    }

    // Initialises the window to edit an existing file
    public void InitWithExistingFile(string relativePath, string chronoVehicleDataRoot)
    {
        m_chronoVehicleDataRoot = chronoVehicleDataRoot;
        m_isNewFile = false;
        m_currentRelativePath = relativePath;

        // Load the JSON from disk
        m_loadedObject = UChVehGenJSONUtils.LoadJson(chronoVehicleDataRoot, relativePath);
        if (m_loadedObject == null)
        {
            m_loadedObject = new JObject();  // fallback if load failed
        }
    }

    // Initialise with brand-new JSON
    public void InitInMemoryNewFile(JObject newObj, string chronoVehicleDataRoot)
    {
        m_loadedObject = newObj;
        m_isNewFile = true;
        m_chronoVehicleDataRoot = chronoVehicleDataRoot;
        m_currentRelativePath = "";
    }

    private void OnGUI()
    {
        // Ifnothing to display
        if (m_loadedObject == null)
        {
            EditorGUILayout.LabelField("No data loaded");
            return;
        }

        // Show a read-only label at the top
        if (string.IsNullOrEmpty(m_currentRelativePath))
        {
            EditorGUILayout.LabelField("Currently editing: [New JSON]", EditorStyles.boldLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Currently editing: " + m_currentRelativePath, EditorStyles.boldLabel);
        }

        if (GUILayout.Button("Edit Raw JSON", GUILayout.Width(120)))
        {
            // Convert m_loadedObject to a nicely formatted string
            string rawText = m_loadedObject.ToString(Newtonsoft.Json.Formatting.Indented);
            // Normalise line endings
            rawText = rawText.Replace("\r\n", "\n").Replace("\r", "");

            // Open the raw JSON window
            UChJSONRawWindow rawWin = GetWindow<UChJSONRawWindow>("JSON Raw Editor");
            // set up the text directly
            rawWin.InitializeWithDirectJson("InMemory", m_chronoVehicleDataRoot, rawText);

            // Safely close using a delayed call to avoid IMGUI issues
            EditorApplication.delayCall += () => {
                this.Close();
            };
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.Separator();

        // JSON object contents in a scrollable area
        EditorGUILayout.Space();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        {
            // UChVehGenParsing is responsible for drawing the JToken structure
            UChVehGenParsing.DrawFullJsonObject(m_loadedObject, m_chronoVehicleDataRoot, mapDrawer);
        }
        EditorGUILayout.EndScrollView();

        // save over / Save as buttons
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledGroupScope(m_isNewFile))
        {
            // only enabled if this is not a new file
            if (GUILayout.Button("Save Over", GUILayout.Width(100)))
            {
                if (!m_isNewFile)
                {
                    SaveOver();
                }
            }
        }
        if (GUILayout.Button("Save As", GUILayout.Width(100)))
        {
            SaveAs();
        }

        // **** Close Button ****
        if (GUILayout.Button("Close"))
        {
            // Safely close using a delayed call to avoid IMGUI issues
            EditorApplication.delayCall += () => {
                this.Close();
            };
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();
    }

    // Overwrite the current file with the edited JSON content
    private void SaveOver()
    {
        if (string.IsNullOrEmpty(m_currentRelativePath))
        {
            Debug.LogError("No current file path to overwrite.");
            return;
        }
        UChVehGenJSONUtils.SaveJson(m_loadedObject, m_chronoVehicleDataRoot, m_currentRelativePath);
        Debug.Log("File overwritten: " + m_currentRelativePath);
    }

    // prompt for a new filename and saves the JSON content
    private void SaveAs()
    {
        // guess a default name from the JSON's "Name" field
        string defaultName = (string)(m_loadedObject["Name"] ?? "NewComponent") + ".json";

        string fullPath = EditorUtility.SaveFilePanel(
            "Save JSON As...",
            m_chronoVehicleDataRoot,
            defaultName,
            "json"
        );
        if (string.IsNullOrEmpty(fullPath))
            return;

        // convert to relative path to keep it under root vehicle data folder (if thas what the user wantS)
        string relPath = UChVehGenJSONUtils.ToRelativePath(m_chronoVehicleDataRoot, fullPath);
        if (string.IsNullOrEmpty(relPath))
        {
            // fallback to just the filename
            relPath = Path.GetFileName(fullPath);
        }

        // write the file to disk
        UChVehGenJSONUtils.SaveJson(m_loadedObject, m_chronoVehicleDataRoot, relPath);
        Debug.Log("File saved: " + relPath);

        // Switch to existing file mode now that we have a path
        m_currentRelativePath = relPath;
        m_isNewFile = false;
    }
}
