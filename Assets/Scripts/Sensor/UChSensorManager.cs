// =============================================================================
// PROJECT CHRONO - http://projectchrono.org
//
// Copyright (c) 2025 projectchrono.org
// All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found
// in the LICENSE file at the top level of the distribution.
//
// =============================================================================
// Authors: Bocheng Zou
// =============================================================================

using UnityEngine;
using System.Collections.Generic;

// Add Sensor after to the UChVehicle (which is at -900)
[DefaultExecutionOrder(-800)]
public class UChSensorManager : MonoBehaviour, IAdvance
{

    public static string sensorShaderDir = "/ChronoData/sensor/"; //used for the sensor data

    public UChSystem system;
    public List<UChSensor> sensors = new List<UChSensor>();

    private ChSensorManager sensor_manager;

    void Start()
    {
        system.Register(gameObject.name + "_sensor", this);
        sensor_manager = new ChSensorManager(UChSystem.chrono_system);
        for (int i = 0; i < sensors.Count; i++)
        {
            sensor_manager.AddSensor(sensors[i].Sensor);
        }
    }

    void Awake()
    {
        chrono_sensor.SetSensorShaderDir(Application.streamingAssetsPath + sensorShaderDir);
    }

    public void Advance(double step)
    {
        sensor_manager.Update();
    }

}

