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

    public BackgroundMode background_mode;
    public float color_zenith_R = 0.4f;
    public float color_zenith_G = 0.5f;
    public float color_zenith_B = 0.6f;
    public float color_horizon_R = 0.7f;
    public float color_horizon_G = 0.8f;
    public float color_horizon_B = 0.9f;
    public string env_tex;
    public float AmbientLight_R = 0.01f;
    public float AmbientLight_G = 0.01f;
    public float AmbientLight_B = 0.01f;
    public List<SensorPointLight> point_lights = new List<SensorPointLight>();
    void Start()
    {
        system.Register(gameObject.name + "_sensor", this);
        sensor_manager = new ChSensorManager(UChSystem.chrono_system);
        for (int i = 0; i < sensors.Count; i++)
        {
            sensor_manager.AddSensor(sensors[i].Sensor);
        }
        Background bg = new Background();
        bg.mode = background_mode;
        bg.color_zenith = new ChVector3f(color_zenith_R, color_zenith_G, color_zenith_B);
        bg.color_horizon = new ChVector3f(color_horizon_R, color_horizon_G, color_horizon_B);
        bg.env_tex = env_tex;
        sensor_manager.scene.SetBackground(bg);
        sensor_manager.scene.SetAmbientLight(new ChVector3f(AmbientLight_R, AmbientLight_G, AmbientLight_B));
        foreach (var light in point_lights)
        {
            ChVector3d pos = Utils.ToChronoFlip(light.transform.position);
            sensor_manager.scene.AddPointLight(new ChVector3f((float)pos.x, (float)pos.y, (float)pos.z),
                new ChColor(light.R, light.G, light.B), 
                light.max_range);
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

