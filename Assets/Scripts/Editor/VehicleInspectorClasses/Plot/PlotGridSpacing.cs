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
// Calculates grid spacing for graph axes from a given data range to give
// readable grid lines (e.g., 0.1, 0.2, 0.5, 1, 2, 5, 10, etc.)
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;

namespace ChronoVehicleBuilder
{

    public static class PlotGridSpacing
        {
        public static float CalculateGridStep(float minVal, float maxVal, float targetLines = 8f)
        {
            float range = Mathf.Abs(maxVal - minVal);
            if (range < 1e-9f) return 0.1f;

            // Target ~8-10 grid lines
            float rawStep = range / targetLines;
            
            // Find the order of magnitude
            float logStep = Mathf.Log10(rawStep);
            int magnitude = Mathf.FloorToInt(logStep);
            float powerOf10 = Mathf.Pow(10f, magnitude);
            
            // Normalize to range [1, 10)
            float normalized = rawStep / powerOf10;

            // Choose clean step: 1, 2, 2.5, 5, or 10
            float step;
            if (normalized <= 1.5f)
                step = 1f * powerOf10;
            else if (normalized <= 2.25f)
                step = 2f * powerOf10;
            else if (normalized <= 3.5f)
                step = 2.5f * powerOf10;
            else if (normalized <= 7.5f)
                step = 5f * powerOf10;
            else
                step = 10f * powerOf10;

            return step;
        }

        /// <summary>
        /// Format a tick value with appropriate precision based on the step size
        /// </summary>
        public static string FormatTickValue(float val, float step)
        {
            float stepAbs = Mathf.Abs(step);
            if (stepAbs < 1e-12f)
            {
                return val.ToString("0.00");
            }

            // Treat very small values as zero
            if (Mathf.Abs(val) < stepAbs * 0.001f)
            {
                val = 0f;
            }

            float exponent = Mathf.Floor(Mathf.Log10(stepAbs));
            float magnitude = Mathf.Pow(10f, exponent);
            float normalized = stepAbs / magnitude;

            int decimals = Mathf.Max(0, (int)(-exponent));
            
            // Add extra decimal for 2.5 multiples in sub-unit range
            if (exponent < 0f && normalized > 2.0f + 1e-3f && normalized < 3.0f)
            {
                decimals += 1;
            }

            decimals = Mathf.Clamp(decimals, 0, 6);

            return val.ToString("F" + decimals);
        }
    }
}
#endif
