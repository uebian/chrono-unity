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
// Utility helpers for parsing json data
// =============================================================================

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VehicleBuilder.Editor
{

    public static class VehicleJsonDataParser
    {
        public static VehicleDataModel.VehicleData ParseVehicleData(JObject vehicleRoot)
        {
            if (vehicleRoot == null)
            {
                return null;
            }

            var vehicle = new VehicleDataModel.VehicleData
            {
                Name = vehicleRoot["Name"]?.ToString(),
                Type = vehicleRoot["Type"]?.ToString(),
                Template = vehicleRoot["Template"]?.ToString(),
                Wheelbase = vehicleRoot["Wheelbase"]?.ToObject<float>() ?? 0f,
                MinimumTurningRadius = vehicleRoot["Minimum Turning Radius"]?.ToObject<float>() ?? 0f,
                MaximumSteeringAngleDeg = vehicleRoot["Maximum Steering Angle (deg)"]?.ToObject<float>() ?? 0f,
                Chassis = ParseComponentReference(vehicleRoot["Chassis"]),
                Engine = ParseComponentReference(vehicleRoot["Engine"]),
                Transmission = ParseComponentReference(vehicleRoot["Transmission"]),
                Driveline = ParseComponentReference(vehicleRoot["Driveline"])
            };

            if (vehicleRoot["Axles"] is JArray axlesArray)
            {
                vehicle.Axles = new List<VehicleDataModel.AxleEntry>();
                foreach (var token in axlesArray)
                {
                    if (token is JObject axleObj)
                    {
                        vehicle.Axles.Add(new VehicleDataModel.AxleEntry
                        {
                            Suspension = axleObj["Suspension Input File"]?.ToString(),
                            SuspensionLocation = ParseVector3(axleObj["Suspension Location"]),
                            SteeringIndex = axleObj["Steering Index"]?.ToObject<int>() ?? 0,
                            LeftWheel = axleObj["Left Wheel Input File"]?.ToString(),
                            RightWheel = axleObj["Right Wheel Input File"]?.ToString(),
                            LeftBrake = axleObj["Left Brake Input File"]?.ToString(),
                            RightBrake = axleObj["Right Brake Input File"]?.ToString()
                        });
                    }
                }
            }

            if (vehicleRoot["Steering Subsystems"] is JArray steeringArray)
            {
                vehicle.SteeringSubsystems = new List<VehicleDataModel.SteeringEntry>();
                foreach (var token in steeringArray)
                {
                    if (token is JObject steeringObj)
                    {
                        vehicle.SteeringSubsystems.Add(new VehicleDataModel.SteeringEntry
                        {
                            Steering = steeringObj["Input File"]?.ToString(),
                            Location = ParseVector3(steeringObj["Location"]),
                            Orientation = ParseQuaternion(steeringObj["Orientation"])
                        });
                    }
                }
            }

            return vehicle;
        }

        private static VehicleDataModel.ComponentReference ParseComponentReference(JToken token)
        {
            if (token is JObject obj)
            {
                return new VehicleDataModel.ComponentReference
                {
                    InputFile = obj["Input File"]?.ToString()
                };
            }

            return null;
        }

        private static Vector3 ParseVector3(JToken token)
        {
            if (token is JArray arr && arr.Count >= 3)
            {
                return new Vector3(
                    arr[0]?.ToObject<float>() ?? 0f,
                    arr[1]?.ToObject<float>() ?? 0f,
                    arr[2]?.ToObject<float>() ?? 0f
                );
            }

            return Vector3.zero;
        }

        private static Quaternion ParseQuaternion(JToken token)
        {
            if (token is JArray arr && arr.Count >= 4)
            {
                return new Quaternion(
                    arr[1]?.ToObject<float>() ?? 0f,
                    arr[2]?.ToObject<float>() ?? 0f,
                    arr[3]?.ToObject<float>() ?? 0f,
                    arr[0]?.ToObject<float>() ?? 1f
                );
            }

            return Quaternion.identity;
        }
    }
}
