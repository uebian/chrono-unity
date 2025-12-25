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

    public static bool ParentHasSupportedBody(GameObject source)
    {
        return source.GetComponent<UChVehicle>() != null || source.GetComponent<UViperB>() != null || source.GetComponent<UChBody>() != null;
    }
    
    public static ChFramed GetSensorFrame(Transform sensorTransform, GameObject bodySource)
    {
        ChFramed frame = new ChFramed(Utils.ToChronoFlip(sensorTransform.localPosition), Utils.ToChronoFlip(sensorTransform.localRotation));

        if (bodySource.GetComponent<UViperB>() != null)
        {
            ChQuaterniond qAxis = new ChQuaterniond(chrono.QuatFromAngleX(chrono.CH_PI_2)); // Rotate Viper from Z-up to Y-up
            frame.SetPos(qAxis.Rotate(frame.GetPos()));
            frame.SetRot(qAxis.__rmul__(frame.GetRot()).__rmul__(qAxis.GetInverse()));
        }

        return frame;
    }

    public static ChBody GetSensorBody(GameObject bodySource)
    {
        ChBody body = null;

        if (bodySource.GetComponent<UChVehicle>() != null)
        {
            UChVehicle vehicle = bodySource.GetComponent<UChVehicle>();
            body = vehicle.GetChVehicle().GetChassisBody();
        }
        else if (bodySource.GetComponent<UViperB>() != null)
        {
            UViperB viper = bodySource.GetComponent<UViperB>();
            body = viper.GetViper().GetChassis().GetBody();
        }
        else if (bodySource.GetComponent<UChBody>() != null)
        {
            UChBody uchBody = bodySource.GetComponent<UChBody>();
            body = uchBody.GetChBody();
        }

        return body;
    }

}

