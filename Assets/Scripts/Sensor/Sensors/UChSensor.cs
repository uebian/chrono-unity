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

// Add Sensor after the UChVehicle (which is at -900)
[DefaultExecutionOrder(-800)]
public class UChSensor : MonoBehaviour
{
    public List<UChFilter> Filters = new List<UChFilter>();
    public ChSensor Sensor { get; set; }

    protected virtual void Start()
    {
        for (int i = 0; i < Filters.Count; i++)
        {
            UChFilter filter = Filters[i];
            Sensor.PushFilter(filter.Filter);
        }
    }

    void Awake()
    {
    }

    void OnValidate()
    {
    }


}

