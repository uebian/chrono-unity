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
//
// Utility class for vehicle setup operations like auto-assigning prefabs,
// wheels, and JSON files based on vehicle type - not in the main class just
// to avoid monolithic size where possible
// =============================================================================

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ChronoVehicleBuilder;

namespace VehicleBuilder.Editor
{

    public static class VehicleSetupUtility
    {

        public static void AutoAssignVehiclePrefabs(
            UWheeledVehicle vehicle,
            string currentVehicleType,
            VehiclePrefabScanner prefabScanner,
            VehicleBuilderCore builderCore,
            VehicleDataModel.VehicleData parsedVehicleData,
            bool vehicleJsonLoaded,
            System.Action loadVehicleJsonData,
            System.Action cleanupExistingAxlesAndWheels)
        {
            // Ensure vehicle JSON is loaded to get axle data
            if (!vehicleJsonLoaded && !string.IsNullOrEmpty(vehicle.topLevelVehicleJSON))
            {
                loadVehicleJsonData?.Invoke();
            }
            
            // CRITICAL: Clean up ALL existing axles and wheels before creating new ones
            cleanupExistingAxlesAndWheels?.Invoke();
            
            // Auto-detect and assign complete vehicle setup when type changes
            int numAxles = vehicle.axleData?.Count ?? 2;
            var vehicleSetup = prefabScanner.GetCompleteVehicleSetup(currentVehicleType, numAxles);
            
            if (!vehicleSetup.HasChassis && !vehicleSetup.HasAllWheels)
            {
                Debug.Log($"No prefabs found for vehicle type: {currentVehicleType}");
                return;
            }
            
            // Assign chassis
            if (vehicleSetup.HasChassis)
            {
                AssignChassisPrefab(vehicle, vehicleSetup.Chassis);
            }
            
            // Assign wheels with proper positioning if we have parsed vehicle data
            if (vehicleSetup.HasAllWheels && vehicle.axleData != null)
            {
                for (int axleIdx = 0; axleIdx < numAxles && axleIdx < vehicleSetup.Wheels.Count; axleIdx++)
                {
                    var axle = vehicle.axleData[axleIdx];
                    if (axle.visualGameobjects == null)
                        axle.visualGameobjects = new List<GameObject>();
                    
                    while (axle.visualGameobjects.Count < 2)
                        axle.visualGameobjects.Add(null);
                    
                    var wheelPair = vehicleSetup.Wheels[axleIdx];
                    
                    // Get axle position and spacing from parsed data
                    Vector3 axlePosition = Vector3.zero;
                    float halfTrack = 1.0f; // Default wheel spacing
                    
                    if (parsedVehicleData != null && parsedVehicleData.Axles != null && axleIdx < parsedVehicleData.Axles.Count)
                    {
                        var axleData = parsedVehicleData.Axles[axleIdx];
                        axlePosition = new Vector3((axleData.SuspensionLocation.x), (axleData.SuspensionLocation.z), -(axleData.SuspensionLocation.y)
                        );
                        
                        // Try to get half-track from suspension JSON
                        if (!string.IsNullOrEmpty(axleData.Suspension))
                        {
                            halfTrack = GetSuspensionHalfTrack(builderCore, axleData.Suspension);
                        }
                    }
                    
                    // Create or update axle container
                    Transform axleContainer = vehicle.transform.Find($"Axle_{axleIdx}");
                    if (axleContainer == null)
                    {
                        GameObject axleGO = new GameObject($"Axle_{axleIdx}");
                        axleGO.transform.SetParent(vehicle.transform);
                        axleGO.transform.localPosition = axlePosition;
                        axleContainer = axleGO.transform;
                        
                        // Create axle visualization
                        CreateAxleVisualization(axleContainer, halfTrack);
                    }
                    else
                    {
                        Debug.LogWarning($"Axle_{axleIdx} already exists! Updating position from {axleContainer.localPosition} to {axlePosition}");
                        axleContainer.localPosition = axlePosition;
                        // Update axle visualization
                        CreateAxleVisualization(axleContainer, halfTrack);
                    }
                    
                    // Assign left wheel
                    if (wheelPair.Left != null)
                    {
                        axle.visualGameobjects[0] = AssignWheel(wheelPair.Left, axleContainer, "Left", halfTrack);
                    }
                    
                    // Assign right wheel
                    if (wheelPair.Right != null)
                    {
                        axle.visualGameobjects[1] = AssignWheel(wheelPair.Right, axleContainer, "Right", -halfTrack);
                    }
                }
            }
            
            EditorUtility.SetDirty(vehicle);
        }
        
