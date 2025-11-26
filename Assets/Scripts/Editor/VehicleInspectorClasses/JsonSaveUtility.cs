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
// save promps for JSON save operations - ensures everything gets a consistent
// "are you sure" dialog
// =============================================================================

using UnityEditor;

namespace VehicleBuilder.Editor
{

    internal static class JsonSaveUtility
    {
        public static bool ConfirmSave(string description, string path)
        {
            string label = string.IsNullOrEmpty(description) ? "JSON" : description;
            string message = string.IsNullOrEmpty(path)
                ? $"Are you sure you want to save the {label}?"
                : $"Are you sure you want to save the {label}?\n{path}";

            return EditorUtility.DisplayDialog("Confirm Save", message, "Save", "Cancel");
        }

        public static bool ConfirmSaveAs(string description, string path)
        {
            string label = string.IsNullOrEmpty(description) ? "JSON" : description;
            string safePath = string.IsNullOrEmpty(path) ? "(new file)" : path;
            string message = $"Are you sure you want to save the {label} as:\n{safePath}?";
            return EditorUtility.DisplayDialog("Confirm Save As", message, "Save", "Cancel");
        }
    }
}
