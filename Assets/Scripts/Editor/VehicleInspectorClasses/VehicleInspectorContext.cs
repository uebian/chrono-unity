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
// Handles vehicle data models for the VehicleBuilder inspector system -
// single context ctonater for all tabs to share state and data
// =============================================================================

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using ChronoVehicleBuilder;

namespace VehicleBuilder.Editor
{

    public class VehicleInspectorContext
    {
        // Target vehicle being inspected
        public UWheeledVehicle Vehicle { get; private set; }
        
        // Centralized JSON state manager (single source of truth)
        public VehicleJsonState JsonState { get; private set; }
        
        // Core systems
        public VehicleBuilderCore BuilderCore { get; private set; }
        public VehicleJSONParser JsonParser { get; private set; }
        public VehiclePrefabScanner PrefabScanner { get; private set; }
        public SyntaxHighlightedJsonEditor TextEditor { get; private set; }
        public VehicleMapEditor MapEditor { get; private set; }
        
        // Cached file lists
        public List<string> VehicleJsonFiles { get; set; }
        public List<string> EngineJsonFiles { get; set; }
        public List<string> TransmissionJsonFiles { get; set; }
        public List<string> TireJsonFiles { get; set; }

        // Per-type cache for quick component selectors
        private readonly Dictionary<string, List<string>> componentFileCache =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        
        // Current vehicle type
        public string CurrentVehicleType { get; set; }
        
        // Parsed vehicle data
        public VehicleDataModel.VehicleData ParsedVehicleData { get; set; }
        
        // Callbacks for inspector-level operations
        public Action OnVehicleJsonChanged { get; set; }
        public Action OnVehicleTypeChanged { get; set; }
    public Action OnVehicleStructureChanged { get; set; }
        
        public VehicleInspectorContext(
            UWheeledVehicle vehicle,
            VehicleJsonState jsonState,
            VehicleBuilderCore builderCore,
            VehicleJSONParser jsonParser,
            VehiclePrefabScanner prefabScanner,
            SyntaxHighlightedJsonEditor textEditor,
            VehicleMapEditor mapEditor)
        {
            Vehicle = vehicle;
            JsonState = jsonState;
            BuilderCore = builderCore;
            JsonParser = jsonParser;
            PrefabScanner = prefabScanner;
            TextEditor = textEditor;
            MapEditor = mapEditor;
            
            VehicleJsonFiles = new List<string>();
            EngineJsonFiles = new List<string>();
            TransmissionJsonFiles = new List<string>();
            TireJsonFiles = new List<string>();
        }
        
        public void RefreshFileLists()
        {
            // Use the same filtering logic as the old vehicle generator tool
            VehicleJsonFiles = BuilderCore.GetFilesForVehicleTypeAndType(CurrentVehicleType, "Vehicle");
            EngineJsonFiles = BuilderCore.GetFilesForVehicleTypeAndType(CurrentVehicleType, "Engine");
            TransmissionJsonFiles = BuilderCore.GetFilesForVehicleTypeAndType(CurrentVehicleType, "Transmission");
            TireJsonFiles = BuilderCore.GetFilesForVehicleTypeAndType(CurrentVehicleType, "Tire");

            componentFileCache.Clear();
        }

