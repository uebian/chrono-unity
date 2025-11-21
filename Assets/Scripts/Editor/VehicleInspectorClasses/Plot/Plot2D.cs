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
// Main 2D plotting class - oversees grid, axes, series, and interactions
// =============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace ChronoVehicleBuilder.Plotting
{
    public class Plot2D
    {
        // Components
        private PlotGrid grid;
        private PlotAxes axes;
        private List<PlotSeries> seriesList;
        private PlotViewport viewport;
        private PlotInteraction interaction;
        
        // Configuration
        public string Title { get; set; }
        public float Width { get; set; } = 0f;
        public float Height { get; set; } = 300f;
        private float plotMinWidth = 150f;
        
        // Display options
        public bool ShowGrid { get; set; } = true;
        public bool ShowLabels { get; set; } = false;
        public bool ShowLegend { get; set; } = false;
        
        public Plot2D(string title = "Plot")
        {
            Title = title;
            seriesList = new List<PlotSeries>();
            viewport = new PlotViewport();
            grid = new PlotGrid();
            axes = new PlotAxes();
            interaction = new PlotInteraction(viewport);
        }
        
        /// <summary>
        /// Add a data series to the plot
        /// </summary>
        public PlotSeries AddSeries(string name, List<Vector2> points, Color color)
        {
            var series = new PlotSeries(name, points, color);
            seriesList.Add(series);
            return series;
        }
        
        /// <summary>
        /// Remove all data series
        /// </summary>
        public void ClearSeries()
        {
            seriesList.Clear();
        }
        
        /// <summary>
        /// Fit viewport to show all data
        /// </summary>
        public void FitToData(float padding = 0.1f)
        {
            if (seriesList.Count == 0) return;
            
            Bounds2D bounds = CalculateDataBounds();
            viewport.FitToBounds(bounds, padding);
        }
        
        /// <summary>
        /// Draw the plot
        /// </summary>
        public void Draw()
        {
            DrawHeader();

            const float baseHorizontalMargin = 40f;
            const float bottomMargin = 28f;

            // Get actual available inspector width
            float maxInspectorWidth = EditorGUIUtility.currentViewWidth - 32f; // Account for inspector padding/scroll
            
            var layoutOptions = new List<GUILayoutOption>
            {
                GUILayout.Height(Height + bottomMargin),
                GUILayout.ExpandWidth(true),
                GUILayout.MaxWidth(maxInspectorWidth)
            };

            if (Width > 0f)
            {
                float targetWidth = Mathf.Max(Width, plotMinWidth) + (baseHorizontalMargin * 2f);
                // Use the smaller of requested width or available inspector width
                layoutOptions.RemoveAt(layoutOptions.Count - 1); // Remove the max width we just added
                layoutOptions.Add(GUILayout.MaxWidth(Mathf.Min(targetWidth, maxInspectorWidth)));
            }

            Rect totalRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, layoutOptions.ToArray());
            totalRect = EditorGUI.IndentedRect(totalRect);

            float availableWidth = Mathf.Max(0f, totalRect.width);
            
            // Calculate graph width and margins - eliminate margins if space is too tight (important!)
            float graphWidth;
            float horizontalMargin;
            
            float idealWidthWithMargins = plotMinWidth + (baseHorizontalMargin * 2f);
            
            if (availableWidth >= idealWidthWithMargins)
            {
                // Plenty of space - use margins
                graphWidth = Mathf.Max(plotMinWidth, availableWidth - (baseHorizontalMargin * 2f));
                horizontalMargin = baseHorizontalMargin;
            }
            else
            {
                // Too narrow - use full width, no margins
                graphWidth = availableWidth;
                horizontalMargin = 0f;
            }
            
            float graphHeight = Mathf.Max(120f, totalRect.height - bottomMargin);
            Rect graphRect = new Rect(totalRect.x + horizontalMargin, totalRect.y, graphWidth, graphHeight);

            EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f));
            GUI.Box(graphRect, "", EditorStyles.helpBox);

            // Claim a passive control and cursor so Unity consistently sends mouse move events for hover logic
            int plotControlId = GUIUtility.GetControlID("ChronoPlot2D".GetHashCode(), FocusType.Passive, graphRect);
            if (Event.current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(plotControlId);
            }
            EditorGUIUtility.AddCursorRect(graphRect, MouseCursor.Arrow);
            
            // Unity inspectors don't send MouseMove events by default so we need to request repaints
            // Only call Repaint during Repaint event to avoid "called outside OnGUI" errors!!!!
            if (Event.current.type == EventType.Repaint)
            {
                if (graphRect.Contains(Event.current.mousePosition))
                {
                    // Request repaint on next frame to keep getting mouse position updates
                    HandleUtility.Repaint();
                }
            }
            
            if (seriesList.Count == 0)
            {
                DrawEmptyState(graphRect);
                return;
            }
            
            // Auto-fit on first draw
            if (!viewport.IsInitialized)
            {
                FitToData();
            }
            
            // Handle series editing FIRST (before pan/zoom so left-clicks go to points)
            foreach (var series in seriesList)
            {
                if (series.IsEditable)
                {
                    series.HandleEdit(graphRect, viewport);
                }
            }
            
            // Handle user interactions (zoom, pan) - after point editing
            interaction.HandleInput(graphRect, CalculateDataBounds());
            
            // Draw during repaint
            if (Event.current.type == EventType.Repaint)
            {
                // Always draw grid
                grid.Draw(graphRect, viewport);
                
                axes.Draw(graphRect, viewport);
                
                foreach (var series in seriesList)
                {
                    series.ShowLabels = ShowLabels;
                    series.Draw(graphRect, viewport);
                }
            }
            
            DrawFooter();
        }
        
        private void DrawHeader()
        {
            float inspectorWidth = EditorGUIUtility.currentViewWidth - 32f;
            
            EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(inspectorWidth));
            
            // Calculate available width for title after accounting for controls
            float controlsWidth = 60f + 8f + 45f; // Labels + space + Fit (no Grid checkbox)
            float pointCountWidth = seriesList.Count > 0 ? 58f : 0f; // "Pts: N" + space
            float minTitleWidth = 30f;
            float maxTitleWidth = Mathf.Max(minTitleWidth, inspectorWidth - controlsWidth - pointCountWidth - 20f);
            
            // Title - clamp to available width
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                clipping = TextClipping.Clip
            };
            
            if (maxTitleWidth >= minTitleWidth)
            {
                EditorGUILayout.LabelField(Title, titleStyle, GUILayout.MinWidth(minTitleWidth), GUILayout.MaxWidth(maxTitleWidth));
            }
            
            // Point count (if exists and space available)
            if (seriesList.Count > 0 && inspectorWidth > 200f)
            {
                GUILayout.Space(8f);
                EditorGUILayout.LabelField($"Pts: {GetTotalPointCount()}", EditorStyles.miniLabel, GUILayout.Width(50f));
            }
            
            GUILayout.FlexibleSpace();
            
            // Controls on the right - hide when too narrow
            if (inspectorWidth > 180f)
            {
                ShowLabels = GUILayout.Toggle(ShowLabels, "Data Labels", GUILayout.Width(45f));
                GUILayout.Space(6f);
            }
            
            if (inspectorWidth > 120f)
            {
                if (GUILayout.Button("Fit", GUILayout.Width(40f)))
                {
                    viewport.Reset();
                    FitToData();
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawFooter()
        {
            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField(
                "Click+Drag: Move point  •  Double-click: Add point  •  Del: Remove  •  Wheel: Zoom  •  Right-drag: Pan",
                EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(5);
        }
        
        private void DrawEmptyState(Rect graphRect)
        {
            GUIStyle emptyStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
            
            GUI.Label(graphRect,
                "No data\n\n" +
                "Add series with Plot2D.AddSeries()\n" +
                "Mouse wheel: zoom  •  Right-drag: pan",
                emptyStyle);
        }
        
        private Bounds2D CalculateDataBounds()
        {
            Bounds2D bounds = new Bounds2D();
            
            foreach (var series in seriesList)
            {
                bounds.Encapsulate(series.GetBounds());
            }
            
            return bounds;
        }
        
        private int GetTotalPointCount()
        {
            int count = 0;
            foreach (var series in seriesList)
            {
                count += series.PointCount;
            }
            return count;
        }
        
        /// <summary>
        /// Get viewport for external manipulation
        /// </summary>
        public PlotViewport GetViewport() => viewport;
        
        /// <summary>
        /// Get grid for configuration
        /// </summary>
        public PlotGrid GetGrid() => grid;
        
        /// <summary>
        /// Get axes for configuration
        /// </summary>
        public PlotAxes GetAxes() => axes;
        
        /// <summary>
        /// Get number of series in plot
        /// </summary>
        public int GetSeriesCount() => seriesList?.Count ?? 0;
        
        /// <summary>
        /// Get series by index
        /// </summary>
        public PlotSeries GetSeries(int index)
        {
            if (seriesList == null || index < 0 || index >= seriesList.Count)
                return null;
            return seriesList[index];
        }
        
        /// <summary>
        /// Get points from series by index
        /// </summary>
        public List<Vector2> GetSeriesPoints(int index)
        {
            var series = GetSeries(index);
            return series?.Points;
        }
    }
}
#endif
