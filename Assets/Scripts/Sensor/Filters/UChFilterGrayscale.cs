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
public class UChFilterGrayscale : UChFilter
{

    void Start()
    {
        Filter = new ChFilterGrayscale();
    }

    void Awake()
    {
    }

    void OnValidate()
    {
    }


}