        public static GameObject AssignWheel(GameObject wheelPrefab, Transform axleContainer, string sideName, float lateralOffset)
        {
            // Find or create wheel instance
            string wheelName = $"Wheel_{sideName}";
            Transform existingWheel = axleContainer.Find(wheelName);
            
            GameObject wheelInstance;
            if (existingWheel != null)
            {
                // Replace existing wheel
                Object.DestroyImmediate(existingWheel.gameObject);
            }
            
            // Instantiate new wheel
            wheelInstance = (GameObject)PrefabUtility.InstantiatePrefab(wheelPrefab, axleContainer);
            if (wheelInstance == null)
                wheelInstance = Object.Instantiate(wheelPrefab, axleContainer);
            
            wheelInstance.name = wheelName;
            wheelInstance.transform.localPosition = new Vector3(0, 0, lateralOffset);
            wheelInstance.transform.localRotation = Quaternion.identity;
                       
            return wheelInstance;
        }
        
        public static float GetSuspensionHalfTrack(VehicleBuilderCore builderCore, string suspensionJsonPath)
        {
            try
            {
                JObject suspJson = builderCore.LoadJson(suspensionJsonPath);
                if (suspJson != null && suspJson["Spindle"] is JObject spindleObj)
                {
                    if (spindleObj["COM"] is JArray comArr && comArr.Count >= 3)
                    {
                        float sy = comArr[1]?.ToObject<float>() ?? 0f;
                        return Mathf.Abs(sy);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to read suspension half-track from {suspensionJsonPath}: {ex.Message}");
            }
            
            return 1.0f; // Default half-track
        }
        
        /// <summary>
        /// Get chassis centroidal frame COM location from chassis JSON file
        /// Returns the offset in Chrono coordinates (X=forward, Y=left, Z=up)
        /// </summary>
        public static Vector3 GetChassisCOMOffset(VehicleBuilderCore builderCore, string chassisJsonPath)
        {
            if (string.IsNullOrEmpty(chassisJsonPath))
                return Vector3.zero;
            
            try
            {
                JObject chassisJson = builderCore.LoadJson(chassisJsonPath);
                if (chassisJson != null && chassisJson["Components"] is JArray componentsArr && componentsArr.Count > 0)
                {
                    var firstComponent = componentsArr[0] as JObject;
                    if (firstComponent != null && firstComponent["Centroidal Frame"] is JObject centroidalFrame)
                    {
                        if (centroidalFrame["Location"] is JArray locArr && locArr.Count >= 3)
                        {
                            float cx = locArr[0]?.ToObject<float>() ?? 0f;
                            float cy = locArr[1]?.ToObject<float>() ?? 0f;
                            float cz = locArr[2]?.ToObject<float>() ?? 0f;
                            
                            // Return in Chrono coords (will be converted by caller)
                            return new Vector3(cx, cy, cz);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to read chassis COM from {chassisJsonPath}: {ex.Message}");
            }
            
            return Vector3.zero;
        }
        
        public static void CreateAxleVisualization(Transform axleContainer, float halfTrack)
        {
            // Remove existing axle tube if present
            Transform existingTube = axleContainer.Find("AxleTube");
            if (existingTube != null)
                Object.DestroyImmediate(existingTube.gameObject);
            
            // Create axle tube visualization
            GameObject axleTube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            axleTube.name = "AxleTube";
            axleTube.transform.SetParent(axleContainer);
            axleTube.transform.localPosition = Vector3.zero;
            axleTube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // Rotate 90° around X to align with Z-axis
            axleTube.transform.localScale = new Vector3(0.1f, halfTrack, 0.1f);
            
            // Set URP/Lit material to dark gray
            var renderer = axleTube.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Try to use URP/Lit shader, fallback to Standard if not available
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader == null)
                    urpShader = Shader.Find("Standard");
                
                renderer.sharedMaterial = new Material(urpShader);
                renderer.sharedMaterial.color = new Color(0.2f, 0.2f, 0.2f);
            }
        }
        
        public static void AssignChassisPrefab(UWheeledVehicle vehicle, GameObject chassisPrefab)
        {
            AssignChassisPrefab(vehicle, chassisPrefab, Vector3.zero);
        }
        
        // most positioning here is estimated for the editor view - but in runtime chrono takes over
        // but the user will need to place the chassis properly within the child object if needed
        public static void AssignChassisPrefab(UWheeledVehicle vehicle, GameObject chassisPrefab, Vector3 chassisCOMOffset)
        {
            // Find or create "Chassis" child object - must be a DIRECT child
            Transform chassisTransform = null;
            foreach (Transform child in vehicle.transform)
            {
                if (child.name == "Chassis" && child.childCount > 0)
                {
                    chassisTransform = child;
                    break;
                }
            }
            
            if (chassisTransform != null)
            {
                // Clear existing chassis children - destroy ALL children
                int childCount = chassisTransform.childCount;
                Debug.Log($"Found existing Chassis container with {childCount} children. Removing ALL...");
                
                // Collect all children first (to avoid modifying collection while iterating)
                List<GameObject> childrenToDestroy = new List<GameObject>();
                foreach (Transform child in chassisTransform)
                {
                    childrenToDestroy.Add(child.gameObject);
                }
                
                // Now destroy them all
                foreach (GameObject child in childrenToDestroy)
                {
                    Object.DestroyImmediate(child);
                }
            }
            else
            {
                // Create new chassis container
                GameObject chassisContainer = new GameObject("Chassis");
                chassisContainer.transform.SetParent(vehicle.transform);
                chassisContainer.transform.localPosition = -chassisCOMOffset;  // Offset by negative COM
                chassisContainer.transform.localRotation = Quaternion.identity;
                chassisTransform = chassisContainer.transform;
            }
            
            // Set position with COM offset (if container already existed, update it)
            chassisTransform.localPosition = -chassisCOMOffset;
                      
            // Instantiate chassis prefab as child
            GameObject chassisInstance = (GameObject)PrefabUtility.InstantiatePrefab(chassisPrefab, chassisTransform);
            if (chassisInstance == null)
            {
                Debug.Log("PrefabUtility.InstantiatePrefab returned null, using Object.Instantiate instead");
                chassisInstance = Object.Instantiate(chassisPrefab, chassisTransform);
            }
            
            chassisInstance.transform.localPosition = Vector3.zero;
            chassisInstance.transform.localRotation = Quaternion.identity;
            
            // Unpack the prefab instance so we can reparent its children
            PrefabUtility.UnpackPrefabInstance(chassisInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            
            // Flatten hierarchy: move mesh children up to chassis container and destroy wrapper
            List<Transform> meshChildren = new List<Transform>();
            foreach (Transform child in chassisInstance.transform)
            {
                meshChildren.Add(child);
            }
            
            foreach (Transform child in meshChildren)
            {
                child.SetParent(chassisTransform);
            }
            
            // Destroy the prefab wrapper GameObject
            Object.DestroyImmediate(chassisInstance);
            
            MeshRenderer[] renderers = chassisTransform.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                // Debug.Log($"  - Renderer on {renderer.gameObject.name}");
            }
            
            EditorUtility.SetDirty(vehicle);
        }
        
        public static void AutoAssignAllWheels(UWheeledVehicle vehicle)
        {
            int numAxles = vehicle.axleData?.Count ?? 0;
            int assignedCount = 0;
            
            for (int axleIdx = 0; axleIdx < numAxles; axleIdx++)
            {
                var axle = vehicle.axleData[axleIdx];
                if (axle.visualGameobjects == null)
                    axle.visualGameobjects = new List<GameObject>();
                
                while (axle.visualGameobjects.Count < 2)
                    axle.visualGameobjects.Add(null);
                
                // Find axle container
                Transform axleContainer = vehicle.transform.Find($"Axle_{axleIdx}");
                if (axleContainer == null)
                    continue;
                
                // Auto-assign left wheel
                Transform leftWheel = axleContainer.Find("Wheel_Left");
                if (leftWheel != null && axle.visualGameobjects[0] == null)
                {
                    axle.visualGameobjects[0] = leftWheel.gameObject;
                    assignedCount++;
                }
                
                // Auto-assign right wheel
                Transform rightWheel = axleContainer.Find("Wheel_Right");
                if (rightWheel != null && axle.visualGameobjects[1] == null)
                {
                    axle.visualGameobjects[1] = rightWheel.gameObject;
                    assignedCount++;
                }
            }
            
            if (assignedCount > 0)
            {
                EditorUtility.SetDirty(vehicle);
            }
            else
            {
                Debug.LogWarning("No wheels found to auto-assign. Make sure wheels are named 'Wheel_Left' and 'Wheel_Right' under axle containers.");
            }
        }
    }
}
