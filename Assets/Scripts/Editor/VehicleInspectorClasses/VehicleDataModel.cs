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
// Handles vehicle data models for the VehicleBuilder inspector system
// =============================================================================

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VehicleBuilder.Editor
{

    public static class VehicleDataModel
    {
        /// <summary>
        /// Complete vehicle configuration data parsed from JSON
        /// </summary>
        public class VehicleData
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Template { get; set; }

            // Core components
            public ComponentReference Chassis { get; set; }
            public ComponentReference Engine { get; set; }
            public ComponentReference Transmission { get; set; }
            public ComponentReference Driveline { get; set; }

            // Axles and steering
            public List<AxleEntry> Axles { get; set; } = new List<AxleEntry>();
            public List<SteeringEntry> SteeringSubsystems { get; set; } = new List<SteeringEntry>();

            // Vehicle characteristics
            public float Wheelbase { get; set; }
            public float MinimumTurningRadius { get; set; }
            public float MaximumSteeringAngleDeg { get; set; }

            // Collision geometry
            public VehicleGeometryData Geometry { get; set; }

            // Extra fields not explicitly handled
            public Dictionary<string, JToken> ExtraFields { get; set; } = new Dictionary<string, JToken>();
        }

        /// <summary>
        /// Reference to a single JSON subcomponent file (e.g., Engine, Chassis)
        /// </summary>
        public class ComponentReference
        {
            [JsonProperty("Input File")]
            public string InputFile { get; set; }
        }

        /// <summary>
        /// Axle configuration containing suspension, wheels, brakes, and steering
        /// </summary>
        public class AxleEntry
        {
            [JsonProperty("Suspension Input File")]
            public string Suspension { get; set; }

            [JsonProperty("Suspension Location")]
            public Vector3 SuspensionLocation { get; set; }

            [JsonProperty("Steering Index")]
            public int SteeringIndex { get; set; }

            [JsonProperty("Left Wheel Input File")]
            public string LeftWheel { get; set; }

            [JsonProperty("Right Wheel Input File")]
            public string RightWheel { get; set; }

            [JsonProperty("Left Brake Input File")]
            public string LeftBrake { get; set; }

            [JsonProperty("Right Brake Input File")]
            public string RightBrake { get; set; }
        }

        /// <summary>
        /// Steering subsystem entry in the top-level vehicle file
        /// </summary>
        public class SteeringEntry
        {
            [JsonProperty("Input File")]
            public string Steering { get; set; }

            [JsonProperty("Location")]
            public Vector3 Location { get; set; }

            [JsonProperty("Orientation")]
            public Quaternion Orientation { get; set; }
        }

        /// <summary>
        /// Vehicle collision geometry data (materials and shapes)
        /// </summary>
        public class VehicleGeometryData
        {
            public List<VehicleContactMaterial> Materials { get; set; } = new List<VehicleContactMaterial>();
            public List<VehicleContactShape> Shapes { get; set; } = new List<VehicleContactShape>();
        }

        /// <summary>
        /// Contact material properties for vehicle collision
        /// </summary>
        public class VehicleContactMaterial
        {
            public float Mu { get; set; }  // Coefficient of friction
            public float Cr { get; set; }  // Coefficient of restitution

            // Material properties (if "Properties" is present)
            public float YoungModulus { get; set; }
            public float PoissonRatio { get; set; }

            // Coefficients (if "Coefficients" is present)
            public float NormalStiffness { get; set; }
            public float NormalDamping { get; set; }
            public float TangentialStiffness { get; set; }
            public float TangentialDamping { get; set; }
        }

        /// <summary>
        /// Contact shape for vehicle collision geometry
        /// </summary>
        public class VehicleContactShape
        {
            public string Type { get; set; }
            public int MaterialIndex { get; set; }

            public Vector3 Location { get; set; }
            public Quaternion Orientation { get; set; } = Quaternion.identity;

            // Shape-specific parameters
            public float Radius { get; set; }
            public float Length { get; set; }
            public Vector3 Dimensions { get; set; }
            public Vector3 Axis { get; set; }

            // For mesh/hull
            public string Filename { get; set; }
            public float ContactRadius { get; set; }
        }
    }
}
