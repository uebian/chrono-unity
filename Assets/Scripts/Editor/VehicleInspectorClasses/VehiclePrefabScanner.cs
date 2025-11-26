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
// Automatic prefab detection scans Resources/vehicle and ChronoVehicle folders
// for wheel/chassis prefabs to be auto applied on the vehicle
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChronoVehicleBuilder
{
    public class VehiclePrefabScanner
    {
        private Dictionary<string, List<GameObject>> prefabsByType = new Dictionary<string, List<GameObject>>();
        private List<string> searchPaths = new List<string>
        {
            "Assets/ChronoVehicle",
            "Assets/Resources/vehicle",
            "Assets/Chrono/vehicle",
            "Assets/Prefabs/vehicle"
        };

        public VehiclePrefabScanner()
        {
            ScanForPrefabs();
        }

        public void ScanForPrefabs()
        {
            prefabsByType.Clear();
            
            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath))
                    continue;

                // Find all prefabs in this directory
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { basePath });
                
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (prefab != null)
                    {
                        CategorizePrefab(prefab, path);
                    }
                }
            }
        }

        private void CategorizePrefab(GameObject prefab, string path)
        {
            string nameLower = prefab.name.ToLower();
            string pathLower = path.ToLower();

            // Detect wheel types
            if (nameLower.Contains("wheel") || pathLower.Contains("wheel"))
            {
                if (nameLower.Contains("left") || nameLower.Contains("_l") || nameLower.Contains("-l"))
                    AddPrefab("wheel_left", prefab);
                else if (nameLower.Contains("right") || nameLower.Contains("_r") || nameLower.Contains("-r"))
                    AddPrefab("wheel_right", prefab);
                else if (nameLower.Contains("front"))
                    AddPrefab("wheel_front", prefab);
                else if (nameLower.Contains("rear") || nameLower.Contains("back"))
                    AddPrefab("wheel_rear", prefab);
                else
                    AddPrefab("wheel", prefab);
            }
            // Detect chassis
            else if (nameLower.Contains("chassis") || nameLower.Contains("body") || nameLower.Contains("frame"))
            {
                AddPrefab("chassis", prefab);
            }
            // Detect tire
            else if (nameLower.Contains("tire") || nameLower.Contains("tyre"))
            {
                AddPrefab("tire", prefab);
            }
        }

        private void AddPrefab(string category, GameObject prefab)
        {
            if (!prefabsByType.ContainsKey(category))
                prefabsByType[category] = new List<GameObject>();
            
            if (!prefabsByType[category].Contains(prefab))
                prefabsByType[category].Add(prefab);
        }

        public List<GameObject> GetPrefabsOfType(string type)
        {
            if (prefabsByType.ContainsKey(type))
                return prefabsByType[type];
            return new List<GameObject>();
        }

        public GameObject GetBestChassisMatch(string vehicleType = "")
        {
            var chassis = GetPrefabsOfType("chassis");
            if (chassis.Count == 0) return null;

            if (!string.IsNullOrEmpty(vehicleType))
            {
                // Try to find matching vehicle type in folder path first
                foreach (var c in chassis)
                {
                    string assetPath = AssetDatabase.GetAssetPath(c);
                    if (assetPath.ToLower().Contains($"/{vehicleType.ToLower()}/") || 
                        assetPath.ToLower().Contains($"\\{vehicleType.ToLower()}\\"))
                    {
                        return c;
                    }
                }
                
                // Fallback: check name
                var match = chassis.FirstOrDefault(c => c.name.ToLower().Contains(vehicleType.ToLower()));
                if (match != null) return match;
            }

            // Default to HMMWV if available, otherwise use first found
            var hmmwv = chassis.FirstOrDefault(c => 
            {
                string path = AssetDatabase.GetAssetPath(c);
                return path.ToLower().Contains("/hmmwv/") || 
                       path.ToLower().Contains("\\hmmwv\\") ||
                       c.name.ToLower().Contains("hmmwv");
            });
            
            return hmmwv ?? chassis[0];
        }

        public GameObject GetBestWheelMatch(string side, string position = "", string vehicleType = "")
        {
            // Try specific side first
            string sideKey = side.ToLower().Contains("left") ? "wheel_left" : "wheel_right";
            var specificWheels = GetPrefabsOfType(sideKey);
            
            if (specificWheels.Count > 0)
            {
                if (!string.IsNullOrEmpty(vehicleType))
                {
                    // Try to find matching vehicle type in folder path first
                    foreach (var w in specificWheels)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(w);
                        if (assetPath.ToLower().Contains($"/{vehicleType.ToLower()}/") || 
                            assetPath.ToLower().Contains($"\\{vehicleType.ToLower()}\\"))
                        {
                            return w;
                        }
                    }
                    
                    // Fallback: check name
                    var match = specificWheels.FirstOrDefault(w => w.name.ToLower().Contains(vehicleType.ToLower()));
                    if (match != null) return match;
                }
                
                // Default to HMMWV if available, otherwise use first found
                var hmmwv = specificWheels.FirstOrDefault(w => 
                {
                    string path = AssetDatabase.GetAssetPath(w);
                    return path.ToLower().Contains("/hmmwv/") || 
                           path.ToLower().Contains("\\hmmwv\\") ||
                           w.name.ToLower().Contains("hmmwv");
                });
                
                return hmmwv ?? specificWheels[0];
            }

            // Fallback to generic wheels
            var genericWheels = GetPrefabsOfType("wheel");
            if (genericWheels.Count > 0)
            {
                if (!string.IsNullOrEmpty(vehicleType))
                {
                    // Try to find matching vehicle type in folder path first
                    foreach (var w in genericWheels)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(w);
                        if (assetPath.ToLower().Contains($"/{vehicleType.ToLower()}/") || 
                            assetPath.ToLower().Contains($"\\{vehicleType.ToLower()}\\"))
                        {
                            return w;
                        }
                    }
                    
                    // Fallback: check name
                    var match = genericWheels.FirstOrDefault(w => w.name.ToLower().Contains(vehicleType.ToLower()));
                    if (match != null) return match;
                }
                
                // Default to HMMWV if available, otherwise use first found
                var hmmwv = genericWheels.FirstOrDefault(w => 
                {
                    string path = AssetDatabase.GetAssetPath(w);
                    return path.ToLower().Contains("/hmmwv/") || 
                           path.ToLower().Contains("\\hmmwv\\") ||
                           w.name.ToLower().Contains("hmmwv");
                });
                
                return hmmwv ?? genericWheels[0];
            }

            return null;
        }

        public void DrawPrefabSelector(string label, string category, ref GameObject selected, float labelWidth = 120f)
        {
            var prefabs = GetPrefabsOfType(category);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
            
            if (prefabs.Count == 0)
            {
                selected = (GameObject)EditorGUILayout.ObjectField(selected, typeof(GameObject), false);
            }
            else
            {
                // Create dropdown with found prefabs
                string[] names = prefabs.Select(p => p.name).ToArray();
                int currentIndex = selected != null ? prefabs.IndexOf(selected) : -1;
                if (currentIndex < 0) currentIndex = 0;
                
                int newIndex = EditorGUILayout.Popup(currentIndex, names);
                if (newIndex >= 0 && newIndex < prefabs.Count)
                    selected = prefabs[newIndex];
                
                // Manual override option
                selected = (GameObject)EditorGUILayout.ObjectField(selected, typeof(GameObject), false, GUILayout.Width(100));
            }
            
            EditorGUILayout.EndHorizontal();
        }

        public int GetTotalPrefabsFound()
        {
            return prefabsByType.Values.Sum(list => list.Count);
        }

        public Dictionary<string, int> GetPrefabCountsByType()
        {
            return prefabsByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
        }

        // Get complete vehicle setup for a given vehicle type
        public VehicleSetup GetCompleteVehicleSetup(string vehicleType, int numAxles = 2)
        {
            var setup = new VehicleSetup();
            
            // Get chassis
            setup.Chassis = GetBestChassisMatch(vehicleType);
            
            // Get wheels for each axle
            setup.Wheels = new List<WheelPair>();
            for (int i = 0; i < numAxles; i++)
            {
                string position = i == 0 ? "front" : "rear";
                setup.Wheels.Add(new WheelPair
                {
                    Left = GetBestWheelMatch("left", position, vehicleType),
                    Right = GetBestWheelMatch("right", position, vehicleType)
                });
            }
            
            return setup;
        }

        // Helper class to store complete vehicle prefab setup
        public class VehicleSetup
        {
            public GameObject Chassis;
            public List<WheelPair> Wheels = new List<WheelPair>();
            
            public bool HasChassis => Chassis != null;
            public bool HasAllWheels
            {
                get
                {
                    if (Wheels.Count == 0) return false;
                    return Wheels.All(w => w.Left != null && w.Right != null);
                }
            }
            public bool IsComplete => HasChassis && HasAllWheels;
        }

        public class WheelPair
        {
            public GameObject Left;
            public GameObject Right;
        }
    }
}
#endif
