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
// Generic Json-built UWheeledVehicle. This has been tested with the in-built
// HMMWV, Gator, MAN and UAZ jsons and hence should work for most of the chrono
// wheeled vehicles.
// =============================================================================


using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic; // for List<>
using Newtonsoft.Json.Linq;


// This is essentially a storage class for holding the UWheeledVehicle generated data (mostly linking gameobjects to chrono tires)
// doing this as a class (vs. a list in a list) allows unity to present parent child arrays/lists in the inspector
[System.Serializable]
public class WheelGameobjects
{
    // All wheel objects attached to this axle
    public List<GameObject> visualGameobjects;
}

public class UWheeledVehicle : UChVehicle
{
    [Header("JSON for the vehicle")]
    public string topLevelVehicleJSON;  // e.g. "hmmwv/vehicle/HMMWV_Vehicle_4WD.json"

    [Header("Powertrain JSON")]
    public string engineJSON;
    public string transJSON;

    [Header("Tire JSON")]
    [Tooltip("If true, uses the single 'tireJSON' for all wheels. Otherwise, fill perAxleTireSpec for each wheel.")]
    public bool useSingleTireFile = true;
    public string tireJSON = "hmmwv/tire/HMMWV_TMeasyTire.json";

    /// <summary>
    /// If useSingleTireFile == false, you can store multiple tire JSON references here,
    /// in the order that wheels appear in Chrono:
    ///    Axle 0 => (LEFT, RIGHT),
    ///    Axle 1 => (LEFT, RIGHT), etc.
    /// </summary>
    [Header("If not single, multiple tire JSONs (one per wheel)")]
    public List<string> perAxleTireSpec = new List<string>();

    [Header("Initial Conditions")]
    public bool chassisFixed = false;
    public bool brakeLocking = false;
    public double initForwardVel = 0;
    [Tooltip("Init Wheel Angle Velocity not currently implemented")]
    public double initWheelAngVel = 0;
    public float tireStepSize = 0.001f;

    [Header("Collision / Visualisation")]
    public ChTire.CollisionType tireCollisionType = ChTire.CollisionType.SINGLE_POINT;

    [Header("Axle Data (Gameobjects to Axles)")]
    [SerializeField]
    public List<WheelGameobjects> axleData = new List<WheelGameobjects>();

    // pointer to the built WheeledVehicle
    private WheeledVehicle UChJSONVehicle = null;


    
    //////// Functions
    // At start, call the builder to create the vehicle from the json specs
    protected override void OnStart()
    {
        bool success = BuildFromTopLevelJSON(topLevelVehicleJSON);
        if (!success)
        {
            Debug.LogWarning($"{name}: JSON load failed, so no vehicle was built.");
        }
    }

    // Build from JSON
    private bool BuildFromTopLevelJSON(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Debug.LogWarning($"{name}: No top-level JSON file specified.");
            return false;
        }

        // resolve actual file path
        string vehicleJson = chrono_vehicle.GetVehicleDataFile(fileName);
        if (!File.Exists(vehicleJson))
        {
            Debug.LogWarning($"{name}: top-level JSON not found: {vehicleJson}");
            return false;
        }

        // sanity check that it's Type=Vehicle
        string text = File.ReadAllText(vehicleJson);
        if (!text.Contains("\"Type\": \"Vehicle\""))
        {
            Debug.LogWarning($"{name}: top-level JSON not recognized as Type=Vehicle: {fileName}");
            return false;
        }

