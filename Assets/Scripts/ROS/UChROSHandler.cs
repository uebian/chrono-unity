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

// Add Handler after the SensorManager (which is at -800)
[DefaultExecutionOrder(-750)]
public class UChROSHandler : MonoBehaviour
{
    public ChROSHandler Handler { get; set; }

    protected virtual void Start()
    {
    }

    void Awake()
    {
    }

    void OnValidate()
    {
    }

}

