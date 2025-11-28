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

// The same as UChVehicle (which is at -900)
[DefaultExecutionOrder(-900)]
public class UViper : MonoBehaviour
{
    private Viper viper;

    void Start()
    {
    }

    void Awake()
    {
    }

    public Viper GetViper()
    {
        return viper;
    }


}

