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

// Add Filters prior to the Sensor (which is at -800)
[DefaultExecutionOrder(-850)]
public class UChFilterVisualize : UChFilter
{
    public int w = 1920;
    public int h = 1080;
    public bool fullscreen = false;

    void Start()
    {
        Filter = new ChFilterVisualize(w, h, FilterName, fullscreen);
    }

    void Awake()
    {
    }

    void OnValidate()
    {
    }


}

