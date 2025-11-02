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
// Class that can handle multiple [x,y] "Maps." Each map is pasrsed/identified
// by a string label (e.g. "Engine Map"). Features for each map are a numeric
// pair list (DrawPairs) to the left to show the x and y vectors and also an
// interactive 2D graph (DrawGraph) to the right hand side with multi-pass
// label collision shifting for each point label, zooming (with mouse wheel),
// pan (with right mouse) and double-click to add points or double right click to
// auto-scale, etc. hopefully relatively intuitively.
// Each map gets its own state s we don't get data mixing
// =============================================================================

using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;


public class UChVehGenMapping
{    
    // internal class holds the state for one map
    private class MapState
    {
        public bool showDataLabels = false;

        // The current view bounds (minX, maxX, minY, maxY)
        public float minX = 0f, maxX = 1f;
        public float minY = 0f, maxY = 1f;

        // or dragging data points (left-drag)
        public int draggingPointIndex = -1;

        // right/middle-click pan
        public bool isPanning = false;
        public Vector2 panStartMouse;
        public float panMinX, panMaxX, panMinY, panMaxY;
        public int panButton = -1; // which mouse button started the pan
    }

    // Store each map's state in a dictionary keyed by the map's label.
    // e.g "Engine Map" -> MapState, "Transmission Map" -> MapState.
    private Dictionary<string, MapState> mapStates = new Dictionary<string, MapState>();

    // major grid lines in the graph to attempt to set the resolution
    private float targetGridLineResolution = 8f;

    // get or create a mapstate for the given map label
    private MapState GetMapState(string label)
    {
        if (!mapStates.TryGetValue(label, out MapState ms))
        {
            ms = new MapState();
            mapStates[label] = ms;
        }
        return ms;
    }


    // draw the numeric list of points [x,y] for the given map (left side)
    // Every row has an x and y, plus a remove button and lso an "Add Point" button
    public void DrawPairs(JArray mapArray, string label = "Map")
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        if (mapArray == null || mapArray.Count == 0)
        {
            EditorGUILayout.HelpBox("No data in map.", MessageType.Info);
            return;
        }

        // iterate over each [x,y] pair
        for (int i = 0; i < mapArray.Count; i++)
        {
            if (mapArray[i] is JArray arr && arr.Count == 2)
            {
                float oldX = arr[0].ToObject<float>();
                float oldY = arr[1].ToObject<float>();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{i}] X:", GUILayout.Width(60));
                float newX = EditorGUILayout.FloatField(oldX, GUILayout.Width(80));
                EditorGUILayout.LabelField("Y:", GUILayout.Width(20));
                float newY = EditorGUILayout.FloatField(oldY, GUILayout.Width(80));

                // Remove button for this row
                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    mapArray.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    return; // Early return so we don't process an out-of-range index
                }
                EditorGUILayout.EndHorizontal();