        /// <summary>
        /// Ensures the top-level vehicle JSON is loaded and parsed
        /// </summary>
        public void EnsureVehicleJsonLoaded(bool forceReload = false)
        {
            if (string.IsNullOrEmpty(JsonState.VehiclePath))
                return;

            if (!forceReload && JsonState.VehicleData != null)
                return;

            try
            {
                var data = BuilderCore.LoadJson(JsonState.VehiclePath);
                if (data != null)
                {
                    var parsed = VehicleJsonDataParser.ParseVehicleData(data);
                    JsonState.LoadVehicle(data, parsed);
                    ParsedVehicleData = parsed;
                    OnVehicleStructureChanged?.Invoke();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load vehicle JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Rebuilds the parsed vehicle data from the currently loaded JSON object
        /// Call after mutating JsonState.VehicleData directly
        /// </summary>
        public void RefreshParsedVehicleData()
        {
            if (JsonState.VehicleData == null)
                return;

            var parsed = VehicleJsonDataParser.ParseVehicleData(JsonState.VehicleData);
            JsonState.LoadVehicle(JsonState.VehicleData, parsed);
            ParsedVehicleData = parsed;
            OnVehicleStructureChanged?.Invoke();
        }

        /// <summary>
        /// Cached lookup for component JSONs (e.g., Suspension, Steering, Driveline) scoped to the active vehicle type
        /// </summary>
        public List<string> GetFilesForComponentType(string componentType)
        {
            if (string.IsNullOrEmpty(componentType))
            {
                return new List<string>();
            }

            if (componentFileCache.TryGetValue(componentType, out var cached))
            {
                return cached;
            }

            var files = BuilderCore.GetFilesForVehicleTypeAndType(CurrentVehicleType, componentType) ?? new List<string>();
            componentFileCache[componentType] = files;
            return files;
        }
        
        /// <summary>
        /// Load engine JSON if path is set and data is not already loaded
        /// </summary>
        public void EnsureEngineJsonLoaded(bool forceReload = false)
        {
            if (!string.IsNullOrEmpty(JsonState.EnginePath))
            {
                if (forceReload || JsonState.EngineData == null)
                {
                    try
                    {
                        var data = BuilderCore.LoadJson(JsonState.EnginePath);
                        JsonState.LoadEngine(data);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to auto-load engine JSON: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Load transmission JSON if path is set and data is not already loaded
        /// Called when switching to transmission tab or when transmission path changes
        /// </summary>
        public void EnsureTransmissionJsonLoaded(bool forceReload = false)
        {
            if (!string.IsNullOrEmpty(JsonState.TransmissionPath))
            {
                if (forceReload || JsonState.TransmissionData == null)
                {
                    try
                    {
                        var data = BuilderCore.LoadJson(JsonState.TransmissionPath);
                        JsonState.LoadTransmission(data);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to auto-load transmission JSON: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Load tire JSON if path is set and data is not already loaded
        /// Called when switching to tire tab or when tire path changes
        /// </summary>
        public void EnsureTireJsonLoaded(bool forceReload = false)
        {
            if (!string.IsNullOrEmpty(JsonState.TirePath))
            {
                if (forceReload || JsonState.TireData == null)
                {
                    try
                    {
                        var data = BuilderCore.LoadJson(JsonState.TirePath);
                        JsonState.LoadTire(data);
                        Debug.Log($"Auto-loaded tire JSON: {JsonState.TirePath}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to auto-load tire JSON: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Invalidate cached JSON data when paths change and forces reload
        /// </summary>
        public void InvalidateJsonCache()
        {
            JsonState.LoadEngine(null);
            JsonState.LoadTransmission(null);
            JsonState.LoadTire(null);
            Debug.Log("Invalidated JSON cache - will reload on next tab visit");
        }

        /// <summary>
        /// Attempt to seed per-axle/per-wheel tire overrides with front/rear JSON defaults based on naming
        /// Returns true if any overrides were updated
        /// </summary>
        public bool TryAssignFrontRearTireDefaults(int axleCount)
        {
            if (Vehicle == null || Vehicle.useSingleTireFile)
            {
                return false;
            }

            if (axleCount <= 0)
            {
                axleCount = Vehicle.axleData?.Count ?? 0;
            }

            if (axleCount <= 0)
            {
                return false;
            }

            if (TireJsonFiles == null || TireJsonFiles.Count == 0)
            {
                return false;
            }

            string frontTire = FindTireJsonContaining("front");
            string rearTire = FindTireJsonContaining("rear");

            if (string.IsNullOrEmpty(frontTire) || string.IsNullOrEmpty(rearTire))
            {
                return false;
            }

            bool updated = false;

            switch (Vehicle.tireAssignmentMode)
            {
                case UWheeledVehicle.TireAssignmentMode.PerAxleList:
                    for (int axle = 0; axle < Vehicle.perAxleTireSpec.Count && axle < axleCount; axle++)
                    {
                        Vehicle.perAxleTireSpec[axle] = axle == 0 ? frontTire : rearTire;
                        updated = true;
                    }
                    break;

                case UWheeledVehicle.TireAssignmentMode.PerWheelList:
                    int wheelsPerAxle = GuessWheelsPerAxle();
                    if (wheelsPerAxle <= 0)
                    {
                        wheelsPerAxle = 2;
                    }

                    int entryIndex = 0;
                    for (int axle = 0; axle < axleCount && entryIndex < Vehicle.perAxleTireSpec.Count; axle++)
                    {
                        for (int wheel = 0; wheel < wheelsPerAxle && entryIndex < Vehicle.perAxleTireSpec.Count; wheel++)
                        {
                            Vehicle.perAxleTireSpec[entryIndex++] = axle == 0 ? frontTire : rearTire;
                            updated = true;
                        }
                    }
                    break;
            }

            if (updated)
            {
                Vehicle.RefreshTireOverridesFromInspector(forceReset: false);
            }

            return updated;
        }

        private string FindTireJsonContaining(string keyword)
        {
            if (string.IsNullOrEmpty(keyword) || TireJsonFiles == null)
            {
                return null;
            }

            string loweredKeyword = keyword.ToLowerInvariant();
            return TireJsonFiles.FirstOrDefault(path => !string.IsNullOrEmpty(path) && path.ToLowerInvariant().Contains(loweredKeyword));
        }

        private int GuessWheelsPerAxle()
        {
            if (Vehicle?.axleData != null)
            {
                foreach (var axle in Vehicle.axleData)
                {
                    if (axle?.visualGameobjects != null && axle.visualGameobjects.Count > 0)
                    {
                        return axle.visualGameobjects.Count;
                    }
                }
            }

            return 0;
        }
    }
}
