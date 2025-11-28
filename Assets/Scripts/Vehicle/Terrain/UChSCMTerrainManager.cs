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

using UnityEngine;

// Add Terrain prior to the UChVehicle (which is at -900)
[DefaultExecutionOrder(-950)]
public class UChSCMTerrainManager : UChTerrainManager
{

    void Start()
    {
        chronoTerrain = GetComponentInChildren<UChSCMTerrain>().chronoTerrain;
    }

}

