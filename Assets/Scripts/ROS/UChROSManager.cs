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

// Add Sensor after to the UChSensorManager (which is at -800)
[DefaultExecutionOrder(-700)]
public class UChROSManager : MonoBehaviour, IAdvance
{

    public List<UChROSHandler> handlers = new List<UChROSHandler>();

    private ChROSManager ros_manager;
    private double sim_time = 0;
    void Start()
    {
        UChSystem system = UnityEngine.Object.FindFirstObjectByType<UChSystem>();
        system.Register(gameObject.name + "_ros", this);
        ros_manager = new ChROSManager();
        for (int i = 0; i < handlers.Count; i++)
        {
            ros_manager.RegisterHandler(handlers[i].Handler);
        }
        ros_manager.Initialize();
    }

    void Awake()
    {
    }

    public void Advance(double step)
    {
        sim_time += step;
        ros_manager.Update(sim_time, step);
    }

}

