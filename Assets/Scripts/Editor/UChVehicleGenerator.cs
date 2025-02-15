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
// This is the main window for generating JSON vehicles from Chrono-based json
// files and attaching unity gameobjects to them, speccing the vehicle and
// generating it in the scene
// =============================================================================

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System;
using Newtonsoft.Json.Linq;
using System.Linq;


// vehicle types - this will determine the subfolders to be searched (by matched names)
public enum ChronoVehicleType
{
    HMMWV,
    GATOR,
    UAZ,
    MAN_KAT1,
    CustomGeneric // haven't got any jsons in that folder yet...
}

/// TODO:
/// - expand to handle creation of dual tyred arrangments.. <summary>
/// - add more json folders
public class UChVehicleGenerator : EditorWindow
{
    /// Absolute or project-relative path to the ChronoData/vehicle root folder.
    private string chronoVehicleDataRoot = "Assets/StreamingAssets/ChronoData/vehicle";
    private ChronoVehicleType vehicleType = ChronoVehicleType.HMMWV;
    private Dictionary<string, List<string>> subcomponentFilesByType = new Dictionary<string, List<string>>();

    // Indices for creating new subcomponents
    private int newSubTypeIndex = 0;
    private int newTemplateIndex = 0;

    // List of known subcomponent types
    private List<string> knownTypes = new List<string>
    {
        "Engine", "Transmission", "Brake", "Driveline",
        "Steering", "Suspension", "Wheel", "Tire", "Vehicle"
    };

    // Default step size for tires
    private float tireStepSize = 0.001f;

    // List of vehicle JSON files
    private List<string> vehicleJsonFiles = new List<string>();

    // Selected jsons to get passed to the created vehicle
    private string chosenVehicleFile = "";
    private string selectedEngineFile = "";
    private string selectedTransmissionFile = "";

    // Whether to use a single tire file for all wheels
    private bool useSingleTireFile = true;
    private string selectedSingleTireFile = "";
    // For multiple tire files, store one per axle
    private List<string> multiTireFiles = new List<string>();

    // Other basic specs
    private bool chassisFixed = false;
    private bool brakeLocking = false;
    private float initForwardVel = 0f;
    private float initWheelAngVel = 0f;
    private ChTire.CollisionType tireCollisionType = ChTire.CollisionType.SINGLE_POINT;

    // Prefab references for building the visulisatio
    private GameObject chassisMesh;

    // single left/right for the entire vehicle, or separate left/right prefabs per axle
    private bool individualWheelsPerAxle = false;
    // Single left/right wheel prefabs if uniformLRWheels == false
    private GameObject wheelMeshLeft;
    private GameObject wheelMeshRight;

    // 2D list of prefabs: 
    private List<List<GameObject>> multiWheelPrefabs = new List<List<GameObject>>();

    // Foldout toggle for creating a new JSON subcomponent
    private bool showCreateJsonFoldout = false;
    // tracking the scroll position if the window is too small to fit everything
    private Vector2 scrollPosition = Vector2.zero;


    // Unity menu item to open this window
    [MenuItem("Tools/Chrono/Vehicle Builder")]
    public static void ShowWindow()
    {
        var window = GetWindow<UChVehicleGenerator>("Chrono Vehicle Builder");
        window.Show();
    }

    void OnEnable()
    {
        // Refresh the internal file lists
        ReloadFileLists();
        ClearLoadedData();
        // build or refresh the file list cache from the Chrono vehicle data root
        UChVehGenJSONUtils.BuildFileMetaCache(chronoVehicleDataRoot);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Chrono Vehicle Builder", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        // Vehicle selection
        DrawVehicleTypeSelection();

        // Toplevel main vehicle JSON selection
        DrawTopLevelVehicleSelection();

        // Basic toggles/fields
        DrawBasicParameters();

        // Subcomponent references
        DrawExternalVehicleRef();

        // If multiple tires are used, present the tire json section, but
        // only if there's a top-level vehicle chosen for us to parse the wheel count
        if (!string.IsNullOrEmpty(chosenVehicleFile))
        {
            DrawTireFileSelection();
        }

        // prefab references and "Generate Vehicle" button
        DrawPrefabAndGenerationSection();

        // draw a foldout for "Create New Subcomponent JSON"
        showCreateJsonFoldout = EditorGUILayout.Foldout(showCreateJsonFoldout, "Create New Subcomponent JSON");
        if (showCreateJsonFoldout)
        {
            DrawNewSubcomponentCreationBox();
        }
        EditorGUILayout.EndScrollView();
    }