                // if changed values update the JArray
                if (!Mathf.Approximately(newX, oldX) || !Mathf.Approximately(newY, oldY))
                {
                    mapArray[i] = new JArray(newX, newY);
                }
            }
        }
        // Add a new point
        if (GUILayout.Button("Add Point", GUILayout.Width(100)))
        {
            mapArray.Add(new JArray(0f, 0f));
        }
    }

    // Funciton to zoom, pan, line clipping, label collision resolution to avoid overlaps
    public void DrawGraph(JArray mapArray, string label = "Graph")
    {
        // get/create the state for this map
        MapState state = GetMapState(label);

        // toggle for displaying data labels near each point
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        state.showDataLabels = EditorGUILayout.Toggle("Show Data Point Values?", state.showDataLabels);

        if (mapArray == null)
        {
            EditorGUILayout.HelpBox("Map array is null.", MessageType.Warning);
            return;
        }

        // reserve space for the graph
        Rect graphRect = GUILayoutUtility.GetRect(400, 300);
        GUI.Box(graphRect, "");

        // put data points into a list
        List<Vector2> points = new List<Vector2>();
        float dataMinX = float.MaxValue, dataMaxX = float.MinValue;
        float dataMinY = float.MaxValue, dataMaxY = float.MinValue;

        for (int i = 0; i < mapArray.Count; i++)
        {
            if (mapArray[i] is JArray arr && arr.Count == 2)
            {
                float px = arr[0].ToObject<float>();
                float py = arr[1].ToObject<float>();
                points.Add(new Vector2(px, py));

                dataMinX = Mathf.Min(dataMinX, px);
                dataMaxX = Mathf.Max(dataMaxX, px);
                dataMinY = Mathf.Min(dataMinY, py);
                dataMaxY = Mathf.Max(dataMaxY, py);
            }
        }

        // If no points, display help text and allow double-click insert / instructions
        if (points.Count == 0)
        {
            GUI.Label(graphRect,
                "No data. Double-click LMB => add point.\n" +
                "Double-click RMB => auto-scale.\n" +
                "RMB => pan, Wheel => zoom.",
                new GUIStyle { alignment = TextAnchor.MiddleCenter });

            // Let user add points with double-click
            HandleGraphEvents(graphRect, mapArray, 0f, 1f, 0f, 1f, label, points);
            return;
        }

        // make some data-based padding so points aren't on the extreme edges
        if (dataMinX > dataMaxX) (dataMinX, dataMaxX) = (0, 1);
        if (dataMinY > dataMaxY) (dataMinY, dataMaxY) = (0, 1);
        float dx = Mathf.Max(1e-3f, dataMaxX - dataMinX);
        float dy = Mathf.Max(1e-3f, dataMaxY - dataMinY);
        float padFactor = 0.1f;
        float dataViewMinX = dataMinX - dx * padFactor;
        float dataViewMaxX = dataMaxX + dx * padFactor;
        float dataViewMinY = dataMinY - dy * padFactor;
        float dataViewMaxY = dataMaxY + dy * padFactor;

        // set the initial view to the data bounding box
        if (Mathf.Approximately(state.maxX, 1f) && Mathf.Approximately(state.minX, 0f)
            && Mathf.Approximately(state.maxY, 1f) && Mathf.Approximately(state.minY, 0f))
        {
            state.minX = dataViewMinX;
            state.maxX = dataViewMaxX;
            state.minY = dataViewMinY;
            state.maxY = dataViewMaxY;
        }

        // Zoom + auto-scale (mouse wheel / double-click RMB)
        HandleMouseWheelZoomAndReset(
            graphRect,
            ref state.minX, ref state.maxX,
            ref state.minY, ref state.maxY,
            dataViewMinX, dataViewMaxX,
            dataViewMinY, dataViewMaxY
        );

        // pan (right/middle click & drag)
        HandlePan(
            state,
            graphRect,
            ref state.minX, ref state.maxX,
            ref state.minY, ref state.maxY
        );

        // If we're repainting, draw the lines, grid, labels, and so on
        if (Event.current.type == EventType.Repaint)
        {
            // draw grid lines
            Handles.color = new Color(1f, 1f, 1f, 0.1f);
            DrawGridLines(graphRect, state.minX, state.maxX, state.minY, state.maxY);

            // axes at zero
            Handles.color = Color.gray;
            if (0 >= state.minY && 0 <= state.maxY)
            {
                Vector2 xAxisA = WorldToGraphPos(new Vector2(state.minX, 0), graphRect, state.minX, state.maxX, state.minY, state.maxY);
                Vector2 xAxisB = WorldToGraphPos(new Vector2(state.maxX, 0), graphRect, state.minX, state.maxX, state.minY, state.maxY);
                Handles.DrawLine(xAxisA, xAxisB);
            }
            if (0 >= state.minX && 0 <= state.maxX)
            {
                Vector2 yAxisA = WorldToGraphPos(new Vector2(0, state.minY), graphRect, state.minX, state.maxX, state.minY, state.maxY);
                Vector2 yAxisB = WorldToGraphPos(new Vector2(0, state.maxY), graphRect, state.minX, state.maxX, state.minY, state.maxY);
                Handles.DrawLine(yAxisA, yAxisB);
            }

            // lines connecting the data points (including bounding-box clipping)
            Handles.color = Color.green;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                if (ClipLineSegment(ref p1, ref p2, state.minX, state.maxX, state.minY, state.maxY))
                {
                    Vector2 sp1 = WorldToGraphPos(p1, graphRect, state.minX, state.maxX, state.minY, state.maxY);
                    Vector2 sp2 = WorldToGraphPos(p2, graphRect, state.minX, state.maxX, state.minY, state.maxY);
                    Handles.DrawLine(sp1, sp2);
                }
            }

            // If data labels ticked then place & draw them
            if (state.showDataLabels)
            {
                var placed = PlaceLabelsNearPoints(points, graphRect, state.minX, state.maxX, state.minY, state.maxY);
                RelaxLabelPositions(placed, graphRect, 40);
                DrawAllLabels(placed);
            }

            // points drawn as solid discs
            DrawAllPoints(points, graphRect, state.minX, state.maxX, state.minY, state.maxY);
        }

        // finally, let user drag points or insert new ones
        HandleGraphEvents(graphRect, mapArray, state.minX, state.maxX, state.minY, state.maxY, label, points);
    }

    // ---------------------------------------------------------------------------------------------------------
    // EVENT HANDLERS: MOUSEWHEEL ZOOM, PAN, DRAG POINTS, DOUBLE-CLICK ADD
    // ----------------------------------------------------------------------------------------------------------
    private void HandleMouseWheelZoomAndReset(
        Rect graphRect,
        ref float viewMinX, ref float viewMaxX,
        ref float viewMinY, ref float viewMaxY,
        float dataViewMinX, float dataViewMaxX,
        float dataViewMinY, float dataViewMaxY
    )
    {
        Event e = Event.current;
        if (!graphRect.Contains(e.mousePosition)) return;

        // Mouse wheel => zoom
        if (e.type == EventType.ScrollWheel)
        {
            float zf = 1f - e.delta.y * 0.03f;
            if (zf < 0.01f) zf = 0.01f;

            // zoom about the center of the current view
            float cx = (viewMinX + viewMaxX) * 0.5f;
            float cy = (viewMinY + viewMaxY) * 0.5f;
            float hw = (viewMaxX - viewMinX) * 0.5f * zf;
            float hh = (viewMaxY - viewMinY) * 0.5f * zf;

            viewMinX = cx - hw;
            viewMaxX = cx + hw;
            viewMinY = cy - hh;
            viewMaxY = cy + hh;

            e.Use();
        }

        // double-click right => auto-scale to data bounds
        if (e.type == EventType.MouseDown && e.button == 1 && e.clickCount == 2)
        {
            viewMinX = dataViewMinX;
            viewMaxX = dataViewMaxX;
            viewMinY = dataViewMinY;
            viewMaxY = dataViewMaxY;
            e.Use();
        }
    }

    private void HandlePan(
        MapState state,
        Rect graphRect,
        ref float minX, ref float maxX,
        ref float minY, ref float maxY
    )
    {
        Event e = Event.current;
        if (!graphRect.Contains(e.mousePosition)) return;

        // right or middle click => begin panning -> allows for both options
        // since some would be used to one way, but others a different way
        if (e.type == EventType.MouseDown && (e.button == 1 || e.button == 2))
        {
            state.isPanning = true;
            state.panStartMouse = e.mousePosition;
            state.panMinX = minX;
            state.panMaxX = maxX;
            state.panMinY = minY;
            state.panMaxY = maxY;
            state.panButton = e.button;
            e.Use();
        }
        // mouseDrag => update panning
        else if (e.type == EventType.MouseDrag && state.isPanning && e.button == state.panButton)
        {
            Vector2 delta = e.mousePosition - state.panStartMouse;
            float vw = (state.panMaxX - state.panMinX);
            float vh = (state.panMaxY - state.panMinY);

            float dxFrac = -delta.x / graphRect.width;
            float dyFrac = delta.y / graphRect.height;

            minX = state.panMinX + dxFrac * vw;
            maxX = state.panMaxX + dxFrac * vw;
            minY = state.panMinY + dyFrac * vh;
            maxY = state.panMaxY + dyFrac * vh;

            e.Use();
        }
        // if mouseUp, stop panning
        else if (e.type == EventType.MouseUp && state.isPanning && e.button == state.panButton)
        {
            state.isPanning = false;
            state.panButton = -1;
            e.Use();
        }
    }

    private void HandleGraphEvents(
        Rect graphRect,
        JArray mapArray,
        float minX, float maxX,
        float minY, float maxY,
        string label,
        List<Vector2> points
    )
    {
        // retrieve the map state so we know if the user is dragging a point
        MapState state = GetMapState(label);

        Event e = Event.current;
        if (!graphRect.Contains(e.mousePosition)) return;

        Vector2 mousePos = e.mousePosition;
        Vector2 worldMouse = GraphPosToWorld(mousePos, graphRect, minX, maxX, minY, maxY);

        // Left-click down picks nearest data point to drag
        if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 1)
        {
            float pickDist = 10f; // picking radius in screen coords
            float bestDist = pickDist;
            int bestIndex = -1;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 sp = WorldToGraphPos(points[i], graphRect, minX, maxX, minY, maxY);
                float dist = Vector2.Distance(sp, mousePos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }
            if (bestIndex >= 0)
            {
                state.draggingPointIndex = bestIndex;
                e.Use();
            }
        }
        // left-drag moves the chosen data point
        else if (e.type == EventType.MouseDrag && e.button == 0 && state.draggingPointIndex >= 0)
        {
            mapArray[state.draggingPointIndex] = new JArray(worldMouse.x, worldMouse.y);
            e.Use();
        }
        // left-up finishes dragging
        else if (e.type == EventType.MouseUp && e.button == 0 && state.draggingPointIndex >= 0)
        {
            state.draggingPointIndex = -1;
            e.Use();
        }
        // double left click inserts a new point in sorted order (order is sorted by x only)
        else if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2)
        {
            InsertPointInOrder(mapArray, worldMouse);
            e.Use();
        }
    }

    // Insert a new point [newPoint.x, newPoint.y] into the map array,
    // sorted by x which should be robust enough for most of chrono mappping
    private void InsertPointInOrder(JArray mapArray, Vector2 newPoint)
    {
        float newX = newPoint.x;
        int idx = 0;
        for (; idx < mapArray.Count; idx++)
        {
            if (mapArray[idx] is JArray arr && arr.Count == 2)
            {
                float px = arr[0].ToObject<float>();
                if (px > newX) break;
            }
        }
        mapArray.Insert(idx, new JArray(newPoint.x, newPoint.y));
    }

    // -----------------------------------------------------------------------
    // GRID LINES SECTION
    // -----------------------------------------------------------------------

    // Draw grid lines with attempted auto calc step sizes for X and Y aiming for
    // about 10 lines on each axis, even if minX..maxX is tiny, while minY..maxY is huge, or vice versa
    private void DrawGridLines(
        Rect graphRect,
        float minX, float maxX,
        float minY, float maxY
    )
    {
        // determine a grid step unit for X and Y
        float gridStepX = CalculateGridStep(minX, maxX);
        float gridStepY = CalculateGridStep(minY, maxY);

        // vrtical lines
        float startX = Mathf.Floor(minX / gridStepX) * gridStepX;
        for (float gx = startX; gx <= maxX; gx += gridStepX)
        {
            if (gx < minX) continue;

            // draw a vertical line
            Vector2 worldStart = new Vector2(gx, minY);
            Vector2 worldEnd = new Vector2(gx, maxY);

            Vector2 screenStart = WorldToGraphPos(worldStart, graphRect, minX, maxX, minY, maxY);
            Vector2 screenEnd = WorldToGraphPos(worldEnd, graphRect, minX, maxX, minY, maxY);
            Handles.DrawLine(screenStart, screenEnd);

            // label near the bottom
            Vector2 labelPos = WorldToGraphPos(new Vector2(gx, minY), graphRect, minX, maxX, minY, maxY);
            Vector2 offset = new Vector2(4, 2);
            Vector2 finalLabel = ClampLabelPosition(labelPos + offset, graphRect);

            // set decimal precision
            string labelText = FormatTickValue(gx, gridStepX);
            Handles.Label(finalLabel, labelText);
        }

        // horizontal lines with the calculated gridStepY
        float startY = Mathf.Floor(minY / gridStepY) * gridStepY;
        for (float gy = startY; gy <= maxY; gy += gridStepY)
        {
            if (gy < minY) continue;

            // draw a horizontal line
            Vector2 worldStart = new Vector2(minX, gy);
            Vector2 worldEnd = new Vector2(maxX, gy);

            Vector2 screenStart = WorldToGraphPos(worldStart, graphRect, minX, maxX, minY, maxY);
            Vector2 screenEnd = WorldToGraphPos(worldEnd, graphRect, minX, maxX, minY, maxY);
            Handles.DrawLine(screenStart, screenEnd);

            // put the label near the left
            Vector2 labelPos = WorldToGraphPos(new Vector2(minX, gy), graphRect, minX, maxX, minY, maxY);
            Vector2 offset = new Vector2(4, -14);
            Vector2 finalLabel = ClampLabelPosition(labelPos + offset, graphRect);

            // decimal units formatting
            string labelText = FormatTickValue(gy, gridStepY);
            Handles.Label(finalLabel, labelText);
        }
    }

    // calculate the step size for ~10 grid lines in the given min..max range
    private float CalculateGridStep(float minVal, float maxVal)
    {
        float range = Mathf.Abs(maxVal - minVal);
        // If extremely small, fallback
        if (range < 1e-9f)
            return 0.1f;

        // set the number of lines by the target res spec'd above
        float targetLines = targetGridLineResolution;
        float rawStep = range / targetLines;

        // get 10^N scale
        float magnitude = Mathf.Pow(10, Mathf.FloorToInt(Mathf.Log10(rawStep)));
        float leadingDigit = rawStep / magnitude;  // ~ [1..10)

        // pick from 1, 2, 2.5, or 5 scaling
        float step;
        if (leadingDigit < 2f)
            step = 1f * magnitude;
        else if (leadingDigit < 2.5f)
            step = 2f * magnitude;
        else if (leadingDigit < 5f)
            step = 2.5f * magnitude;
        else
            step = 5f * magnitude;

        return step;
    }


    // chooses an appropriate decimal precision for a given axis 'step'
    // and returns the number as a string with that precision
    private string FormatTickValue(float val, float step)
    {
        float stepAbs = Mathf.Abs(step);
        if (stepAbs < 1e-12f)
        {
            // fallback if step is extremely small
            return val.ToString("F2");
        }

        // compute how many decimals to show based on the log10 of step
        float logScale = -Mathf.Log10(stepAbs);
        int decimals = Mathf.CeilToInt(logScale) + 1;
        if (decimals < 0) decimals = 0;   // no negative
        if (decimals > 8) decimals = 6;   // limit to something reasonable

        if (stepAbs >= 1f && decimals < 1)
            decimals = 0;

        // convert the value to a string with that many decimals
        return val.ToString("F" + decimals);
    }


    // clamps the label position so it stays inside the graph rect
    private Vector2 ClampLabelPosition(Vector2 labelPos, Rect graphRect)
    {
        float clampedX = Mathf.Clamp(labelPos.x, graphRect.x, graphRect.xMax);
        float clampedY = Mathf.Clamp(labelPos.y, graphRect.y, graphRect.yMax);
        return new Vector2(clampedX, clampedY);
    }

    // -----------------------------------------------------------------------
    // LABEL PLACEMENT & COLLISION AVOIDANCE
    // -----------------------------------------------------------------------
    private class PlacedLabel
    {
        public Vector2 pointWorld;  // "world" coords
        public Vector2 pointScreen; // "screen" coords
        public Rect rect;           // BB in screen coords
        public string text;         // label text
    }

    private List<PlacedLabel> PlaceLabelsNearPoints(
        List<Vector2> points,
        Rect graphRect,
        float viewMinX, float viewMaxX,
        float viewMinY, float viewMaxY
    )
    {
        var result = new List<PlacedLabel>();

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 wP = points[i];
            // Skip out-of-range
            if (wP.x < viewMinX || wP.x > viewMaxX || wP.y < viewMinY || wP.y > viewMaxY)
                continue;

            Vector2 sP = WorldToGraphPos(wP, graphRect, viewMinX, viewMaxX, viewMinY, viewMaxY);
            string lbl = $"[{wP.x:F0},{wP.y:F0}]";

            Vector2 size = EditorStyles.label.CalcSize(new GUIContent(lbl));
            Vector2 offset = new Vector2(6, -8);

            float rx = sP.x + offset.x;
            float ry = sP.y + offset.y;
            Rect r = new Rect(rx, ry, size.x, size.y);

            // clamp to graph rect
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

    // multipass to push overlapping labels away from each other, and reclamp so they stay inside the graph
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
                        // push them away from each other
                        Vector2 ci = new Vector2(ri.x + ri.width * 0.5f, ri.y + ri.height * 0.5f);
                        Vector2 cj = new Vector2(rj.x + rj.width * 0.5f, rj.y + rj.height * 0.5f);
                        Vector2 diff = cj - ci;
                        if (diff.sqrMagnitude < 0.001f)
                            diff = new Vector2(0.1f, -0.1f);

                        diff.Normalize();
                        diff *= 2f; // push distance

                        // push j in one direction
                        rj.x += diff.x;
                        rj.y += diff.y;
                        // push i in the opposite
                        ri.x -= diff.x;
                        ri.y -= diff.y;

                        // clamp them again
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

    // draw the final labels and leader lines
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

    // Draw every data point as a small solid circle with the colour green
    private void DrawAllPoints(
        List<Vector2> points,
        Rect graphRect,
        float minX, float maxX,
        float minY, float maxY
    )
    {
        Handles.color = Color.green;
        foreach (var point in points)
        {
            if (point.x >= minX && point.x <= maxX && point.y >= minY && point.y <= maxY)
            {
                Vector2 screenPos = WorldToGraphPos(point, graphRect, minX, maxX, minY, maxY);
                Handles.DrawSolidDisc(screenPos, Vector3.forward, 2.5f);
            }
        }
    }

    // cut off lines going beyond the rect
    private bool ClipLineSegment(
        ref Vector2 p1, ref Vector2 p2,
        float minX, float maxX,
        float minY, float maxY
    )
    {
        // a clip test implementation of Liang-Barsky style line clipping
        Vector2 d = p2 - p1;
        float t0 = 0f, t1 = 1f;
        if (ClipTest(-d.x, p1.x - minX, ref t0, ref t1) &&
            ClipTest(d.x, maxX - p1.x, ref t0, ref t1) &&
            ClipTest(-d.y, p1.y - minY, ref t0, ref t1) &&
            ClipTest(d.y, maxY - p1.y, ref t0, ref t1))
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
            // if the line is parallel to one of the clipping boundaries
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

    // transition between "world" coords (i.e. real point data space) to UI screen coords in the graph rect
    private Vector2 WorldToGraphPos(Vector2 world, Rect rect, float minX, float maxX, float minY, float maxY)
    {
        float dx = (world.x - minX) / (maxX - minX);
        float dy = (world.y - minY) / (maxY - minY);

        float px = rect.x + dx * rect.width;
        float py = rect.yMax - dy * rect.height; // note y is flipped
        return new Vector2(px, py);
    }

    // convert from screen coords in the rect to data "coords"
    private Vector2 GraphPosToWorld(Vector2 graphPos, Rect rect, float minX, float maxX, float minY, float maxY)
    {
        float dx = (graphPos.x - rect.x) / rect.width;
        float dy = (rect.yMax - graphPos.y) / rect.height;

        float wx = minX + dx * (maxX - minX);
        float wy = minY + dy * (maxY - minY);
        return new Vector2(wx, wy);
    }


    // clear all per-map states (zoom, toggles, etc.) from memory (avoid things hanging in mem)
    public void ClearAllMapStates()
    {
        mapStates.Clear();
    }
}
