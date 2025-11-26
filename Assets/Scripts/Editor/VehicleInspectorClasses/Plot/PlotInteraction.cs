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
// User interaction handling - zoom, pan, reset
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ChronoVehicleBuilder.Plotting
{
    /// <summary>
    /// Handles user interactions: zoom, pan, reset
    /// </summary>
    public class PlotInteraction
    {
        private PlotViewport viewport;
        
        // Pan state
        private bool isPanning = false;
        private Vector2 panStartMouse;
        private float panStartMinX, panStartMaxX, panStartMinY, panStartMaxY;
        private int panButton = -1;
        
        public float ZoomSensitivity { get; set; } = 0.03f;
        public float MinZoom { get; set; } = 0.01f;
        public float MaxZoom { get; set; } = 100f;
        
        public PlotInteraction(PlotViewport viewport)
        {
            this.viewport = viewport;
        }
        
        /// <summary>
        /// Handle mouse wheel zoom and pan interactions
        /// </summary>
        public void HandleInput(Rect graphRect, Bounds2D dataBounds)
        {
            Event e = Event.current;
            if (!graphRect.Contains(e.mousePosition))
                return;
            
            // Mouse wheel zoom
            if (e.type == EventType.ScrollWheel)
            {
                float zoomFactor = 1f - e.delta.y * ZoomSensitivity;
                zoomFactor = Mathf.Clamp(zoomFactor, MinZoom, MaxZoom);
                viewport.Zoom(zoomFactor);
                e.Use();
                GUI.changed = true;
                HandleUtility.Repaint();
            }
            // Double right-click: auto-fit
            else if (e.type == EventType.MouseDown && e.button == 1 && e.clickCount == 2)
            {
                viewport.FitToBounds(dataBounds, 0.1f);
                e.Use();
                GUI.changed = true;
                HandleUtility.Repaint();
            }
            // Right/middle click: start pan
            else if (e.type == EventType.MouseDown && (e.button == 1 || e.button == 2) && e.clickCount == 1)
            {
                isPanning = true;
                panStartMouse = e.mousePosition;
                panStartMinX = viewport.MinX;
                panStartMaxX = viewport.MaxX;
                panStartMinY = viewport.MinY;
                panStartMaxY = viewport.MaxY;
                panButton = e.button;
                e.Use();
            }
            // Drag: update pan
            else if (e.type == EventType.MouseDrag && isPanning && e.button == panButton)
            {
                Vector2 delta = e.mousePosition - panStartMouse;
                float viewWidth = panStartMaxX - panStartMinX;
                float viewHeight = panStartMaxY - panStartMinY;
                
                float dxFrac = -delta.x / graphRect.width;
                float dyFrac = delta.y / graphRect.height;
                
                viewport.MinX = panStartMinX + dxFrac * viewWidth;
                viewport.MaxX = panStartMaxX + dxFrac * viewWidth;
                viewport.MinY = panStartMinY + dyFrac * viewHeight;
                viewport.MaxY = panStartMaxY + dyFrac * viewHeight;
                
                e.Use();
                GUI.changed = true;
                HandleUtility.Repaint();
            }
            // Mouse up: end pan
            else if (e.type == EventType.MouseUp && isPanning && e.button == panButton)
            {
                isPanning = false;
                panButton = -1;
                e.Use();
                HandleUtility.Repaint();
            }
        }
    }
}
#endif
