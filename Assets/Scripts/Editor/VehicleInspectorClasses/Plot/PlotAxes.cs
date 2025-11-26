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
// Axis rendering (X=0 and Y=0 lines) (small class but kept seperate to avoid
// a monolithic Plot2D class )
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ChronoVehicleBuilder.Plotting
{
    /// <summary>
    /// Draws X and Y axes through origin
    /// </summary>
    public class PlotAxes
    {
        public Color AxisColor { get; set; } = Color.gray;
        public bool ShowXAxis { get; set; } = true;
        public bool ShowYAxis { get; set; } = true;
        
        public PlotAxes() { }
        
        /// <summary>
        /// Draw axes if they're in view
        /// </summary>
        public void Draw(Rect graphRect, PlotViewport viewport)
        {
            Handles.color = AxisColor;
            
            // X axis (horizontal line at Y=0)
            if (ShowXAxis && 0 >= viewport.MinY && 0 <= viewport.MaxY)
            {
                Vector2 left = viewport.WorldToScreen(new Vector2(viewport.MinX, 0), graphRect);
                Vector2 right = viewport.WorldToScreen(new Vector2(viewport.MaxX, 0), graphRect);
                Handles.DrawLine(left, right);
            }
            
            // Y axis (vertical line at X=0)
            if (ShowYAxis && 0 >= viewport.MinX && 0 <= viewport.MaxX)
            {
                Vector2 bottom = viewport.WorldToScreen(new Vector2(0, viewport.MinY), graphRect);
                Vector2 top = viewport.WorldToScreen(new Vector2(0, viewport.MaxY), graphRect);
                Handles.DrawLine(bottom, top);
            }
        }
    }
}
#endif