        try
        {
            // Instantiate the Chrono WheeledVehicle using the JSON
            UChJSONVehicle = new WheeledVehicle(UChSystem.chrono_system, vehicleJson);

            // initialise
            var csys = new ChCoordsysd(Utils.ToChronoFlip(transform.position), Utils.ToChronoFlip(transform.rotation));
            UChJSONVehicle.Initialize(csys, initForwardVel);

            // set chassis fixed / brake locking
            UChJSONVehicle.GetChassis().SetFixed(chassisFixed);
            UChJSONVehicle.EnableBrakeLocking(brakeLocking);
            
            // give it a name
            UChJSONVehicle.SetName(this.gameObject.name);

            // read & initialise engine+trans
            if (!string.IsNullOrEmpty(engineJSON) && !string.IsNullOrEmpty(transJSON))
            {
                string engPath = chrono_vehicle.GetVehicleDataFile(engineJSON);
                string transPath = chrono_vehicle.GetVehicleDataFile(transJSON);

                if (File.Exists(engPath) && File.Exists(transPath))
                {
                    ChEngine engine = chrono_vehicle.ReadEngineJSON(engPath);
                    ChTransmission trans = chrono_vehicle.ReadTransmissionJSON(transPath);
                    ChPowertrainAssembly powertrain = new ChPowertrainAssembly(engine, trans);
                    UChJSONVehicle.InitializePowertrain(powertrain);
                }
                else
                {
                    Debug.LogWarning($"{name}: engine/trans JSON not found at {engPath} or {transPath}");
                }
            }

            // create & initialise tires
            var axleList = UChJSONVehicle.GetAxles();
            uint numAxles = UChJSONVehicle.GetNumberAxles();

            for (int axleIndex = 0; axleIndex < numAxles; axleIndex++)
            {
                ChAxle axle = axleList[axleIndex];
                string tirePath = "";

                if (useSingleTireFile)
                {
                    tirePath = chrono_vehicle.GetVehicleDataFile(tireJSON);
                }
                else
                {
                    if (axleIndex < perAxleTireSpec.Count)
                    {
                        string userTireFile = perAxleTireSpec[axleIndex];
                        if (!string.IsNullOrEmpty(userTireFile))
                        {
                            tirePath = chrono_vehicle.GetVehicleDataFile(userTireFile);
                        }
                    }
                }

                if (string.IsNullOrEmpty(tirePath) || !File.Exists(tirePath))
                {
                    Debug.LogWarning($"{name}: Tire JSON for axle #{axleIndex} not found. Skipping.");
                    continue;
                }

                // for each wheel in this axle, we apply the same ChTire instance
                var wheels = axle.GetWheels();
                for (int w = 0; w < wheels.Count; w++)
                {
                    ChWheel wheel = wheels[w];
                    ChTire tire = chrono_vehicle.ReadTireJSON(tirePath);
                    tire.SetStepsize(tireStepSize);
                    UChJSONVehicle.InitializeTire(tire, wheel, VisualizationType.NONE, tireCollisionType);
                }
            }

            ////// set initial wheel angular velocity - not tested
            //if (initWheelAngVel != 0)
            //{
            //    for (int axleIndex = 0; axleIndex < numAxles; axleIndex++)
            //    {
            //        ChAxle axle = axleList[axleIndex];
            //        foreach (var wheel in axle.GetWheels())
            //        {
            //            // this doesn't work yet
            //            ChVector3d omega = new ChVector3d(0, 0, initWheelAngVel);
            //            wheel.GetSpindle().SetAngVelLocal(omega);
            //        }
            //    }
            //}

            // Debug outputs
            //Debug.Log($"{name}: built JSON-based wheeled vehicle from {topLevelVehicleJSON} \n" +
            //          $"Engine loaded from: {engineJSON}\n" +
            //          $"Transmission loaded from: {transJSON}\n" +
            //          $"Number of axles {UChJSONVehicle.GetNumberAxles()} \n");

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{name}: Exception building from top-level => {e.Message}");
            UChJSONVehicle = null;
            return false;
        }
    }

    protected override void OnAdvance(double step)
    {
        if (UChJSONVehicle != null)
        {
            UpdateFromJsonVehicle(step);
        }
    }

    // synchronises the Chrono vehicle with inputs & terrain, and importantly,
    // iterates thourhg the gameobjects to update the Unity transforms for visualisation
    // (and unity interaction if desired)
    private void UpdateFromJsonVehicle(double step)
    {
        // move the chassis
        var pos = UChJSONVehicle.GetPos();
        var rot = UChJSONVehicle.GetRot();
        transform.position = Utils.FromChronoFlip(pos);
        transform.rotation = Utils.FromChronoFlip(rot);

        // for each axle update the sub-list in axleData[axleIndex].visualGameobjects
        var axleList = UChJSONVehicle.GetAxles();
        int axleCount = (int)UChJSONVehicle.GetNumberAxles();

        for (int axleIndex = 0; axleIndex < axleCount; axleIndex++)
        {
            ChAxle chronoAxle = axleList[axleIndex];
            var chronoWheels = chronoAxle.GetWheels();

            // Make sure we have an entry in axleData
            if (axleIndex >= axleData.Count)
                break;

            var unityWheelsForThisAxle = axleData[axleIndex].visualGameobjects;
            if (unityWheelsForThisAxle == null) continue;

            // only update as many wheels as Chrono says exist - not dictacted by Unity
            int count = Mathf.Min(chronoWheels.Count, unityWheelsForThisAxle.Count);
            for (int w = 0; w < count; w++)
            {
                GameObject unityWheelGO = unityWheelsForThisAxle[w];
                if (!unityWheelGO) continue;

                // use Chrono's correct spindle transform
                ChWheel cwheel = chronoWheels[w];
                VehicleSide side = cwheel.GetSide();

                var wheelPos = UChJSONVehicle.GetSpindlePos(axleIndex, side);
                var wheelRot = UChJSONVehicle.GetSpindleRot(axleIndex, side);

                unityWheelGO.transform.position = Utils.FromChronoFlip(wheelPos);
                unityWheelGO.transform.rotation = Utils.FromChronoFlip(wheelRot);
            }
        }

        // get chrono to update
        UChJSONVehicle.Synchronize(UChSystem.chrono_system.GetChTime(), inputs, chTerrain);
        UChJSONVehicle.Advance(step);
    }


    public override ChVehicle GetChVehicle()
    {
        return UChJSONVehicle;
    }

    public override ChPowertrainAssembly GetPowertrainAssembly()
    {
        return UChJSONVehicle?.GetPowertrainAssembly();
    }

    public override ChEngine GetEngine()
    {
        return UChJSONVehicle?.GetEngine();
    }

    public override ChTransmission GetTransmission()
    {
        return UChJSONVehicle?.GetTransmission();
    }

    public override double GetMaxSpeed()
    {
        // this might need adjusting by JSON rather than arbitrary hard coded value
        return 33.3;
    }
}
