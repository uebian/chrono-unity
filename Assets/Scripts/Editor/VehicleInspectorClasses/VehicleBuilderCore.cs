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
// Non-static vehicle builder for JSON file management and parsing ported from
//  UChVehGenJSONUtils to be instance-based
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChronoVehicleBuilder
{

    public class VehicleBuilderCore
    {
        // Path to ChronoData/vehicle root
        private string chronoVehicleDataRoot = "Assets/StreamingAssets/ChronoData/vehicle";

        // Cache for file metadata: relPath -> (Subfolder, Type, Name)
        private Dictionary<string, (string Subfolder, string Type, string Name)> fileMetaCache =
            new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);

        // Multi-level cache: subFolder -> Type -> List of file paths
        private Dictionary<string, Dictionary<string, List<string>>> subcomponentCache =
            new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);

        public string ChronoVehicleDataRoot => chronoVehicleDataRoot;

        public VehicleBuilderCore(string dataRoot = null)
        {
            if (!string.IsNullOrEmpty(dataRoot))
                chronoVehicleDataRoot = dataRoot;
            
            RebuildCache();
        }

        // Rebuild all caches
        public void RebuildCache()
        {
            BuildFileMetaCache();
            BuildSubcomponentCache();
        }

        // Build the file metadata cache
        public void BuildFileMetaCache()
        {
            fileMetaCache.Clear();

            if (!Directory.Exists(chronoVehicleDataRoot))
            {
                Debug.LogWarning($"VehicleBuilderCore: Data root does not exist: {chronoVehicleDataRoot}");
                return;
            }

            var allJsonPaths = Directory.GetFiles(chronoVehicleDataRoot, "*.json", SearchOption.AllDirectories);
            foreach (var absPath in allJsonPaths)
            {
                string relPath = ToRelativePath(absPath);
                if (string.IsNullOrEmpty(relPath))
                    continue;

                string subFolder = "";
                string[] parts = relPath.Split('/', '\\');
                if (parts.Length > 0)
                    subFolder = parts[0];

                try
                {
                    string text = File.ReadAllText(absPath);
                    if (JsonCommentHandling.TryParseJObject(text, out var root))
                    {
                        string typeVal = root["Type"]?.ToString() ?? "Unknown";
                        string nameVal = root["Name"]?.ToString() ?? Path.GetFileNameWithoutExtension(relPath);

                        fileMetaCache[relPath] = (subFolder, typeVal, nameVal);
                    }
                    else
                    {
                        fileMetaCache[relPath] = (subFolder, "Invalid", Path.GetFileNameWithoutExtension(relPath));
                    }
                }
                catch
                {
                    fileMetaCache[relPath] = (subFolder, "Invalid", Path.GetFileNameWithoutExtension(relPath));
                }
            }
        }

        // Build the subcomponent cache by subfolder and type
        private void BuildSubcomponentCache()
        {
            subcomponentCache.Clear();

            foreach (var kvp in fileMetaCache)
            {
                string relPath = kvp.Key;
                var (subFolder, typeVal, nameVal) = kvp.Value;

                if (!subcomponentCache.ContainsKey(subFolder))
                    subcomponentCache[subFolder] = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                if (!subcomponentCache[subFolder].ContainsKey(typeVal))
                    subcomponentCache[subFolder][typeVal] = new List<string>();

                subcomponentCache[subFolder][typeVal].Add(relPath);
            }
        }

        // Get files by type from cache
        public Dictionary<string, List<string>> GetSubcomponentFilesByType()
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in fileMetaCache)
            {
                string relPath = kvp.Key;
                string typeVal = kvp.Value.Type;

                if (!result.ContainsKey(typeVal))
                    result[typeVal] = new List<string>();
                result[typeVal].Add(relPath);
            }

            return result;
        }

        // Get files filtered by subfolder and type
        public List<string> GetFilesForSubfolderAndType(string subfolder, string type)
        {
            if (subcomponentCache.TryGetValue(subfolder, out var typeDict))
            {
                if (typeDict.TryGetValue(type, out var files))
                {
                    Debug.Log($"[GetFilesForSubfolderAndType] Found {files.Count} files for subfolder='{subfolder}', type='{type}'");
                    return new List<string>(files);
                }
                else
                {
                    Debug.LogWarning($"[GetFilesForSubfolderAndType] Type '{type}' not found in subfolder '{subfolder}'. Available types: {string.Join(", ", typeDict.Keys)}");
                }
            }
            else
            {
                Debug.LogWarning($"[GetFilesForSubfolderAndType] Subfolder '{subfolder}' not found in cache. Available subfolders: {string.Join(", ", subcomponentCache.Keys)}");
            }
            return new List<string>();
        }

        // Get files by type, filtered by vehicle type prefix (matches old VehGen behavior)
        // This filters ALL files of a given type by checking if the path contains the vehicle type string
        public List<string> GetFilesForVehicleTypeAndType(string vehicleTypePrefix, string type)
        {
            var result = new List<string>();
            var filesByType = GetSubcomponentFilesByType();
            
            if (filesByType.ContainsKey(type))
            {
                string folderPrefix = vehicleTypePrefix.ToLower();
                List<string> allFilesOfType = filesByType[type];
                
                foreach (var fileSubPath in allFilesOfType)
                {
                    if (fileSubPath.ToLower().Contains(folderPrefix))
                    {
                        result.Add(fileSubPath);
                    }
                }
            }
            
            return result;
        }

        // Get display name for a file path
        public string GetDisplayNameFor(string relPath)
        {
            if (fileMetaCache.TryGetValue(relPath, out var meta))
                return meta.Name;
            return Path.GetFileNameWithoutExtension(relPath);
        }

        // Load a JSON file
        public JObject LoadJson(string relativeSubPath)
        {
            if (string.IsNullOrEmpty(relativeSubPath))
            {
                Debug.LogError("LoadJson: relativeSubPath is null or empty.");
                return null;
            }

            string fullPath = GetFullPath(relativeSubPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"LoadJson: File not found: {fullPath}");
                return null;
            }

            try
            {
                // Read file with timeout protection
                string jsonStr;
                using (var fileStream = File.OpenText(fullPath))
                {
                    jsonStr = fileStream.ReadToEnd();
                }

                if (string.IsNullOrWhiteSpace(jsonStr))
                {
                    Debug.LogError($"LoadJson: File is empty: {fullPath}");
                    return null;
                }

                // Parse JSON with C++ style comment support
                JObject root;
                try
                {
                    root = JsonCommentHandling.ParseJObject(jsonStr, relativeSubPath);
                }
                catch (JsonReaderException jex)
                {
                    Debug.LogError($"LoadJson: JSON syntax error in {relativeSubPath} at line {jex.LineNumber}, position {jex.LinePosition}: {jex.Message}");
                    return null;
                }

                if (root == null)
                {
                    Debug.LogError($"LoadJson: Parsed to null: {fullPath}");
                    return null;
                }

                return root;
            }
            catch (IOException ioEx)
            {
                Debug.LogError($"LoadJson: IO error reading {relativeSubPath}: {ioEx.Message}");
                return null;
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Debug.LogError($"LoadJson: Access denied {relativeSubPath}: {uaEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"LoadJson: Unexpected error parsing {relativeSubPath}: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        // Save a JSON file
        public bool SaveJson(JObject obj, string relativeSubPath)
        {
            if (obj == null)
            {
                Debug.LogError("SaveJson: JObject is null.");
                return false;
            }
            if (string.IsNullOrEmpty(relativeSubPath))
            {
                Debug.LogError("SaveJson: relativeSubPath is null or empty.");
                return false;
            }

            try
            {
                string fullPath = GetFullPath(relativeSubPath);
                string dir = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string content = JsonConvert.SerializeObject(obj, Formatting.Indented);
                File.WriteAllText(fullPath, content);
                Debug.Log($"SaveJson: Wrote JSON to {fullPath}");

#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveJson: Exception saving {relativeSubPath}: {ex.Message}");
                return false;
            }
        }

        // Get full path from relative path
        public string GetFullPath(string relativeSubPath)
        {
            relativeSubPath = relativeSubPath.Replace("\\", "/");
            string combined = Path.Combine(chronoVehicleDataRoot, relativeSubPath);
            return Path.GetFullPath(combined);
        }

        // Convert absolute path to relative
        public string ToRelativePath(string absolutePath)
        {
            try
            {
                string rootFull = Path.GetFullPath(chronoVehicleDataRoot).Replace("\\", "/");
                string absFull = Path.GetFullPath(absolutePath).Replace("\\", "/");

                if (!absFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                    return null;

                string sub = absFull.Substring(rootFull.Length).TrimStart('/', '\\');
                return sub;
            }
            catch
            {
                return null;
            }
        }

        // Build display list from file paths
        public void BuildDisplayListFromFilenames(
            List<string> filePaths,
            out List<string> displayNames,
            out List<string> actualPaths)
        {
            displayNames = new List<string>();
            actualPaths = new List<string>();
            
            // Track duplicate base names to append suffix
            Dictionary<string, int> nameCount = new Dictionary<string, int>();
            
            foreach (var relPath in filePaths)
            {
                // Use filename without extension as base name (matches old VehGen behavior)
                string baseName = Path.GetFileNameWithoutExtension(relPath);
                
                // Check for duplicates and append suffix if needed
                if (!nameCount.ContainsKey(baseName))
                {
                    // First occurrence - no suffix
                    nameCount[baseName] = 1;
                    displayNames.Add(baseName);
                }
                else
                {
                    // Duplicate - append count suffix
                    int count = nameCount[baseName];
                    nameCount[baseName] = count + 1;
                    string displayName = $"{baseName} ({count})";
                    displayNames.Add(displayName);
                }
                
                actualPaths.Add(relPath);
            }
        }

        // Get all vehicle types (subfolders)
        public List<string> GetVehicleTypes()
        {
            return subcomponentCache.Keys.ToList();
        }

        // Determine forced type based on parent and child keys
        public static string GetForcedType(string parentKey, string childKey)
        {
            string p = parentKey?.Trim().ToLowerInvariant();
            string c = childKey?.Trim().ToLowerInvariant();

            return (p, c) switch
            {
                ("driveline", "input file") => "Driveline",
                ("steering subsystems", "input file") => "Steering",
                ("chassis", "input file") => "Chassis",
                ("engine", "input file") => "Engine",
                ("transmission", "input file") => "Transmission",
                (_, "suspension input file") => "Suspension",
                (_, "left wheel input file") or (_, "right wheel input file") => "Wheel",
                (_, "left brake input file") or (_, "right brake input file") => "Brake",
                _ => ""
            };
        }
    }

    // Vehicle data structure
    [Serializable]
    public class VehicleData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Template { get; set; }
        public List<AxleEntry> Axles { get; set; } = new List<AxleEntry>();
        public Dictionary<string, JToken> ExtraFields { get; set; } = new Dictionary<string, JToken>();
    }

    // Axle entry
    [Serializable]
    public class AxleEntry
    {
        [JsonProperty("Suspension Input File")]
        public string Suspension { get; set; }

        [JsonProperty("Suspension Location")]
        public Vector3 SuspensionLocation { get; set; }

        [JsonProperty("Steering Index")]
        public int SteeringIndex { get; set; }

        [JsonProperty("Left Wheel Input File")]
        public string LeftWheel { get; set; }

        [JsonProperty("Right Wheel Input File")]
        public string RightWheel { get; set; }

        [JsonProperty("Left Brake Input File")]
        public string LeftBrake { get; set; }

        [JsonProperty("Right Brake Input File")]
        public string RightBrake { get; set; }
    }
}
