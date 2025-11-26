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
// Data series - represents a line/curve with points
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace ChronoVehicleBuilder.Plotting
{
    /// <summary>
    /// A single data series (line with points)
    /// </summary>
    public class PlotSeries
    {
        public string Name { get; set; }
        public List<Vector2> Points { get; private set; }
        public Color Color { get; set; } = Color.green;
        public bool IsEditable { get; set; } = false;
        public bool IsVisible { get; set; } = true;
        public bool ShowLabels { get; set; } = false;
        
        public float LineWidth { get; set; } = 1f;
    public float PointRadius { get; set; } = 4.5f;
    public float HoverRadius { get; set; } = 8f;
        
        private int draggingPointIndex = -1;
        private int hoveredPointIndex = -1;
        
        public int PointCount => Points?.Count ?? 0;
        public bool IsDragging => draggingPointIndex >= 0;
        
        private class PlacedLabel
        {
            public Vector2 pointWorld;
            public Vector2 pointScreen;
            public Rect rect;
            public string text;
        }
        
        public PlotSeries(string name, List<Vector2> points, Color color)
        {
            Name = name;
            Points = points != null ? new List<Vector2>(points) : new List<Vector2>();
            Color = color;
        }

        /// <summary>
        /// Replace the point collection while preserving interaction state
        /// </summary>
        public void SetPoints(List<Vector2> source)
        {
            if (Points == null)
                Points = new List<Vector2>();

            Points.Clear();

            if (source == null)
                return;

            Points.AddRange(source);
        }
        
        /// <summary>
        /// Draw the series (lines and points)
        /// </summary>
        public void Draw(Rect graphRect, PlotViewport viewport)
        {
            if (!IsVisible || Points == null || Points.Count == 0)
                return;
            
            // Draw connecting lines with proper clipping
            Handles.color = Color;
            for (int i = 0; i < Points.Count - 1; i++)
            {
                Vector2 p1 = Points[i];
                Vector2 p2 = Points[i + 1];
                
                // Clip line segment to viewport bounds
                if (ClipLineSegment(ref p1, ref p2, viewport))
                {
                    Vector2 sp1 = viewport.WorldToScreen(p1, graphRect);
                    Vector2 sp2 = viewport.WorldToScreen(p2, graphRect);
                    Handles.DrawLine(sp1, sp2);
                }
            }
            
            // Draw points
            for (int i = 0; i < Points.Count; i++)
            {
                if (!viewport.Contains(Points[i]))
                    continue;
                
                Vector2 screenPos = viewport.WorldToScreen(Points[i], graphRect);
                
                Color pointColor = Color;
                float radius = PointRadius;
                
                if (draggingPointIndex == i)
                {
                    pointColor = Color.cyan;
                    radius = HoverRadius;
                }
                else if (hoveredPointIndex == i)
                {
                    pointColor = Color.yellow;
                    radius = HoverRadius;
                }
                
                // Draw point
                Handles.color = pointColor;
                Handles.DrawSolidDisc(screenPos, Vector3.forward, radius);
            }
            
            // Draw labels if enabled
            if (ShowLabels && Points.Count > 0)
            {
                List<PlacedLabel> labels = PlaceLabelsNearPoints(graphRect, viewport);
                RelaxLabelPositions(labels, graphRect, 40);
                DrawAllLabels(labels);
            }
        }
        
        /// <summary>
        /// Handle point editing interactions
        /// </summary>
        public void HandleEdit(Rect graphRect, PlotViewport viewport)
        {
            if (!IsEditable || Points == null)
                return;
            
            Event e = Event.current;
            
            // important - update hover state ONLY when not currently dragging a point
            // This prevents picking up additional points during a drag operation
            // and having them all meld together under the mouse
            if (draggingPointIndex < 0)
            {
                // Check hover on BOTH MouseMove (immediate) and Repaint (visual update)
                if (e.type == EventType.MouseMove || e.type == EventType.Repaint)
                {
                    Vector2 hoverPos = e.mousePosition;
                    int newHover = -1;
                    
                    // Only check for hover if mouse is actually in THIS graph's bounds
                    if (graphRect.Contains(hoverPos))
                    {
                        newHover = FindNearestPoint(hoverPos, graphRect, viewport, HoverRadius * 3f);
                    }
                    // If mouse is NOT in this graph, clear hover
                    else
                    {
                        newHover = -1;
                    }

                    if (newHover != hoveredPointIndex)
                    {
                        hoveredPointIndex = newHover;
                        GUI.changed = true;
                        // Force a repaint right away when the hover state changes
                        if (e.type == EventType.MouseMove)
                        {
                            HandleUtility.Repaint();
                        }
                    }
                }
            }
            
            // Handle outside bounds during interactions
            if (!graphRect.Contains(e.mousePosition))
            {
                // Clear hover when mouse leaves during repaint
                if (hoveredPointIndex >= 0 && e.type == EventType.Repaint)
                {
                    hoveredPointIndex = -1;
                    GUI.changed = true;
                }
                if (e.type == EventType.MouseUp && e.button == 0 && draggingPointIndex >= 0)
                {
                    draggingPointIndex = -1;
                    e.Use();
                    GUI.changed = true;
                }
                return;
            }
            
            Vector2 mousePos = e.mousePosition;
            Vector2 worldMouse = viewport.ScreenToWorld(mousePos, graphRect);
            
            // Double-click: insert point (check first to avoid single-click handling)
            if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2)
            {
                InsertPointSorted(worldMouse);
                e.Use();
                GUI.changed = true;
                HandleUtility.Repaint();
            }
            // Left-click down: pick point to drag
            else if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 1)
            {
                draggingPointIndex = FindNearestPoint(mousePos, graphRect, viewport, 10f);
                if (draggingPointIndex >= 0)
                {
                    // Clear hover state when starting to drag
                    hoveredPointIndex = -1;
                    e.Use();
                    GUI.changed = true;
                    HandleUtility.Repaint();
                }
            }
            // Left-drag: move point
            else if (e.type == EventType.MouseDrag && e.button == 0 && draggingPointIndex >= 0)
            {
                Points[draggingPointIndex] = worldMouse;
                e.Use();
                GUI.changed = true;
                HandleUtility.Repaint();
            }
            // Left-up: finish dragging
            else if (e.type == EventType.MouseUp && e.button == 0 && draggingPointIndex >= 0)
            {
                draggingPointIndex = -1;
                e.Use();
                GUI.changed = true;
                HandleUtility.Repaint();
            }
            // Delete key: remove selected
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete && hoveredPointIndex >= 0)
            {
                Points.RemoveAt(hoveredPointIndex);
                hoveredPointIndex = -1;
                e.Use();
                GUI.changed = true;
                HandleUtility.Repaint();
            }
        }
        
        private int FindNearestPoint(Vector2 mousePos, Rect graphRect, PlotViewport viewport, float maxDistance)
        {
            int bestIndex = -1;
            float bestDist = maxDistance;
            
            for (int i = 0; i < Points.Count; i++)
            {
                if (!viewport.Contains(Points[i]))
                    continue;
                
                Vector2 screenPos = viewport.WorldToScreen(Points[i], graphRect);
                float dist = Vector2.Distance(screenPos, mousePos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }
            
            return bestIndex;
        }
        
        private void InsertPointSorted(Vector2 newPoint)
        {
            int insertIndex = 0;
            for (; insertIndex < Points.Count; insertIndex++)
            {
                if (Points[insertIndex].x > newPoint.x)
                    break;
            }
            Points.Insert(insertIndex, newPoint);
        }
        
        /// <summary>
        /// Get bounding box of all points
        /// </summary>
        public Bounds2D GetBounds()
        {
            Bounds2D bounds = new Bounds2D();
            
            if (Points == null || Points.Count == 0)
                return bounds;
            
            foreach (var point in Points)
            {
                bounds.Encapsulate(point);
            }
            
            return bounds;
        }
        
        // Label placement with collision avoidance
        private List<PlacedLabel> PlaceLabelsNearPoints(Rect graphRect, PlotViewport viewport)
        {
            List<PlacedLabel> result = new List<PlacedLabel>();
            
            for (int i = 0; i < Points.Count; i++)
            {
                Vector2 wP = Points[i];
                if (!viewport.Contains(wP))
                    continue;
                
                Vector2 sP = viewport.WorldToScreen(wP, graphRect);
                string lbl = $"[{wP.x:F2},{wP.y:F2}]";
                
                Vector2 size = EditorStyles.label.CalcSize(new GUIContent(lbl));
                Vector2 offset = new Vector2(6, -8);
                
                float rx = sP.x + offset.x;
                float ry = sP.y + offset.y;
                Rect r = new Rect(rx, ry, size.x, size.y);
                
                // Clamp to graph rect
                if (r.x < graphRect.x) r.x = graphRect.x;
                if (r.xMax > graphRect.xMax) r.x = graphRect.xMax - r.width;
                if (r.y < graphRect.y) r.y = graphRect.y;
                if (r.yMax > graphRect.yMax) r.y = graphRect.yMax - r.height;
                
                result.Add(new PlacedLabel
                {
                    pointWorld = wP,
                    pointScreen = sP,
                    rect = r,
                    text = lbl
                });
            }
            
            return result;
        }
        
        private void RelaxLabelPositions(List<PlacedLabel> placed, Rect graphRect, int passes)
        {
            if (placed.Count < 2) return;
            
            for (int iter = 0; iter < passes; iter++)
            {
                bool anyOverlap = false;
                
                for (int i = 0; i < placed.Count; i++)
                {
                    for (int j = i + 1; j < placed.Count; j++)
                    {
                        Rect ri = placed[i].rect;
                        Rect rj = placed[j].rect;
                        if (ri.Overlaps(rj))
                        {
                            anyOverlap = true;
                            // Push them away from each other
                            Vector2 ci = new Vector2(ri.x + ri.width * 0.5f, ri.y + ri.height * 0.5f);
                            Vector2 cj = new Vector2(rj.x + rj.width * 0.5f, rj.y + rj.height * 0.5f);
                            Vector2 diff = cj - ci;
                            if (diff.sqrMagnitude < 0.001f)
                                diff = new Vector2(0.1f, -0.1f);
                            
                            diff.Normalize();
                            diff *= 2f;
                            
                            rj.x += diff.x;
                            rj.y += diff.y;
                            ri.x -= diff.x;
                            ri.y -= diff.y;
                            
                            // Clamp again
                            ri.x = Mathf.Clamp(ri.x, graphRect.x, graphRect.xMax - ri.width);
                            ri.y = Mathf.Clamp(ri.y, graphRect.y, graphRect.yMax - ri.height);
                            rj.x = Mathf.Clamp(rj.x, graphRect.x, graphRect.xMax - rj.width);
                            rj.y = Mathf.Clamp(rj.y, graphRect.y, graphRect.yMax - rj.height);
                            
                            placed[i].rect = ri;
                            placed[j].rect = rj;
                        }
                    }
                }
                
                if (!anyOverlap) break;
            }
        }
        
        private void DrawAllLabels(List<PlacedLabel> placed)
        {
            Handles.color = Color.gray;
            foreach (var pl in placed)
            {
                Vector2 center = new Vector2(pl.rect.x + pl.rect.width * 0.5f, pl.rect.y + pl.rect.height * 0.5f);
                Handles.DrawLine(pl.pointScreen, center);
                Handles.Label(new Vector2(pl.rect.x, pl.rect.y), pl.text);
            }
        }
        
        // Liang-Barsky line clipping algorithm
        private bool ClipLineSegment(ref Vector2 p1, ref Vector2 p2, PlotViewport viewport)
        {
            Vector2 d = p2 - p1;
            float t0 = 0f, t1 = 1f;
            
            if (ClipTest(-d.x, p1.x - viewport.MinX, ref t0, ref t1) &&
                ClipTest(d.x, viewport.MaxX - p1.x, ref t0, ref t1) &&
                ClipTest(-d.y, p1.y - viewport.MinY, ref t0, ref t1) &&
                ClipTest(d.y, viewport.MaxY - p1.y, ref t0, ref t1))
            {
                if (t1 < 1f) p2 = p1 + d * t1;
                if (t0 > 0f) p1 = p1 + d * t0;
                return true;
            }
            return false;
        }
        
        private bool ClipTest(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-9f)
            {
                if (q < 0f) return false;
                return true;
            }
            float r = q / p;
            if (p < 0f)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }
    }
}
#endif
