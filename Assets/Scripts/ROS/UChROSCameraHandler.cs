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

// Add Handler after the SensorManager (which is at -800)
[DefaultExecutionOrder(-750)]
public class UChROSCameraHandler : UChROSHandler
{
    public double ROS_publish_rate = 15;
    public string ROS_topic = "/camera/image_raw";
    protected override void Start()
    {
        ChCameraSensor chrono_camera_sensor = (ChCameraSensor) GetComponent<UChCameraSensor>().Sensor;
        Handler = new ChROSCameraHandler(ROS_publish_rate, chrono_camera_sensor, ROS_topic);
        base.Start();
    }

    void Awake()
    {
    }

    private void RefreshBodySourceFromParent()
    {
    }

}
