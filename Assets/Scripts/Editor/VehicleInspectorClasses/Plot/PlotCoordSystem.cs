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
// Handles coordinate transformations between world space (data values) and
// screen space (pixel positions) for graph plotting (isn't perfectly accurate
// but good enough for plotting purposes)
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;

namespace ChronoVehicleBuilder
{
    public class PlotCoordSystem
    {
        private Rect graphRect;
        private float minX, maxX, minY, maxY;

        public float MinX => minX;
        public float MaxX => maxX;
        public float MinY => minY;
        public float MaxY => maxY;
        public Rect GraphRect => graphRect;

        public PlotCoordSystem(Rect rect, float minX, float maxX, float minY, float maxY)
        {
            this.graphRect = rect;
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
        }

        /// <summary>
        /// Convert world coordinates to screen coordinates
        /// </summary>
        public Vector2 WorldToScreen(Vector2 world)
        {
            float dx = (world.x - minX) / (maxX - minX);
            float dy = (world.y - minY) / (maxY - minY);
            float px = graphRect.x + dx * graphRect.width;
            float py = graphRect.yMax - dy * graphRect.height;
            return new Vector2(px, py);
        }

        /// <summary>
        /// Convert screen coordinates to world coordinates
        /// </summary>
        public Vector2 ScreenToWorld(Vector2 screen)
        {
            float dx = (screen.x - graphRect.x) / graphRect.width;
            float dy = (graphRect.yMax - screen.y) / graphRect.height;
            float wx = minX + dx * (maxX - minX);
            float wy = minY + dy * (maxY - minY);
            return new Vector2(wx, wy);
        }

        /// <summary>
        /// Check if a world coordinate is visible in the current view.
        /// </summary>
        public bool IsVisible(Vector2 world)
        {
            return world.x >= minX && world.x <= maxX && world.y >= minY && world.y <= maxY;
        }

        /// <summary>
        /// Check if a screen coordinate is within the graph area
        /// </summary>
        public bool ContainsScreenPoint(Vector2 screen)
        {
            return graphRect.Contains(screen);
        }

        /// <summary>
        /// Update the view bounds (for zoom/pan)
        /// </summary>
        public void SetViewBounds(float minX, float maxX, float minY, float maxY)
        {
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
        }

        /// <summary>
        /// Update the graph rectangle (for resizing)
        /// </summary>
        public void SetGraphRect(Rect rect)
        {
            this.graphRect = rect;
        }
    }
}
#endif
