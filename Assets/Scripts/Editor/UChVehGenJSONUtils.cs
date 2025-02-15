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
// Provides JSON file I/O plus data classes for Chrono vehicle generator
// =============================================================================


using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using System.Linq;

public static class UChVehGenJSONUtils
{
    // cache for storing each file's Type and Name so we don't parse multiple times
    public static Dictionary<string, (string Subfolder, string Type, string Name)> s_fileMetaCache
        = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);

    // Holds all the vehicle fields, including references to subcomponents,
    // numeric fields, and arrays of axles, steering subsystems, etc.  
    public class VehicleData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Template { get; set; }

        public ComponentReference Chassis { get; set; }
        public ComponentReference Engine { get; set; }
        public ComponentReference Transmission { get; set; }
        public ComponentReference Driveline { get; set; }

        public List<AxleEntry> Axles { get; set; } = new List<AxleEntry>();
        public List<SteeringEntry> SteeringSubsystems { get; set; } = new List<SteeringEntry>();

        public float Wheelbase { get; set; }
        public float MinimumTurningRadius { get; set; }
        public float MaximumSteeringAngleDeg { get; set; }

        public VehicleGeometryData Geometry { get; set; }

        public Dictionary<string, JToken> ExtraFields { get; set; } = new Dictionary<string, JToken>();
    }

    // defines geometry (contact materials, shapes, etc.) for the vehicle
    public class VehicleGeometryData
    {
        public List<VehicleContactMaterial> Materials { get; set; } = new List<VehicleContactMaterial>();
        public List<VehicleContactShape> Shapes { get; set; } = new List<VehicleContactShape>();

        // In future could add visualisation here too
    }

    public class VehicleContactMaterial
    {
        public float Mu { get; set; }  // Coefficient of friction
        public float Cr { get; set; }  // Coefficient of restitution

        // these get used if "Properties" is present
        public float YoungModulus { get; set; }
        public float PoissonRatio { get; set; }

        // these get used if "Coefficients" is present
        public float NormalStiffness { get; set; }
        public float NormalDamping { get; set; }
        public float TangentialStiffness { get; set; }
        public float TangentialDamping { get; set; }
    }

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

    // Cached subfolders
    // e.g., "Nissan_Patrol", "M113", "HMMWV", etc. -> "Chassis" -> List<string> paths
    public static Dictionary<string, Dictionary<string, List<string>>> s_subcomponentCache
        = new Dictionary<string, Dictionary<string, List<string>>>(
            StringComparer.OrdinalIgnoreCase // make it case insensitive
        );


    // reference to a single JSON subcomponent. E.g., "Engine Input File"
    public class ComponentReference
    {
        [JsonProperty("Input File")]
        public string InputFile { get; set; }
    }

    // Contains references to suspension, brakes, wheels, etc.
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

    // steering subsystem entry in the top-level vehicle file
    public class SteeringEntry
    {
        [JsonProperty("Input File")]
        public string Steering { get; set; }

        [JsonProperty("Location")]
        public Vector3 Location { get; set; }

        [JsonProperty("Orientation")]
        public Quaternion Orientation { get; set; }
    }

    // Enum for recognized subcomponent types... will ultimately just
    // convert them to strings like "Suspension", "Brake", etc.
    private enum SubcomponentType
    {
        Unknown,
        Chassis,
        Engine,
        Transmission,
        Driveline,
        Suspension,
        Brake,
        Wheel,
        Steering
    }

    public static string GetForcedType(string parentKey, string childKey)
    {
        // trim any surrounding whitespace and convert to lowercase
        string p = parentKey?.Trim().ToLowerInvariant();
        string c = childKey?.Trim().ToLowerInvariant();

        // Use tuple pattern matching to determine the forced type based on the normalized keys.
        return (p, c) switch
        {
            // Case 1: Driveline object with "Input File" subobject => forced type "Driveline"
            ("driveline", "input file") => "Driveline",

            // Case 2: Steering Subsystems array with "Input File" subobject => forced type "Steering"
            ("steering subsystems", "input file") => "Steering",

            // Case 3: Chassis with "Input File" subobject => forced type "Chassis"
            ("chassis", "input file") => "Chassis",

            // Case 4: Engine with "Input File" subobject => forced type "Engine"
            ("engine", "input file") => "Engine",

            // Case 5: Transmission with "Input File" subobject => forced type "Transmission"
            ("transmission", "input file") => "Transmission",

            // Case 6: Any parent with "Suspension Input File" => forced type "Suspension"
            (_, "suspension input file") => "Suspension",

            // Case 7: Any parent with either "Left Wheel Input File" or "Right Wheel Input File" => forced type "Wheel"
            (_, "left wheel input file") or (_, "right wheel input file") => "Wheel",

            // Case 8: Any parent with either "Left Brake Input File" or "Right Brake Input File" => forced type "Brake"
            (_, "left brake input file") or (_, "right brake input file") => "Brake",

            // Default case: If no patterns match, return an empty string to indicate a scalar value.
            _ => ""
        };
    }


    // ------------------------------------------------------------------------
    // CORE FILE I/O
    // ------------------------------------------------------------------------

    // load a generic JSON file and return a JObject
    public static JObject LoadJson(string chronoVehicleDataRoot, string relativeSubPath)
    {
        if (string.IsNullOrEmpty(relativeSubPath))
        {
            Debug.LogError("LoadJson: relativeSubPath is null or empty.");
            return null;
        }

        // full path to the JSON file
        string fullPath = GetFullPath(chronoVehicleDataRoot, relativeSubPath);
        if (!File.Exists(fullPath)) // checks if file exists
        {
            Debug.LogError($"LoadJson: File not found: {fullPath}");
            return null;
        }

        // try parsing
        try
        {
            string jsonStr = File.ReadAllText(fullPath);   
            JObject root = JObject.Parse(jsonStr);         // parse into JObject
            return root;                                   
        }
        catch (Exception ex)                              
        {
            Debug.LogError($"LoadJson: Exception parsing {relativeSubPath}: {ex.Message}");
            return null;
        }
    }

    // save a JObject to disk (relative to chronoVehicleDataRoot)
    public static bool SaveJson(JObject obj, string chronoVehicleDataRoot, string relativeSubPath)
    {
        if (obj == null)
        {
            Debug.LogError("SaveJson: JObject is null.");
            return false;
        }
        if (string.IsNullOrEmpty(relativeSubPath))
        {
            Debug.LogError("SaveJson: relativeSubPath is null or empty.");
            return false;
        }

        try
        {
            // Convert to absolute path
            string fullPath = GetFullPath(chronoVehicleDataRoot, relativeSubPath);

            // Ensure directory
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // use try-catch to handle JSON-specific issues
            try
            {
                // Use the MNewtonsoft serialiser with custom converters
                JsonSerializer serializer = CreateChronoSerializer();
                string content = JsonConvert.SerializeObject(obj, Formatting.Indented, serializer.Converters.Cast<JsonConverter>().ToArray());
                File.WriteAllText(fullPath, content);
                Debug.Log($"SaveJson: Wrote JSON to {fullPath}");
            }
            catch (JsonException jsonEx)  // if there's a JSON-specific issue
            {
                Debug.LogError($"SaveJson: JSON serialization error for {relativeSubPath}: {jsonEx.Message}");
                return false;
            }

            AssetDatabase.Refresh();
            return true;
        }
        catch (Exception ex)
        {
            // Catch other exceptions (e.g., I/O, path, etc.)
            Debug.LogError($"SaveJson: Exception saving {relativeSubPath}: {ex.Message}");
            return false;
        }
    }

    // helper for getting full path
    public static string GetFullPath(string chronoVehicleDataRoot, string relativeSubPath)
    {
        // Convert backslashes and combine with the root path
        relativeSubPath = relativeSubPath.Replace("\\", "/");
        string combined = Path.Combine(chronoVehicleDataRoot, relativeSubPath);
        return Path.GetFullPath(combined);
    }

    // Caching the findings of the scanning
    public static void BuildFileMetaCache(string chronoVehicleDataRoot)
    {
        // Clear previous cache
        s_fileMetaCache.Clear();

        var allJsonPaths = Directory.GetFiles(chronoVehicleDataRoot, "*.json", SearchOption.AllDirectories);
        foreach (var absPath in allJsonPaths)
        {
            // Convert absolute path to relative path
            string relPath = ToRelativePath(chronoVehicleDataRoot, absPath);
            if (string.IsNullOrEmpty(relPath))
                continue;

            // if relPath = "HMMWV/engine/HMMWV_Engine.json", then subFolder = "HMMWV"
            string subFolder = "";
            string[] parts = relPath.Split('/', '\\');  // split path by slash
            if (parts.Length > 0)
                subFolder = parts[0];

            try
            {
                string text = File.ReadAllText(absPath);
                JObject root = JObject.Parse(text);

                // Extract type from JSON
                string typeVal = root["Type"]?.ToString() ?? "Unknown";
                // get name from JSON
                string nameVal = root["Name"]?.ToString() ?? Path.GetFileNameWithoutExtension(relPath);

                // store in the dictionary
                s_fileMetaCache[relPath] = (subFolder, typeVal, nameVal);
            }
            catch
            {
                // If parse fails, mark type as "Invalid"
                // subFolder is still known, name is a fallback
                s_fileMetaCache[relPath] = (subFolder, "Invalid", Path.GetFileNameWithoutExtension(relPath));
            }
        }
    }

    // retrieve from the cache based on type
    public static Dictionary<string, List<string>> GetSubcomponentFilesFromCache()
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in s_fileMetaCache)
        {
            string relPath = kvp.Key;
            var meta = kvp.Value;
            string typeVal = meta.Type;

            // add if the type doesn't exist in the dict
            if (!result.ContainsKey(typeVal))
                result[typeVal] = new List<string>();
            result[typeVal].Add(relPath);
        }

        foreach (var kvp in result)
        {
            kvp.Value.Sort(); // sorts paths in ascending order
        }
        return result;
    }

    public static string GetDisplayNameFor(string relPath)
    {
        // s_fileMetaCache maps relPath -> (subFolder, type, name)
        if (s_fileMetaCache.TryGetValue(relPath, out var meta))
        {
            return meta.Name; // if found in cache, return name
        }
        // fallback to file name if not found
        return Path.GetFileNameWithoutExtension(relPath);
    }

    // pull the correct cache from the type
    public static Dictionary<string, Dictionary<string, List<string>>> GetCacheBySubfolderAndType()
    {
        // key by subfolder then type
        var result = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in s_fileMetaCache)
        {
            string relPath = kvp.Key;
            var (subFolder, typeVal, nameVal) = kvp.Value;

            if (!result.ContainsKey(subFolder))
                result[subFolder] = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            if (!result[subFolder].ContainsKey(typeVal))
                result[subFolder][typeVal] = new List<string>();

            // add the file path to that type list
            result[subFolder][typeVal].Add(relPath);
        }
        return result;
    }

    // scan the chrono vehicle data root for all jsons and parse each type property
    // then group by type -> list<subpaths>
    // (store invalid or parse-failed files under "Invalid")
    public static Dictionary<string, List<string>> GetSubcomponentFiles(string chronoVehicleDataRoot)
    {
        Dictionary<string, List<string>> result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(chronoVehicleDataRoot))
        {
            Debug.LogWarning($"GetSubcomponentFiles: Directory does not exist: {chronoVehicleDataRoot}");
            return result;
        }
        var allJsonPaths = Directory.GetFiles(chronoVehicleDataRoot, "*.json", SearchOption.AllDirectories);

        foreach (var absPath in allJsonPaths)
        {
            string relPath = ToRelativePath(chronoVehicleDataRoot, absPath);
            if (string.IsNullOrEmpty(relPath))
                continue;

            // try to parse the json and get the type out
            try
            {
                string text = File.ReadAllText(absPath);
                JObject root = JObject.Parse(text);
                string typeVal = root["Type"]?.ToString() ?? "Unknown";

                // if no entry yet for this type, create an empty list
                if (!result.ContainsKey(typeVal))
                {
                    result[typeVal] = new List<string>();
                }
                // add this file's relative path to the appropriate list
                result[typeVal].Add(relPath);
            }
            catch
            {
                // if it fails to parse, store under "Invalid"
                if (!result.ContainsKey("Invalid"))
                {
                    result["Invalid"] = new List<string>();
                }
                result["Invalid"].Add(relPath);
            }
        }

        foreach (var kvp in result)
        {
            kvp.Value.Sort();  // sorts each sub-list of file paths
        }

        return result;
    }

    // Convert absolute path to a subpath relative to chrono vehicle data folder
    public static string ToRelativePath(string chronoVehicleDataRoot, string absolutePath)
    {
        try
        {
            string rootFull = Path.GetFullPath(chronoVehicleDataRoot).Replace("\\", "/");
            string absFull = Path.GetFullPath(absolutePath).Replace("\\", "/");

            // if the absolute path doesn't start with our root, it's outside the directory
            if (!absFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return null;

            // pull out the portion after the root
            string sub = absFull.Substring(rootFull.Length).TrimStart('/', '\\');
            return sub;
        }
        catch
        {
            return null;
        }
    }

    // rebuild the multi-level cache: subFolder -> Type -> List of files.
    // e.g, for the "Nissan_Patrol" -> { "Chassis" -> ["Nissan_Patrol/json/suv_Chassis.json", ...], ...}
    public static void RebuildSubcomponentCache(string chronoVehicleDataRoot)
    {
        s_subcomponentCache.Clear();
        Dictionary<string, List<string>> rawByType = UChVehGenJSONUtils.GetSubcomponentFiles(chronoVehicleDataRoot);
        Dictionary<string, List<string>> typedFiles = UChVehGenJSONUtils.GetSubcomponentFilesFromCache();

        foreach (var kvp in rawByType)
        {
            string typeName = kvp.Key;  // e.g. "Chassis", "Engine", etc.
            foreach (string relPath in kvp.Value) // loop over each path in kvp.Value
            {
                // Attempt to parse the subfolder, first segment of relPath is assumed to be the subFolder
                string[] parts = relPath.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;
                string subFolder = parts[0];

                if (!s_subcomponentCache.ContainsKey(subFolder))
                    s_subcomponentCache[subFolder] = new Dictionary<string, List<string>>(
                        StringComparer.OrdinalIgnoreCase // ensure subfolders are case insensitive
                    );

                // If typeName not yet in subFolder dictionary, add it
                if (!s_subcomponentCache[subFolder].ContainsKey(typeName))
                    s_subcomponentCache[subFolder][typeName] = new List<string>();

                // Add the relPath to list
                s_subcomponentCache[subFolder][typeName].Add(relPath);
            }
        }
    }

    // ------------------------------------------------------------------------
    // LOADING / SAVING VEHICLE
    // ------------------------------------------------------------------------

    // load a top-level vehicle json (Type="Vehicle") from a subpath, parse it
    public static VehicleData LoadVehicleData(string chronoVehicleDataRoot, string subPath)
    {
        JObject root = LoadJson(chronoVehicleDataRoot, subPath);
        if (root == null)
            return null;

        try
        {
            // Build VehicleData instance from the json
            VehicleData data = new VehicleData
            {
                Name = root["Name"]?.ToString(),
                Type = root["Type"]?.ToString(),
                Template = root["Template"]?.ToString(),
                Chassis = root["Chassis"]?.ToObject<ComponentReference>(),
                Engine = root["Engine"]?.ToObject<ComponentReference>(),
                Transmission = root["Transmission"]?.ToObject<ComponentReference>(),
                Driveline = root["Driveline"]?.ToObject<ComponentReference>(),
                Wheelbase = root["Wheelbase"]?.ToObject<float>() ?? 3.0f,
                MinimumTurningRadius = root["Minimum Turning Radius"]?.ToObject<float>() ?? 7.0f,
                MaximumSteeringAngleDeg = root["Maximum Steering Angle (deg)"]?.ToObject<float>() ?? 30f,
                Geometry = new VehicleGeometryData()
            };

            // extra fields
            HashSet<string> knownFields = new HashSet<string>
            {
                "Name", "Type", "Template",
                "Chassis", "Engine", "Transmission", "Driveline",
                "Axles", "Steering Subsystems",
                "Wheelbase", "Minimum Turning Radius", "Maximum Steering Angle (deg)","Contact" 
                // there's also visualisation, but we haven't got that connected to Chrono Unity. no need...
            };

            // Loop over all properties in root and store any that are not known
            foreach (var prop in root.Properties())
            {
                if (!knownFields.Contains(prop.Name))
                {
                    data.ExtraFields[prop.Name] = prop.Value;
                }
            }

            // Axles
            data.Axles = new List<AxleEntry>();
            if (root["Axles"] is JArray axArr)
            {
                foreach (var axToken in axArr)
                {
                    if (axToken is JObject axObj)
                    {
                        AxleEntry ax = new AxleEntry
                        {
                            Suspension = axObj["Suspension Input File"]?.ToString(),
                            SuspensionLocation = ParseVector3Array(axObj["Suspension Location"]),
                            SteeringIndex = axObj["Steering Index"]?.ToObject<int>() ?? 0,
                            LeftWheel = axObj["Left Wheel Input File"]?.ToString(),
                            RightWheel = axObj["Right Wheel Input File"]?.ToString(),
                            LeftBrake = axObj["Left Brake Input File"]?.ToString(),
                            RightBrake = axObj["Right Brake Input File"]?.ToString()
                        };
                        data.Axles.Add(ax);
                    }
                }
            }

            // Steering
            data.SteeringSubsystems = new List<SteeringEntry>();
            if (root["Steering Subsystems"] is JArray stArr)
            {
                foreach (var stToken in stArr)
                {
                    if (stToken is JObject stObj)
                    {
                        SteeringEntry st = new SteeringEntry
                        {
                            Steering = stObj["Input File"]?.ToString(),
                            Location = ParseVector3Array(stObj["Location"]),
                            Orientation = ParseQuaternionArray(stObj["Orientation"])
                        }; 
                        data.SteeringSubsystems.Add(st);
                    }
                }
            }

            // ---- Parse vehicle geometry & collision data ----
            if (root["Contact"] is JObject contactObj)
            {
                // materials
                if (contactObj["Materials"] is JArray matArr)
                {
                    // pull the materials from the json safely.. check if they're there. there's others, but these are most common
                    foreach (var matToken in matArr)
                    {
                        VehicleContactMaterial mat = new VehicleContactMaterial();
                        mat.Mu = matToken["Coefficient of Friction"]?.ToObject<float>() ?? 0f;
                        mat.Cr = matToken["Coefficient of Restitution"]?.ToObject<float>() ?? 0f;

                        if (matToken["Properties"] is JObject props)
                        {
                            mat.YoungModulus = props["Young Modulus"]?.ToObject<float>() ?? 0f;
                            mat.PoissonRatio = props["Poisson Ratio"]?.ToObject<float>() ?? 0f;
                        }

                        if (matToken["Coefficients"] is JObject coeffs)
                        {
                            mat.NormalStiffness = coeffs["Normal Stiffness"]?.ToObject<float>() ?? 0f;
                            mat.NormalDamping = coeffs["Normal Damping"]?.ToObject<float>() ?? 0f;
                            mat.TangentialStiffness = coeffs["Tangential Stiffness"]?.ToObject<float>() ?? 0f;
                            mat.TangentialDamping = coeffs["Tangential Damping"]?.ToObject<float>() ?? 0f;
                        }
                        data.Geometry.Materials.Add(mat);
                    }
                }

                // shapes
                if (contactObj["Shapes"] is JArray shapeArr)
                {
                    foreach (var shapeToken in shapeArr)
                    {
                        VehicleContactShape shape = new VehicleContactShape();
                        shape.Type = shapeToken["Type"]?.ToString();
                        shape.MaterialIndex = shapeToken["Material Index"]?.ToObject<int>() ?? -1;

                        // location (array)
                        if (shapeToken["Location"] is JArray locArray)
                            shape.Location = ParseVector3Array(locArray);

                        // orientation (array)
                        if (shapeToken["Orientation"] is JArray rotArray)
                            shape.Orientation = ParseQuaternionArray(rotArray);

                        // shape-specific
                        shape.Radius = shapeToken["Radius"]?.ToObject<float>() ?? 0f;
                        shape.Length = shapeToken["Length"]?.ToObject<float>() ?? 0f;

                        // check if dimensions if there's a box shape
                        if (shapeToken["Dimensions"] is JArray dimArray)
                            shape.Dimensions = ParseVector3Array(dimArray);

                        // axis for cylinder
                        if (shapeToken["Axis"] is JArray axisArray)
                            shape.Axis = ParseVector3Array(axisArray);

                        // convex hull or mesh
                        shape.Filename = shapeToken["Filename"]?.ToString();
                        shape.ContactRadius = shapeToken["Contact Radius"]?.ToObject<float>() ?? 0f;

                        // add the shape
                        data.Geometry.Shapes.Add(shape);
                    }
                }
            }

            return data;
        }
        catch (Exception ex) // catch any error that happened during parsing
        {
            Debug.LogError($"LoadVehicleData: Failed to parse {subPath}: {ex.Message}");
            return null;
        }
    }

    // turn 3 array json into vectr3
    private static Vector3 ParseVector3Array(JToken token)
    {
        if (token is JArray arr && arr.Count == 3)
        {
            float x = arr[0].ToObject<float>();
            float y = arr[1].ToObject<float>();
            float z = arr[2].ToObject<float>();
            return new Vector3(x, y, z);
        }
        Debug.LogWarning("ParseVector3Array: token is not a 3-float array => returning Vector3.zero");
        return Vector3.zero;
    }

    // parse [x,y,z,w] into a unity quat
    // NB: this is duplicated in the quat serialiser class converter, but this is a quick easy parse function to
    // call without needing to deal with .toobject, but can be handled in-line.
    private static Quaternion ParseQuaternionArray(JToken token)
    {
        // Check if 4 elements
        if (token is JArray arr && arr.Count == 4)
        {
            float x = arr[0].ToObject<float>();
            float y = arr[1].ToObject<float>();
            float z = arr[2].ToObject<float>();
            float w = arr[3].ToObject<float>();
            return new Quaternion(x, y, z, w);
        }
        Debug.LogWarning("ParseQuaternionArray: token is not a 4-float array => returning Quaternion.identity");
        return Quaternion.identity;
    }

    public static void BuildDisplayListFromFilenames(
        IList<string> fileSubPaths,
        out List<string> displayNames,
        out List<string> actualPaths
    )
    {
        // prep two output lists
        displayNames = new List<string>();
        actualPaths = new List<string>();

        Dictionary<string, int> nameCount = new Dictionary<string, int>();
        foreach (var relPath in fileSubPaths)
        {
            string baseName = Path.GetFileNameWithoutExtension(relPath);

            // check for duplicate
            if (!nameCount.ContainsKey(baseName))
            {
                // if not, begin tracking
                nameCount[baseName] = 1;
                displayNames.Add(baseName);
            }
            else
            {
                // If yes, append a suffix
                int count = nameCount[baseName];
                nameCount[baseName] = count + 1;
                baseName = $"{baseName} ({count})";
                displayNames.Add(baseName);
            }

            // In either case add the relPath to actualPaths
            actualPaths.Add(relPath);
        }
    }

    // ------------------------------------------------------------------------
    // CUSTOM JSON CONVERTERS
    // ------------------------------------------------------------------------

    // converts Vector3 to and from JSON arrays
    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteValue(value.x);
            writer.WriteValue(value.y);
            writer.WriteValue(value.z);
            writer.WriteEndArray();
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JArray array = JArray.Load(reader);
            // ensure length is exactly 3
            if (array.Count != 3)
                throw new JsonSerializationException("Expected array of three elements for Vector3.");

            float x = array[0].ToObject<float>();
            float y = array[1].ToObject<float>();
            float z = array[2].ToObject<float>();
            return new Vector3(x, y, z);
        }
    }

    // converts quat to and from JSON arrays when saving.
    public class QuaternionConverter : JsonConverter<Quaternion>
    {
        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            writer.WriteValue(value.x);
            writer.WriteValue(value.y);
            writer.WriteValue(value.z);
            writer.WriteValue(value.w);
            writer.WriteEndArray();
        }

        public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JArray array = JArray.Load(reader);
            if (array.Count != 4)
                throw new JsonSerializationException("Expected array of four elements for Quaternion.");

            float x = array[0].ToObject<float>();
            float y = array[1].ToObject<float>();
            float z = array[2].ToObject<float>();
            float w = array[3].ToObject<float>();
            return new Quaternion(x, y, z, w);
        }
    }

    // creates a serialiser instance with all custom converters
    public static JsonSerializer CreateChronoSerializer()
    {
        var serializer = new JsonSerializer();
        // Add custom converters
        serializer.Converters.Add(new Vector3Converter());
        serializer.Converters.Add(new QuaternionConverter());
        return serializer;
    }

    // return a clone of the default JSON for the given type+template if it exists.
    public static JObject GetDefaultSubcomponentJson(string type, string template)
    {
        if (UChVehGenJSONTemplates.defaultTemplates.TryGetValue(type, out var templateDict))
            {
                if (templateDict.TryGetValue(template, out var jObj))
            {
                // return a deep clone so we don't modify the dictionary's original
                return (JObject)jObj.DeepClone();
            }
        }
        return null; // not found
    }


    // returns a list of template names for the given subcomponent
    // e.g. "Engine" will provide use "EngineSimple", "EngineSimpleMap", "EngineShafts"
    public static List<string> GetTemplateListForType(string subcomponentType)
    {
        List<string> result = new List<string>();
        if (UChVehGenJSONTemplates.defaultTemplates.TryGetValue(subcomponentType, out var templateDict))
        {
            result.AddRange(templateDict.Keys);
        }
        result.Sort();
        return result;
    }
}
