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
// These are some basic JSON templateds, pulled from existing JSON files in
// Chrono with a few sample fields are filled out as a starting point.
// If these don't suit the user, one could hit 'edit' on an existing json 
// of the type you want, modify and save changes to a new json.
//
// Dictionary for known subcomponent Type -> default JSON
// Key: Type (e.g. "Engine"), Value: Dictionary<Template, JObject>
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class UChVehGenJSONTemplates
{

    public static Dictionary<string, Dictionary<string, JObject>> defaultTemplates
        = new Dictionary<string, Dictionary<string, JObject>>
    {
        {
            "Vehicle",
            new Dictionary<string, JObject>
            {
                {
                    "TrackVehicle",
                    JObject.Parse(@"
                    {
                      ""Name"": ""New Track Vehicle"",
                      ""Type"": ""Vehicle"",
                      ""Template"": ""TrackVehicle"",

                      ""Chassis"": {
                        ""Input File"": ""path\\to\\json\\file""
                      }

                      // Additional fields as needed for a tracked vehicle (bnot yet supported)
                    }")
                },
                {
                    "WheeledVehicle",
                    JObject.Parse(@"
                    {
                      ""Name"": ""Wheeled Vehicle Template"",
                      ""Type"": ""Vehicle"",
                      ""Template"": ""WheeledVehicle"",

                      ""Chassis"": {
                        ""Input File"": """"
                      },

                      ""Axles"": [
                        {
                          ""Suspension Input File"": """",
                          ""Suspension Location"": [ 0, 0, 0 ],
                          ""Steering Index"": 0,
                          ""Left Wheel Input File"": """",
                          ""Right Wheel Input File"": """",
                          ""Left Brake Input File"": """",
                          ""Right Brake Input File"": """"
                        },
                        {
                          ""Suspension Input File"": """",
                          ""Suspension Location"": [ -4.0, 0, 0 ],
                          ""Left Wheel Input File"": """",
                          ""Right Wheel Input File"": """",
                          ""Left Brake Input File"": """",
                          ""Right Brake Input File"": """"
                        }
                      ],

                      ""Steering Subsystems"": [
                        {
                          ""Input File"": """",
                          ""Location"": [ 0, 0, 0 ],
                          ""Orientation"": [ 1, 0, 0, 0 ]
                        }
                      ],

                      ""Wheelbase"": 4,
                      ""Minimum Turning Radius"": 11.5,
                      ""Maximum Steering Angle (deg)"": 39.0,

                      ""Driveline"": {
                        ""Input File"": """",
                        ""Suspension Indexes"": [ 0, 1 ]
                      }
                    }")
                }
            }
        },

        {
            "Chassis",
            new Dictionary<string, JObject>
            {
                {
                    "RigidChassis",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""Generic Rigid Chassis"",
                      ""Type"":     ""Chassis"",
                      ""Template"": ""RigidChassis"",

                      ""Components"": [
                        {
                          ""Centroidal Frame"": {
                            ""Location"":    [0, 0, 0.5],
                            ""Orientation"": [1, 0, 0, 0]
                          },
                          ""Mass"":                1500,
                          ""Moments of Inertia"":  [200, 900, 1000],
                          ""Products of Inertia"": [0, 0, 0],
                          ""Void"":                false
                        }
                      ],

                      ""Driver Position"": {
                        ""Location"":     [1.0, 0.4, 1.2],
                        ""Orientation"":  [1, 0, 0, 0]
                      },

                      ""Rear Connector Location"": [-2.5, 0, -0.1],

                      ""Visualization"": {
                        ""Mesh"":  ""path\\to\\your_chassis_mesh.obj"" // though we don't use this in unity, using the json in normal chrono requires it for vis
                      }
                    }")
                }
            }
        },

        {
            "Engine",
            new Dictionary<string, JObject>
            {
                {
                    "EngineSimple",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New Simple Engine"",
                      ""Type"":     ""Engine"",
                      ""Template"": ""EngineSimple""

                      // can add here if needed in a template
                    }")
                },
                {
                    "EngineSimpleMap",
                    JObject.Parse(@"
                    {
                      ""Name"":                  ""New Mapped Engine"",
                      ""Type"":                  ""Engine"",
                      ""Template"":              ""EngineSimpleMap"",

                      ""Maximal Engine Speed RPM"": 6500,

                      ""Map Full Throttle"": [
                        [-100, 0],
                        [1000, 172.8],
                        [1500, 250.0],
                        [2000, 250.0],
                        [2500, 250.0],
                        [3000, 250.0],
                        [3500, 250.0],
                        [4000, 245.6],
                        [4500, 232.3],
                        [5000, 210.1],
                        [5500, 191.0],
                        [6000, 175.1],
                        [6500, 145.4]
                      ],

                      ""Map Zero Throttle"": [
                        [-954.93, 0],
                        [0, 0],
                        [800.002, 0],
                        [1000, -60],
                        [1500, -60],
                        [2000, -60],
                        [2500.01, -65],
                        [3000.01, -68],
                        [3500.01, -70],
                        [4000.01, -75],
                        [4500.01, -80],
                        [5000.01, -85],
                        [5500.01, -90],
                        [6000.01, -95],
                        [6500.02, -100],
                        [7000.02, -105]
                      ]
                    }")
                },
                {
                    "EngineShafts",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New Engine Shafts"",
                      ""Type"":     ""Engine"",
                      ""Template"": ""EngineShafts""

                      // could add torque curve data, inertia, gear ratios, etc.
                    }")
                }
            }
        },
        {
            "Transmission",
            new Dictionary<string, JObject>
            {
                {
                    "AutomaticTransmissionSimpleMap",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New Auto Trans (Map)"",
                      ""Type"":     ""Transmission"",
                      ""Template"": ""AutomaticTransmissionSimpleMap"",

                      ""Gear Box"": {
                        ""Reverse Gear Ratio"": -0.272,
                        ""Forward Gear Ratios"": [0.294, 0.364, 0.565, 1.075, 1.429, 1.516],
                        ""Shift Points Map RPM"": [
                          [1000, 3500],
                          [1200, 3500],
                          [1400, 3500],
                          [1500, 3500],
                          [1500, 3500],
                          [2000, 6500]
                        ]
                      }
                    }")
                },
                {
                    "AutomaticTransmissionSimpleCVT",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New Auto Trans (CVT)"",
                      ""Type"":     ""Transmission"",
                      ""Template"": ""AutomaticTransmissionSimpleCVT""
                    }")
                },
                {
                    "AutomaticTransmissionShafts",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New Auto Trans (Shafts)"",
                      ""Type"":     ""Transmission"",
                      ""Template"": ""AutomaticTransmissionShafts""
                    }")
                },
                {
                    "ManualTransmissionShafts",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New Manual Trans (Shafts)"",
                      ""Type"":     ""Transmission"",
                      ""Template"": ""ManualTransmissionShafts""
                    }")
                }
            }
        },
        {
            "Brake",
            new Dictionary<string, JObject>
            {
                {
                    "BrakeSimple",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New Simple Brake"",
                      ""Type"":     ""Brake"",
                      ""Template"": ""BrakeSimple"",
                      ""Maximum Torque"": 4000
                    }")
                },
                {
                    "BrakeShafts",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New Shaft Brake"",
                      ""Type"":     ""Brake"",
                      ""Template"": ""BrakeShafts"",
                      ""Shaft Inertia"": 0.4,
                      ""Maximum Torque"": 4000
                    }")
                }
            }
        },
        {
            "Driveline",
            new Dictionary<string, JObject>
            {
                {
                    "ShaftsDriveline2WD",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New 2WD Driveline"",
                      ""Type"":     ""Driveline"",
                      ""Template"": ""ShaftsDriveline2WD"",

                      ""Shaft Direction"": {
                        ""Motor Block"": [1, 0, 0],
                        ""Axle"":        [0, 1, 0]
                      },
                      ""Shaft Inertia"": {
                        ""Driveshaft"":       0.5,
                        ""Differential Box"": 0.6
                      },
                      ""Gear Ratio"": {
                        ""Conical Gear"": 0.2
                      },
                      ""Axle Differential Locking Limit"": 100
                    }")
                },
                {
                    "ShaftsDriveline4WD",
                    JObject.Parse(@"
                    {
                      ""Name"":                       ""New 4WD Driveline"",
                      ""Type"":                       ""Driveline"",
                      ""Template"":                   ""ShaftsDriveline4WD"",

                      ""Shaft Direction"": {
                        ""Motor Block"":              [1, 0, 0],
                        ""Axle"":                     [0, 1, 0]
                      },
                      ""Shaft Inertia"": {
                        ""Driveshaft"":               0.5,
                        ""Front Driveshaft"":         0.5,
                        ""Rear Driveshaft"":          0.5,
                        ""Central Differential Box"":  0.6,
                        ""Front Differential Box"":    0.6,
                        ""Rear Differential Box"":     0.6
                      },
                      ""Gear Ratio"": {
                        ""Front Conical Gear"": 0.2162,
                        ""Rear Conical Gear"":  0.2162
                      },
                      ""Axle Differential Locking Limit"":    100,
                      ""Central Differential Locking Limit"": 100
                    }")
                },
                {
                    "SimpleDriveline",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New Simple Driveline"",
                      ""Type"":     ""Driveline"",
                      ""Template"": ""SimpleDriveline""
                    }")
                },
                {
                    "SimpleDrivelineXWD",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New XWD Driveline"",
                      ""Type"":     ""Driveline"",
                      ""Template"": ""SimpleDrivelineXWD""
                    }")
                },
                {
                    "HMMWV_ShaftsDriveline4WD",
                    JObject.Parse(@"
                    {
                      ""Name"":                       ""HMMWV AWD Driveline"",
                      ""Type"":                       ""Driveline"",
                      ""Template"":                   ""ShaftsDriveline4WD"",

                      ""Shaft Direction"": {
                        ""Motor Block"":              [1, 0, 0],
                        ""Axle"":                     [0, 1, 0]
                      },
                      ""Shaft Inertia"": {
                        ""Driveshaft"":               0.5,
                        ""Front Driveshaft"":         0.5,
                        ""Rear Driveshaft"":          0.5,
                        ""Central Differential Box"": 0.6,
                        ""Front Differential Box"":   0.6,
                        ""Rear Differential Box"":    0.6
                      },
                      ""Gear Ratio"": {
                        ""Front Conical Gear"":       0.2,
                        ""Rear Conical Gear"":        0.2
                      },

                      ""Axle Differential Locking Limit"":    100,
                      ""Central Differential Locking Limit"": 100
                    }")
                }
            }
        },
        {
            "Steering",
            new Dictionary<string, JObject>
            {
                {
                    "PitmanArm",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New PitmanArm Steering"",
                      ""Type"":     ""Steering"",
                      ""Template"": ""PitmanArm""

                    }")
                },
                {
                    "RackPinion",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New RackPinion Steering"",
                      ""Type"":     ""Steering"",
                      ""Template"": ""RackPinion""
                    }")
                },
                {
                    "RotaryArm",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New RotaryArm Steering"",
                      ""Type"":     ""Steering"",
                      ""Template"": ""RotaryArm""
                    }")
                }
            }
        },
        {
            "Suspension",
            new Dictionary<string, JObject>
            {
                {
                    "DoubleWishbone",
                    JObject.Parse(@"
                    {
                      ""Name"":                       ""New DoubleWishbone"",
                      ""Type"":                       ""Suspension"",
                      ""Template"":                   ""DoubleWishbone"",

                      ""Camber Angle (deg)"":         0,
                      ""Toe Angle (deg)"":            0,

                      ""Spindle"" : {
                        ""Mass"":         1.103,
                        ""COM"":          [0, 0.7979, -0.1199],
                        ""Inertia"":      [0.000478, 0.000496, 0.000478],
                        ""Radius"":       0.1,
                        ""Width"":        0.02
                      },

                      ""Upright"": {
                        ""Mass"":               1.397,
                        ""COM"":                [-0.0224, 0.7470, -0.1118],
                        ""Moments of Inertia"": [0.0138, 0.0146, 0.00283],
                        ""Products of Inertia"": [0, 0, 0],
                        ""Radius"":             0.025
                      },

                      ""Upper Control Arm"": {
                        ""Mass"":               1.032,
                        ""COM"":                [-0.1083, 0.5972, 0.1063],
                        ""Moments of Inertia"": [0.00591, 0.00190, 0.00769],
                        ""Products of Inertia"": [0, 0, 0],
                        ""Radius"":             0.02,
                        ""Location Chassis Front"": [-0.100, 0.4700, 0.1050],
                        ""Location Chassis Back"":  [-0.250, 0.5100, 0.1100],
                        ""Location Upright"":       [-0.040, 0.6950, 0.1050]
                      },

                      ""Lower Control Arm"": {
                        ""Mass"":               1.611,
                        ""COM"":                [0.0048, 0.6112, -0.2932],
                        ""Moments of Inertia"": [0.0151, 0.0207, 0.0355],
                        ""Products of Inertia"": [0, 0, 0],
                        ""Radius"":             0.03,
                        ""Location Chassis Front"": [0.2, 0.4200, -0.2700],
                        ""Location Chassis Back"":  [-0.2, 0.4700, -0.2650],
                        ""Location Upright"":       [0.0, 0.7700, -0.3200]
                      },

                      ""Tierod"": {
                        ""Location Chassis"": [-0.2, 0.4200, -0.1200],
                        ""Location Upright"": [-0.15, 0.7700, -0.1200]
                      },

                      ""Spring"": {
                        ""Location Chassis"":   [-0.04, 0.5200, 0.2300],
                        ""Location Arm"":       [0.0, 0.6200, -0.2700],
                        ""Free Length"":        0.51,
                        ""Spring Coefficient"": 500000.0
                      },

                      ""Shock"": {
                        ""Location Chassis"":       [-0.04, 0.5200, 0.2300],
                        ""Location Arm"":           [0.0, 0.6200, -0.2700],
                        ""Damping Coefficient"":    10000.0
                      },

                      ""Axle"": {
                        ""Inertia"": 0.4
                      }
                    }")
                },
                {
                    "DoubleWishboneReduced",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New DoubleWishboneReduced"",
                      ""Type"":     ""Suspension"",
                      ""Template"": ""DoubleWishboneReduced""
                    }")
                },
                {
                    "RigidPanhardAxle",
                    JObject.Parse(@"
                    {
                      ""Name"":                           ""New Rigid Panhard Axle"",
                      ""Type"":                           ""Suspension"",
                      ""Template"":                       ""RigidPanhardAxle"",

                      ""Camber Angle (deg)"":             0,
                      ""Toe Angle (deg)"":                0,

                      ""Spindle"" : {
                        ""Mass"":       14.705,
                        ""COM"":        [0.0, 0.7325, 0.0],
                        ""Inertia"":    [0.04117, 0.07352, 0.04117],
                        ""Radius"":     0.10,
                        ""Width"":      0.06
                      },

                      ""Axle Tube"" : {
                        ""Mass"":       124.0,
                        ""COM"":        [0, 0, 0],
                        ""Inertia"":    [22.21, 0.0775, 22.21],
                        ""Radius"":     0.0476
                      },

                      ""Panhard Rod"" : {
                        ""Mass"":         10.0,
                        ""Inertia"":      [1.0, 0.04, 1.0],
                        ""Radius"":       0.03,
                        ""Location Chassis"": [-0.1, 0.5142, 0.0],
                        ""Location Axle"":    [-0.1, -0.5142, 0.0]
                      },

                      ""Anti Roll Bar"" : {
                        ""Mass"":               5.0,
                        ""Inertia"":            [0.5, 0.02, 0.5],
                        ""Radius"":             0.025,
                        ""Location Chassis"":   [0.4, 0.4, -0.05],
                        ""Location Axle"":      [0.0, 0.4, -0.05],
                        ""Rotational Stiffness"": 1000.0,
                        ""Rotational Damping"":   10.0
                      },

                      ""Spring"": {
                        ""Location Chassis"":       [0.0, 0.5142, 0.3476],
                        ""Location Axle"":          [0.0, 0.5142, 0.0476],
                        ""Spring Coefficient"":     102643.885771329,
                        ""Free Length"":            0.3621225507207084,
                        ""Minimum Length"":         0.22,
                        ""Maximum Length"":         0.38
                      },

                      ""Shock"": {
                        ""Location Chassis"":           [0.20, 0.5142, 0.347],
                        ""Location Axle"":              [0.125, 0.5842, -0.0507],
                        ""Damping Coefficient"":        16336.2817986669,
                        ""Degressivity Compression"":   3,
                        ""Degressivity Expansion"":     1
                      },

                      ""Axle"": {
                        ""Inertia"": 0.4
                      }
                    }")
                },
                {
                    "DeDionAxle",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New DeDionAxle"",
                      ""Type"":     ""Suspension"",
                      ""Template"": ""DeDionAxle""
                    }")
                },
                {
                    "MultiLink",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New MultiLink"",
                      ""Type"":     ""Suspension"",
                      ""Template"": ""MultiLink""
                    }")
                },
                {
                    "MacPhersonStrut",
                    JObject.Parse(@"
                    {
                      ""Name"":     ""New MacPhersonStrut"",
                      ""Type"":     ""Suspension"",
                      ""Template"": ""MacPhersonStrut""
                    }")
                }
            }
        },
        {
            "Wheel",
            new Dictionary<string, JObject>
            {
                {
                    "Wheel",
                    JObject.Parse(@"
                    {
                      ""Name"":   ""New Wheel"",
                      ""Type"":   ""Wheel"",
                      ""Template"": ""Wheel"",

                      ""Mass"": 12.0,
                      ""Inertia"": [0.24, 0.42, 0.24],

                      ""Visualization"": {
                        ""Mesh Filename"": ""path\\to\\rim.obj"",
                        ""Radius"": 0.2032,
                        ""Width"": 0.1524
                      }
                    }")
                }
            }
        }
        // any other templates could be added here as needed...
    };
}
