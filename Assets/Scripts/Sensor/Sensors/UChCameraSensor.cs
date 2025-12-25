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

// Add Sensor prior to the Sensor Manager (which is at -800) but after Filter (which is at -850)
[DefaultExecutionOrder(-830)]
public class UChCameraSensor : UChSensor
{
    public float updateRate = 30f; // [Hz], Update rate
    public uint w = 1920;
    public uint h = 1080;
    public float hFOV = 1.5707963267948966f; // Horizontal field of view
    public uint supersample_factor = 2;
    public CameraLensModelType lensModel = CameraLensModelType.PINHOLE;
    public bool use_gi = false;
    public float gamma = 2.2f;
    public bool use_fog = true;

    protected override void Start()
    {
        Transform parent = transform.parent;
        GameObject bodySource = parent != null ? parent.gameObject : null;
        Debug.Log("UChCameraSensor: Attempting to create Camera Sensor attached to " + (bodySource != null ? bodySource.name : "null"));

        if(!UChSensor.ParentHasSupportedBody(bodySource)){
            Debug.LogWarning($"UChCameraSensor: Unable to locate a valid parent body source for {name}. Sensor creation skipped.");
            return;
        }

        ChBody body = UChSensor.GetSensorBody(bodySource);
        ChFramed frame = UChSensor.GetSensorFrame(transform, bodySource);

        Sensor = new ChCameraSensor(body, updateRate, frame,
            w, h, hFOV, supersample_factor, lensModel, use_gi, gamma, use_fog);
        Debug.Log("UChCameraSensor: Created Camera Sensor attached to " + bodySource.name);
        base.Start();
    }

    void Awake()
    {
    }

    private void RefreshBodySourceFromParent()
    {
    }


}

