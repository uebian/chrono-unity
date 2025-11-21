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
// 2D bounding box for plot data
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;

namespace ChronoVehicleBuilder.Plotting
{
    public class Bounds2D
    {
        public float MinX { get; set; } = float.MaxValue;
        public float MaxX { get; set; } = float.MinValue;
        public float MinY { get; set; } = float.MaxValue;
        public float MaxY { get; set; } = float.MinValue;
        
        public bool IsValid => MinX <= MaxX && MinY <= MaxY;
        
        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;
        public Vector2 Center => new Vector2((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);
        
        public Bounds2D() { }
        
        public Bounds2D(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }
        
        /// <summary>
        /// Expand bounds to include a point
        /// </summary>
        public void Encapsulate(Vector2 point)
        {
            MinX = Mathf.Min(MinX, point.x);
            MaxX = Mathf.Max(MaxX, point.x);
            MinY = Mathf.Min(MinY, point.y);
            MaxY = Mathf.Max(MaxY, point.y);
        }
        
        /// <summary>
        /// Expand bounds to include another bounds
        /// </summary>
        public void Encapsulate(Bounds2D other)
        {
            if (!other.IsValid) return;
            
            MinX = Mathf.Min(MinX, other.MinX);
            MaxX = Mathf.Max(MaxX, other.MaxX);
            MinY = Mathf.Min(MinY, other.MinY);
            MaxY = Mathf.Max(MaxY, other.MaxY);
        }
        
        /// <summary>
        /// Check if point is within bounds
        /// </summary>
        public bool Contains(Vector2 point)
        {
            return point.x >= MinX && point.x <= MaxX &&
                   point.y >= MinY && point.y <= MaxY;
        }
        
        /// <summary>
        /// Expand bounds by margin
        /// </summary>
        public void Expand(float margin)
        {
            MinX -= margin;
            MaxX += margin;
            MinY -= margin;
            MaxY += margin;
        }
        
        /// <summary>
        /// Reset to invalid state
        /// </summary>
        public void Reset()
        {
            MinX = float.MaxValue;
            MaxX = float.MinValue;
            MinY = float.MaxValue;
            MaxY = float.MinValue;
        }
    }
}
#endif