    // Vehicle type selection
    private void DrawVehicleTypeSelection()
    {
        // Store old value to detect changes
        ChronoVehicleType oldType = vehicleType;
        vehicleType = (ChronoVehicleType)EditorGUILayout.EnumPopup("Vehicle Type", vehicleType);
        // If the type changed, reload relevant data
        if (vehicleType != oldType)
        {
            ReloadFileLists();
            ClearLoadedData();
        }
    }

    // Top-Level Vehicle JSON selection
    private void DrawTopLevelVehicleSelection()
    {
        EditorGUILayout.LabelField("Vehicle JSON:", EditorStyles.boldLabel);
        // When there's no files found for the chosen vehicle type
        if (vehicleJsonFiles.Count == 0)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("[No vehicle files found for this vehicle type in its subfolder]", GUILayout.Width(380));

            // removed this in-place edit/creation because we have creation of vehicle jsons in the foldout at the bottom
            //// A button to create a new top-level JSON if none exist
            //if (GUILayout.Button("Create New Vehicle", GUILayout.Width(150)))
            //{
            //    // basic data to create a new WheeledVehicle JSON
            //    UChEditJSONValues.OpenEditWindowForNewSubcomponent(
            //        "Vehicle",      // Type
            //        "WheeledVehicle",
            //        chronoVehicleDataRoot
            //    );
            //}

            EditorGUILayout.EndHorizontal();
        }
        else
        {
            // Build a user-friendly display list from the raw file paths
            UChVehGenJSONUtils.BuildDisplayListFromFilenames(
                vehicleJsonFiles,
                out List<string> displayNames,
                out List<string> actualPaths
            );

            // Find the current index of the chosen file among the actual paths
            int selectedIndex = actualPaths.IndexOf(chosenVehicleFile);
            if (selectedIndex < 0)
            {
                // If not found, default to the first file in the list
                selectedIndex = 0;
                chosenVehicleFile = actualPaths[0];
                // Parse number of wheels from the top-level JSON
                ParseNumWheelsFromTopLevel(chosenVehicleFile);
            }

            EditorGUILayout.BeginHorizontal();
            // Let the user pick from the list of vehicle JSONs
            int newIndex = EditorGUILayout.Popup(selectedIndex, displayNames.ToArray(), GUILayout.Width(420));
            if (newIndex != selectedIndex)
            {
                // If a new item is chosen, parse its wheels
                selectedIndex = newIndex;
                chosenVehicleFile = actualPaths[selectedIndex];
                ParseNumWheelsFromTopLevel(chosenVehicleFile);
            }

            if (!string.IsNullOrEmpty(chosenVehicleFile))
            {
                if (GUILayout.Button("Edit", GUILayout.Width(100)))
                {
                    // Open the chosen file in the editing window
                    UChEditJSONValues.OpenEditWindow(chosenVehicleFile, chronoVehicleDataRoot);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space();
    }

    // Helper to parse top-level JSON to see how many wheels we have so we can create the correct number of entries for multiTireFiles
    // and multiWheelPrefabs. Could possibly shift this to the parsing script..
    private void ParseNumWheelsFromTopLevel(string vehicleFile)
    {
        // start cleanly
        multiTireFiles.Clear();
        multiWheelPrefabs.Clear();

        if (string.IsNullOrEmpty(vehicleFile))
            return;

        // load
        var data = UChVehGenJSONUtils.LoadVehicleData(chronoVehicleDataRoot, vehicleFile);
        if (data == null || data.Axles == null)
            return;

        int numAxles = data.Axles.Count;

        // setup sublists for each axle so each axle has a list for left/right
        for (int i = 0; i < numAxles; i++)
        {
            multiWheelPrefabs.Add(new List<GameObject>());
        }

        // Also create matching empty slots for multiTireFiles 
        // so the user can assign them in the UI - empty here just to ensure they're displayed for editing
        for (int i = 0; i < numAxles; i++)
        {
            multiTireFiles.Add("");
        }
    }

    // basic params
    private void DrawBasicParameters()
    {
        chassisFixed = EditorGUILayout.Toggle("Chassis Fixed", chassisFixed);
        brakeLocking = EditorGUILayout.Toggle("Brake Locking", brakeLocking);
        initForwardVel = EditorGUILayout.FloatField("Init Forward Vel", initForwardVel);
        initWheelAngVel = EditorGUILayout.FloatField("Init Wheel Ang Speed", initWheelAngVel);
        tireCollisionType = (ChTire.CollisionType)EditorGUILayout.EnumPopup("Tire Collision Type", tireCollisionType);
        tireStepSize = EditorGUILayout.FloatField("Tire Step Size", tireStepSize);

        EditorGUILayout.Space();
    }

    // Subcomponent ref
    private void DrawExternalVehicleRef()
    {
        EditorGUILayout.LabelField("Powertrain Config", EditorStyles.boldLabel);

        // -----Engine
        EditorGUILayout.LabelField("Engine Reference", EditorStyles.boldLabel);
        // draw a dropdown that lets the user pick an Engine json file from those detected for this vehicle type
        selectedEngineFile = UChVehGenParsing.DrawJsonReferenceDropdown(
            label: "Engine Name:",
            currentPath: selectedEngineFile,
            labelWidth: 120f,
            fieldWidth: 300f,
            dataRootDir: chronoVehicleDataRoot,
            forcedType: "Engine",
            forcedSubfolder: vehicleType.ToString().ToLower()
        );
        EditorGUILayout.Space();

        // -----Transmission
        EditorGUILayout.LabelField("Transmission Reference", EditorStyles.boldLabel);
        // same as above but for transmission json files
        selectedTransmissionFile = UChVehGenParsing.DrawJsonReferenceDropdown(
            label: "Transmission Name:",
            currentPath: selectedTransmissionFile,
            labelWidth: 120f,
            fieldWidth: 300f,
            dataRootDir: chronoVehicleDataRoot,
            forcedType: "Transmission",
            forcedSubfolder: vehicleType.ToString().ToLower()
        );
        EditorGUILayout.Space();
    }

    // Single tire file or multiple
    private void DrawTireFileSelection()
    {
        EditorGUILayout.LabelField("Tire Selection (Note: FEA tires not supported", EditorStyles.boldLabel);

        // Toggle for single vs. multiple tire file usage
        useSingleTireFile = EditorGUILayout.Toggle("Use Single Tire File?", useSingleTireFile);

        if (useSingleTireFile)
        {
            // Single tire file => single dropdown
            selectedSingleTireFile = UChVehGenParsing.DrawJsonReferenceDropdown(
                label: "Tire Name:",
                currentPath: selectedSingleTireFile,
                labelWidth: 120f,
                fieldWidth: 300f,
                dataRootDir: chronoVehicleDataRoot,
                forcedType: "Tire",
                forcedSubfolder: vehicleType.ToString().ToLower()
            );
        }
        else
        {
            // For multiple tire files do one per axle (I assume the same tire type is fitted on the one axle. would be unlikely to have unmatched tire characteristics on left/right)
            int numAxles = multiTireFiles.Count;
            if (numAxles == 0)
            {
                EditorGUILayout.HelpBox("No axles found in the selected vehicle JSON. Cannot assign multiple tire files yet.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Assign a tire JSON to be used for each wheel on the {numAxles} axles:");
            EditorGUI.indentLevel++;
            for (int i = 0; i < numAxles; i++)
            {
                string currPath = multiTireFiles[i];
                string newPath = UChVehGenParsing.DrawJsonReferenceDropdown(
                    label: $"Axle {i}:",
                    currentPath: currPath,
                    labelWidth: 80f,
                    fieldWidth: 280f,
                    dataRootDir: chronoVehicleDataRoot,
                    forcedType: "Tire",
                    forcedSubfolder: vehicleType.ToString().ToLower()
                );
                multiTireFiles[i] = newPath;
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
    }

    // Prefab references and the main generation button drawing
    private void DrawPrefabAndGenerationSection()
    {
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.Width(250));
        GUILayout.Label("Unity Visualisation Gameobjects:", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        // pick a chassis mesh
        chassisMesh = (GameObject)EditorGUILayout.ObjectField(
            "Chassis Mesh:",
            chassisMesh,
            typeof(GameObject),
            false
        );

        // Toggle for per-axle or single left/right
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Individual wheel gameobjects?", GUILayout.Width(210));
        individualWheelsPerAxle = EditorGUILayout.Toggle(individualWheelsPerAxle);
        EditorGUILayout.EndHorizontal();


        // If false set one universal left + right prefab
        if (!individualWheelsPerAxle)
        {
            EditorGUI.indentLevel++;
            // right wheel
            wheelMeshLeft = (GameObject)EditorGUILayout.ObjectField("Left Wheel:",
                wheelMeshLeft,
                typeof(GameObject),
                false
            );
            // left wheel
            wheelMeshRight = (GameObject)EditorGUILayout.ObjectField("Right Wheel:",
                wheelMeshRight,
                typeof(GameObject),
                false
            );
            EditorGUI.indentLevel--;

        }
        else
        {
            // per axle approach where each axle has a sub-list [0 => left, 1 => right]
            if (multiWheelPrefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("No axles discovered. Parse a top-level JSON first.", MessageType.Info);
            }
            else
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < multiWheelPrefabs.Count; i++)
                {
                    // make sure the list has 2 elements
                    while (multiWheelPrefabs[i].Count < 2)
                    {
                        multiWheelPrefabs[i].Add(null);
                    }

                    multiWheelPrefabs[i][0] = (GameObject)EditorGUILayout.ObjectField(
                        $"Axle {i} Left Wheel:",
                        multiWheelPrefabs[i][0],
                        typeof(GameObject),
                        false
                    );
                    multiWheelPrefabs[i][1] = (GameObject)EditorGUILayout.ObjectField(
                        $"Axle {i} Right Wheel:",
                        multiWheelPrefabs[i][1],
                        typeof(GameObject),
                        false
                    );
                    EditorGUILayout.Space();
                }
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.Space();

        // trigger vehicle generation
        if (GUILayout.Button("Generate Vehicle In Scene", GUILayout.Height(30)))
        {
            GenerateVehicle();
        }
    }

    // foldout for json creation
    private void DrawNewSubcomponentCreationBox()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Create New Subcomponent JSON", EditorStyles.boldLabel);

        // Dropdown for the type
        newSubTypeIndex = EditorGUILayout.Popup("Type", newSubTypeIndex, knownTypes.ToArray());
        string selectedType = knownTypes[newSubTypeIndex];

        // Build a template list, default if none exist
        List<string> templateList = UChVehGenJSONUtils.GetTemplateListForType(selectedType);
        if (templateList == null) templateList = new List<string>();
        if (templateList.Count == 0)
        {
            templateList.Add("UnknownTemplate");
        }

        if (newTemplateIndex >= templateList.Count) newTemplateIndex = 0;
        newTemplateIndex = EditorGUILayout.Popup("Template", newTemplateIndex, templateList.ToArray());
        string chosenTemplate = templateList[newTemplateIndex];

        // open the raw JSON editor for this with the template type prefilled from the templates
        if (GUILayout.Button("Create & Edit"))
        {
            var window = CreateInstance<UChJSONRawWindow>();
            window.titleContent = new GUIContent("Raw JSON Editor");
            window.InitializeForNewSubcomponent(selectedType, chronoVehicleDataRoot, chosenTemplate);
            window.Show();
        }

        EditorGUILayout.EndVertical();
    }

    // Clean sweep of the file cache and lists
    private void ReloadFileLists()
    {
        // Rebuild the file meta cache
        UChVehGenJSONUtils.BuildFileMetaCache(chronoVehicleDataRoot);

        // get subcomponent file info from the cache
        Dictionary<string, List<string>> typedFiles = UChVehGenJSONUtils.GetSubcomponentFilesFromCache();
        subcomponentFilesByType = typedFiles;

        // Clear out the existing list of vehicles
        vehicleJsonFiles.Clear();

        // If the cache contains any "Vehicle" type files
        if (subcomponentFilesByType.ContainsKey("Vehicle"))
        {
            // Filter out only those relevant to this vehicle type, e.g. "hmmwv" for HMMWV
            string folderPrefix = vehicleType.ToString().ToLower();
            List<string> allVehicles = subcomponentFilesByType["Vehicle"];
            foreach (var fileSubPath in allVehicles)
            {
                if (fileSubPath.ToLower().Contains(folderPrefix))
                {
                    vehicleJsonFiles.Add(fileSubPath);
                }
            }
        }
    }

    // Clears data like the chosen vehicle file and multi tire references
    private void ClearLoadedData()
    {
        chosenVehicleFile = "";
        multiTireFiles.Clear();
        selectedSingleTireFile = "";
    }

    // The main function to take what's inputted in this window and generate a chrono vehicle in the unity scene!
    private void GenerateVehicle()
    {
        // Must have a chosen vehicle
        if (string.IsNullOrEmpty(chosenVehicleFile))
        {
            Debug.LogError("No vehicle JSON selected. Unable to generate vehicle.");
            return;
        }

        // Load the vehicle data
        var data = UChVehGenJSONUtils.LoadVehicleData(chronoVehicleDataRoot, chosenVehicleFile);
        if (data == null)
        {
            Debug.LogError("Failed to load vehicle data from " + chosenVehicleFile + ". Cannot generate.");
            return;
        }

        // Create a unique name for the new root GameObject
        int suffix = 1;
        string baseName = vehicleType.ToString() + "_Vehicle";
        string uniqueName = baseName;
        while (GameObject.Find(uniqueName) != null)
        {
            uniqueName = baseName + " (" + suffix + ")";
            suffix++;
        }

        // Create the root vehicle GameObject
        GameObject rootGO = new GameObject(uniqueName);
        rootGO.transform.position = new Vector3(0, 0.5f, 0);

        // Create or instantiate the chassis
        GameObject chassisGO = null;
        Vector3 relativeOffset = Vector3.zero;
        if (chassisMesh != null)
        {
            chassisGO = (GameObject)PrefabUtility.InstantiatePrefab(chassisMesh);
            if (!chassisGO)
                chassisGO = Instantiate(chassisMesh);
            chassisGO.name = "Chassis";
        }
        else
        {
            chassisGO = new GameObject("Chassis");
        }
        chassisGO.transform.position = relativeOffset;
        chassisGO.transform.SetParent(rootGO.transform, false);

        // Add a UWheeledVehicle component to the root
        UWheeledVehicle wheeledVehicle = rootGO.AddComponent<UWheeledVehicle>();
        wheeledVehicle.topLevelVehicleJSON = chosenVehicleFile;
        wheeledVehicle.engineJSON = selectedEngineFile;
        wheeledVehicle.transJSON = selectedTransmissionFile;

        // Single or multiple tire JSON usage
        wheeledVehicle.useSingleTireFile = useSingleTireFile;
        if (useSingleTireFile)
        {
            wheeledVehicle.tireJSON = selectedSingleTireFile;
        }
        else
        {
            // If multiple, store per-axle references
            wheeledVehicle.perAxleTireSpec = new List<string>(multiTireFiles);
        }

        // Set basic toggles
        wheeledVehicle.chassisFixed = chassisFixed;
        wheeledVehicle.brakeLocking = brakeLocking;
        wheeledVehicle.initForwardVel = initForwardVel;
        wheeledVehicle.initWheelAngVel = initWheelAngVel;
        wheeledVehicle.tireCollisionType = tireCollisionType;
        wheeledVehicle.tireStepSize = tireStepSize;


        // Directly assign the result of CreateAndAssignWheels, converting each sub-list
        //into a WheelGameobjects entry. This avoids storing a local 2D list variable which
        // Unity is not a big fan of displaying easily in the inspector...
        wheeledVehicle.axleData = CreateAndAssignWheels(
            parentVehicle: rootGO,
            data: data,
            useSingleTireFile: useSingleTireFile,
            axleTireFiles: multiTireFiles,
            selectedSingleTireFile: selectedSingleTireFile,
            uniformLRWheels: individualWheelsPerAxle,
            multiWheelPrefabs: multiWheelPrefabs,
            wheelMeshLeft: wheelMeshLeft,
            wheelMeshRight: wheelMeshRight
        ).Select(axleWheelList => new WheelGameobjects{ visualGameobjects = axleWheelList }).ToList(); // this flattens the 2d list (IMPORTANT)

        // Add a the driver script
        var driverScript = rootGO.AddComponent<Driver>();
        driverScript.vehicle = wheeledVehicle;

        Debug.Log($"Generated vehicle \"{rootGO.name}\" from vehicle JSON: {chosenVehicleFile}");
    }

    
    // Creates axles and wheels (GameObjects) in a 2D structure: [axleIndex][wheelIndex].
    // The 2d list is purely for convensience; and will be converted to a custmo class when generating the vehicle.
    //   - Single vs. multiple tire files (useSingleTireFile, axleTireFiles)
    //   - Single left/right wheel mesh or per-axle left/right meshes (uniformLRWheels, multiWheelPrefabs).
    private List<List<GameObject>> CreateAndAssignWheels(
        GameObject parentVehicle,
        UChVehGenJSONUtils.VehicleData data,
        bool useSingleTireFile,
        List<string> axleTireFiles,      // if !single, one file per axle
        string selectedSingleTireFile,   // if useSingleTireFile == true
        bool uniformLRWheels,
        List<List<GameObject>> multiWheelPrefabs, // 2D list: multiWheelPrefabs[axle][0 => left, 1 => right]
        GameObject wheelMeshLeft,
        GameObject wheelMeshRight
    )
    {
        // store the final results for returning at the end
        List<List<GameObject>> axleWheelLists = new List<List<GameObject>>();

        if (data.Axles == null || data.Axles.Count == 0)
        {
            Debug.LogWarning("No axles found in VehicleData!");
            return axleWheelLists;
        }

        // Make sub-lists
        for (int i = 0; i < data.Axles.Count; i++)
        {
            axleWheelLists.Add(new List<GameObject>());
        }

        for (int axleIndex = 0; axleIndex < data.Axles.Count; axleIndex++)
        {
            UChVehGenJSONUtils.AxleEntry axle = data.Axles[axleIndex];

            // 1 Create an Axle GameObject
            GameObject axleGO = new GameObject($"Axle_{axleIndex}");
            axleGO.transform.SetParent(parentVehicle.transform, false);

            // 2 Position it at the suspension location
            Vector3 suspLoc = new Vector3(
                axle.SuspensionLocation.x,
                axle.SuspensionLocation.z,
                axle.SuspensionLocation.y
            );
            axleGO.transform.localPosition = suspLoc;

            // set the name for the suspension
            string suspName = Path.GetFileNameWithoutExtension(axle.Suspension) ?? "Suspension";
            GameObject suspObj = new GameObject($"Suspension_{suspName}");
            suspObj.transform.SetParent(axleGO.transform, false);

            // attempt to read spindle COM => halfTrack to roghly locate the objects correctly
            float halfTrack = 0.0f;
            if (!string.IsNullOrEmpty(axle.Suspension))
            {
                JObject suspJson = UChVehGenJSONUtils.LoadJson(chronoVehicleDataRoot, axle.Suspension);
                if (suspJson != null && suspJson["Spindle"] is JObject spindleObj)
                {
                    if (spindleObj["COM"] is JArray comArr && comArr.Count == 3)
                    {
                        float sx = (float)comArr[0];
                        float sy = (float)comArr[1];
                        halfTrack = Mathf.Abs(sy);
                    }
                }
            }

            // visualise the axle with a cylinder (for now... update this and susp visualisation in the future)
            GameObject axleCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            axleCylinder.transform.SetParent(axleGO.transform, false);
            axleCylinder.transform.localPosition = Vector3.zero;
            axleCylinder.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            axleCylinder.transform.localScale = new Vector3(0.1f, halfTrack, 0.1f);

            // Determine wheel mesh prefabs for this axle: left vs. right
            GameObject thisAxleLeftMesh, thisAxleRightMesh;
            if (!uniformLRWheels)
            {
                // Single references for entire vehicle
                thisAxleLeftMesh = wheelMeshLeft;
                thisAxleRightMesh = wheelMeshRight;
            }
            else
            {
                // assume multiWheelPrefabs[axleIndex] has at least 2 elements
                if (multiWheelPrefabs.Count <= axleIndex || multiWheelPrefabs[axleIndex].Count < 2)
                {
                    Debug.LogWarning($"Axle {axleIndex} has insufficient wheel prefabs in multiWheelPrefabs!");
                    continue;
                }
                thisAxleLeftMesh = multiWheelPrefabs[axleIndex][0];
                thisAxleRightMesh = multiWheelPrefabs[axleIndex][1];
            }

            // set each side (left/right) from the JSON
            var sides = new[]
            {
                ("Left",  +1f, axle.LeftWheel, thisAxleLeftMesh),
                ("Right", -1f, axle.RightWheel, thisAxleRightMesh)
            };

            foreach (var (sideName, sideSign, rawTireJsonFromAxle, meshPrefab) in sides)
            {
                // If there's no "Wheel Input File" in the axle, or no mesh prefab, skip
                if (string.IsNullOrEmpty(rawTireJsonFromAxle) || meshPrefab == null)
                    continue;

                // figure out which tire json we actually use => single or multiple
                string finalTireJSON;
                if (useSingleTireFile)
                {
                    // override everything with the single file
                    finalTireJSON = selectedSingleTireFile;
                }
                else
                {
                    // For multiple: use axleTireFiles[axleIndex], if assigned
                    if (axleTireFiles != null && axleIndex < axleTireFiles.Count && !string.IsNullOrEmpty(axleTireFiles[axleIndex]))
                    {
                        finalTireJSON = axleTireFiles[axleIndex];
                    }
                    else
                    {
                        // fallback to the raw json from the axle, e.g. axle.LeftWheel (not tested)
                        finalTireJSON = rawTireJsonFromAxle;
                    }
                }

                // create the wheel GO
                GameObject wheelGO = CreateWheelGO(
                    axleGO,
                    meshPrefab,
                    sideSign * halfTrack,
                    axleIndex,
                    sideName,
                    offsetX: 0f
                );

                // Add to the sub-list for this axle
                axleWheelLists[axleIndex].Add(wheelGO);
            }
        }

        // Return the 2D structure for conversion to a flat list..
        return axleWheelLists;
    }

    
    // creates a wheel GameObject (or prefab instance), parents it under axleGO,
    // applies local position offset, and returns it.
    // This only handles a left and right wheel at the moment
    private GameObject CreateWheelGO(
        GameObject axleGO,
        GameObject meshPrefab,
        float halfTrackSign,
        int axleIndex,
        string sideAbbrev,
        float offsetX
    )
    {
        // instantiate or create the wheel GO
        GameObject wheelGO;
        if (meshPrefab != null)
        {
            wheelGO = (GameObject)PrefabUtility.InstantiatePrefab(meshPrefab);
            if (!wheelGO)
            {
                // fallback if the prefab fails to instantiate
                wheelGO = GameObject.Instantiate(meshPrefab);
            }
            wheelGO.name = $"Axle{axleIndex}_{sideAbbrev}_Wheel";
        }
        else
        {
            // If no mesh prefab, just create an empty GO with a name
            wheelGO = new GameObject($"Axle{axleIndex}_{sideAbbrev}");
            Debug.LogWarning($"No mesh/gameobject was assigned for the wheel: {sideAbbrev} on axle: {axleIndex}. Did you forget to assign in the generator?");
        }

        // Parent the GO to the axle, set local position and flick it back
        wheelGO.transform.SetParent(axleGO.transform, false);
        wheelGO.transform.localPosition = new Vector3(offsetX, 0f, halfTrackSign);
        return wheelGO;
    }
}
