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
// Json state manager for all paths and loaded data in vehicle inspector
// =============================================================================

using Newtonsoft.Json.Linq;
using ChronoVehicleBuilder;

namespace VehicleBuilder.Editor
{

    public class VehicleJsonState
    {
        // Current JSON paths (single source of truth)
        public string VehiclePath { get; private set; } = "";
        public string EnginePath { get; private set; } = "";
        public string TransmissionPath { get; private set; } = "";
        public string TirePath { get; private set; } = "";
        
        // Loaded JSON data
        public JObject VehicleData { get; private set; }
        public JObject EngineData { get; private set; }
        public JObject TransmissionData { get; private set; }
        public JObject TireData { get; private set; }
        
        // Parsed vehicle structure
        public VehicleDataModel.VehicleData ParsedVehicleData { get; private set; }
        
        // Loaded flags
        public bool VehicleLoaded { get; private set; }
        public bool EngineLoaded { get; private set; }
        public bool TransmissionLoaded { get; private set; }
        public bool TireLoaded { get; private set; }
        
        // Set paths and mark for reload
        public void SetVehiclePath(string path)
        {
            if (VehiclePath != path)
            {
                VehiclePath = path;
                VehicleData = null;
                VehicleLoaded = false;
                ParsedVehicleData = null;
            }
        }
        
        public void SetEnginePath(string path)
        {
            if (EnginePath != path)
            {
                EnginePath = path;
                EngineData = null;
                EngineLoaded = false;
            }
        }
        
        public void SetTransmissionPath(string path)
        {
            if (TransmissionPath != path)
            {
                TransmissionPath = path;
                TransmissionData = null;
                TransmissionLoaded = false;
            }
        }
        
        public void SetTirePath(string path)
        {
            if (TirePath != path)
            {
                TirePath = path;
                TireData = null;
                TireLoaded = false;
            }
        }
        
        // Load and cache data
        public void LoadVehicle(JObject data, VehicleDataModel.VehicleData parsed)
        {
            VehicleData = data;
            ParsedVehicleData = parsed;
            VehicleLoaded = true;
        }
        
        public void LoadEngine(JObject data)
        {
            EngineData = data;
            EngineLoaded = true;
        }
        
        public void LoadTransmission(JObject data)
        {
            TransmissionData = data;
            TransmissionLoaded = true;
        }
        
        public void LoadTire(JObject data)
        {
            TireData = data;
            TireLoaded = true;
        }
        
        // Invalidate all
        public void InvalidateAll()
        {
            VehicleData = null;
            EngineData = null;
            TransmissionData = null;
            TireData = null;
            ParsedVehicleData = null;
            VehicleLoaded = false;
            EngineLoaded = false;
            TransmissionLoaded = false;
            TireLoaded = false;
        }
        
        // Individual invalidations
        public void InvalidateVehicle()
        {
            VehicleData = null;
            ParsedVehicleData = null;
            VehicleLoaded = false;
        }
        
        public void InvalidateEngine()
        {
            EngineData = null;
            EngineLoaded = false;
        }
        
        public void InvalidateTransmission()
        {
            TransmissionData = null;
            TransmissionLoaded = false;
        }
        
        public void InvalidateTire()
        {
            TireData = null;
            TireLoaded = false;
        }
    }
}
