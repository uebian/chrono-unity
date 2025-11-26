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
// Grid rendering with automatic step calculation and labels
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;

namespace ChronoVehicleBuilder.Plotting
{

    public class PlotGrid
    {
        public Color GridColor { get; set; } = new Color(1f, 1f, 1f, 0.1f);
        public float TargetLineCount { get; set; } = 8f;
        
        private GUIStyle xLabelStyle;
        private GUIStyle yLabelStyle;
        
        public PlotGrid() { }
        
        /// <summary>
        /// Draw grid lines and labels
        /// </summary>
        public void Draw(Rect graphRect, PlotViewport viewport)
        {
            InitializeStyles();
            
            float gridStepX = CalculateGridStep(viewport.MinX, viewport.MaxX);
            float gridStepY = CalculateGridStep(viewport.MinY, viewport.MaxY);
            
            Handles.color = GridColor;
            
            // Vertical lines
            DrawVerticalGridLines(graphRect, viewport, gridStepX);
            
            // Horizontal lines
            DrawHorizontalGridLines(graphRect, viewport, gridStepY);
        }
        
        private void DrawVerticalGridLines(Rect graphRect, PlotViewport viewport, float step)
        {
            int startIndex = Mathf.FloorToInt(viewport.MinX / step);
            
            for (int i = 0; i < 500; i++) // Safety limit
            {
                float x = (startIndex + i) * step;
                if (x < viewport.MinX) continue;
                if (x > viewport.MaxX) break;
                
                Vector2 screenBottom = viewport.WorldToScreen(new Vector2(x, viewport.MinY), graphRect);
                Vector2 screenTop = viewport.WorldToScreen(new Vector2(x, viewport.MaxY), graphRect);
                
                Handles.DrawLine(screenBottom, screenTop);
                
                // Label
                string label = FormatTickValue(x, step);
                GUIContent content = new GUIContent(label);
                Vector2 labelSize = xLabelStyle.CalcSize(content);
                float labelY = graphRect.yMax + 6f;
                Rect labelRect = new Rect(screenBottom.x - labelSize.x * 0.5f, labelY, labelSize.x, labelSize.y);
                if (IsLabelWithinBounds(labelRect, graphRect, true))
                {
                    GUI.Label(labelRect, content, xLabelStyle);
                }
            }
        }
        
        private void DrawHorizontalGridLines(Rect graphRect, PlotViewport viewport, float step)
        {
            int startIndex = Mathf.FloorToInt(viewport.MinY / step);
            
            for (int i = 0; i < 500; i++) // Safety limit
            {
                float y = (startIndex + i) * step;
                if (y < viewport.MinY) continue;
                if (y > viewport.MaxY) break;
                
                Vector2 screenLeft = viewport.WorldToScreen(new Vector2(viewport.MinX, y), graphRect);
                Vector2 screenRight = viewport.WorldToScreen(new Vector2(viewport.MaxX, y), graphRect);
                
                Handles.DrawLine(screenLeft, screenRight);
                
                // Label
                string label = FormatTickValue(y, step);
                GUIContent content = new GUIContent(label);
                Vector2 labelSize = yLabelStyle.CalcSize(content);
                float labelX = graphRect.xMin - labelSize.x - 6f;
                Rect labelRect = new Rect(labelX, screenLeft.y - labelSize.y * 0.5f, labelSize.x, labelSize.y);
                if (IsLabelWithinBounds(labelRect, graphRect, false))
                {
                    GUI.Label(labelRect, content, yLabelStyle);
                }
            }
        }
        
        // for automatic grid step calculation
        private float CalculateGridStep(float minVal, float maxVal)
        {
            float range = Mathf.Abs(maxVal - minVal);
            if (range < 1e-9f) return 0.1f;
            
            float rawStep = range / TargetLineCount;
            
            float logStep = Mathf.Log10(rawStep);
            int magnitude = Mathf.FloorToInt(logStep);
            float powerOf10 = Mathf.Pow(10f, magnitude);
            
            float normalized = rawStep / powerOf10;
            
            // Choose clean step
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
        
        private string FormatTickValue(float val, float step)
        {
            float stepAbs = Mathf.Abs(step);
            if (stepAbs < 1e-12f)
                return val.ToString("0.00");
            
            // Suppress near-zero values
            if (Mathf.Abs(val) < stepAbs * 0.001f)
                val = 0f;
            
            // Determine decimal places
            float exponent = Mathf.Floor(Mathf.Log10(stepAbs));
            int decimals = Mathf.Max(0, (int)(-exponent));
            
            // Add extra decimal for 2.5 steps in sub-unit range
            float magnitude = Mathf.Pow(10f, exponent);
            float normalized = stepAbs / magnitude;
            if (exponent < 0f && normalized > 2.0f + 1e-3f && normalized < 3.0f)
            {
                decimals += 1;
            }
            
            decimals = Mathf.Clamp(decimals, 0, 6);
            
            return val.ToString("F" + decimals);
        }
        
        private void InitializeStyles()
        {
            if (xLabelStyle == null)
            {
                xLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 9,
                    font = EditorStyles.miniLabel.font // Explicitly copy font reference
                };
                xLabelStyle.normal.textColor = Color.white;
            }
            
            if (yLabelStyle == null)
            {
                yLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 9,
                    font = EditorStyles.miniLabel.font // Explicitly copy font reference
                };
                yLabelStyle.normal.textColor = Color.white;
            }
        }

        private bool IsLabelWithinBounds(Rect labelRect, Rect graphRect, bool isXAxisLabel)
        {
            const float margin = 2f;

            if (isXAxisLabel)
            {
                // Keep labels roughly aligned under the plot without culling useful ticks
                if (labelRect.xMax < graphRect.xMin + margin)
                    return false;
                if (labelRect.xMin > graphRect.xMax - margin)
                    return false;
                return true;
            }

            // Y-axis labels: allow slight overshoot but avoid drifting below/above plot area
            if (labelRect.yMax < graphRect.yMin - margin)
                return false;
            if (labelRect.yMin > graphRect.yMax + margin)
                return false;

            return true;
        }
    }
}
#endif
