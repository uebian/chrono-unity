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
// Viewport management - handles view bounds, zoom, and coordinate transforms
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;

namespace ChronoVehicleBuilder.Plotting
{
    /// <summary>
    /// Manages the visible region of a plot and coordinate transformations
    /// </summary>
    public class PlotViewport
    {
        public float MinX { get; set; } = 0f;
        public float MaxX { get; set; } = 1f;
        public float MinY { get; set; } = 0f;
        public float MaxY { get; set; } = 1f;
        
        public bool IsInitialized { get; private set; } = false;
        
        public PlotViewport() { }
        
        public PlotViewport(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            IsInitialized = true;
        }
        
        /// <summary>
        /// Fit viewport to given bounds with padding
        /// </summary>
        public void FitToBounds(Bounds2D bounds, float padding = 0.1f)
        {
            float dx = Mathf.Max(1e-3f, bounds.Width);
            float dy = Mathf.Max(1e-3f, bounds.Height);
            
            MinX = bounds.MinX - dx * padding;
            MaxX = bounds.MaxX + dx * padding;
            MinY = bounds.MinY - dy * padding;
            MaxY = bounds.MaxY + dy * padding;
            
            IsInitialized = true;
        }
        
        /// <summary>
        /// Zoom viewport by factor around center point
        /// </summary>
        public void Zoom(float factor, Vector2? centerWorld = null)
        {
            factor = Mathf.Clamp(factor, 0.01f, 100f);
            
            Vector2 center = centerWorld ?? new Vector2((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);
            
            float halfWidth = (MaxX - MinX) * 0.5f * factor;
            float halfHeight = (MaxY - MinY) * 0.5f * factor;
            
            MinX = center.x - halfWidth;
            MaxX = center.x + halfWidth;
            MinY = center.y - halfHeight;
            MaxY = center.y + halfHeight;
        }
        
        /// <summary>
        /// Pan viewport by delta in world coordinates
        /// </summary>
        public void Pan(float deltaX, float deltaY)
        {
            MinX += deltaX;
            MaxX += deltaX;
            MinY += deltaY;
            MaxY += deltaY;
        }
        
        /// <summary>
        /// Convert world coordinates to screen coordinates
        /// </summary>
        public Vector2 WorldToScreen(Vector2 world, Rect screenRect)
        {
            float dx = (world.x - MinX) / (MaxX - MinX);
            float dy = (world.y - MinY) / (MaxY - MinY);
            
            float px = screenRect.x + dx * screenRect.width;
            float py = screenRect.yMax - dy * screenRect.height; // Flip Y
            
            return new Vector2(px, py);
        }
        
        /// <summary>
        /// Convert screen coordinates to world coordinates
        /// </summary>
        public Vector2 ScreenToWorld(Vector2 screen, Rect screenRect)
        {
            float dx = (screen.x - screenRect.x) / screenRect.width;
            float dy = (screenRect.yMax - screen.y) / screenRect.height; // Flip Y
            
            float wx = MinX + dx * (MaxX - MinX);
            float wy = MinY + dy * (MaxY - MinY);
            
            return new Vector2(wx, wy);
        }
        
        /// <summary>
        /// Check if world point is visible
        /// </summary>
        public bool Contains(Vector2 world)
        {
            return world.x >= MinX && world.x <= MaxX &&
                   world.y >= MinY && world.y <= MaxY;
        }
        
        /// <summary>
        /// Reset to uninitialized state
        /// </summary>
        public void Reset()
        {
            MinX = 0f;
            MaxX = 1f;
            MinY = 0f;
            MaxY = 1f;
            IsInitialized = false;
        }
        
        /// <summary>
        /// Get center point
        /// </summary>
        public Vector2 Center => new Vector2((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);
        
        /// <summary>
        /// Get viewport width
        /// </summary>
        public float Width => MaxX - MinX;
        
        /// <summary>
        /// Get viewport height
        /// </summary>
        public float Height => MaxY - MinY;
    }
}
#endif
