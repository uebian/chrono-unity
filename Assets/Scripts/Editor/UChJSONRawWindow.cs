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
// This code is a Unity EditorWindow that renders JSON text lines with rich
// text coloring, displays line numbers, a toolbar, and handles keyboard
// & mouse events. The actual text manipulation, bracket matching, etc happens
// in UChRichTextEditorCore. 
// =============================================================================

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

public class UChJSONRawWindow : EditorWindow
{
    private UChRichTextEditorCore m_core; // The underlying core json logic
    // Position of the scroll view
    private Vector2 m_scrollPos;
    private Vector2 oldScrollPos;
    // store the rect to reference in mouse calcs
    private Rect m_contentRect;

    // Styles for rendering text and line numbers
    private GUIStyle m_textStyle;
    private GUIStyle m_lineNumStyle;
    private float m_lineHeight = 16f;

    // Whether to show indentation dotted lines
    private bool m_showIndentDots = true;

    // Variables for caret blinking
    private double m_lastBlinkTime;
    private float m_blinkInterval = 0.7f;
    private bool m_caretVisible = true;

    // OnEnable / OnDisable
    private void OnEnable()
    {
        // create caret
        if (m_core == null)
            m_core = new UChRichTextEditorCore();

        // Setup GUIStyle for syntax highlighting
        m_textStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true,
            wordWrap = false,
            fontSize = 14
        };
        if (EditorStyles.textArea.font != null)
            m_textStyle.font = EditorStyles.textArea.font;

        // style for line numbers in a smaller font, aligned to the right
        m_lineNumStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperRight,
            fontSize = 11
        };

        // line height setting
        m_lineHeight = m_textStyle.lineHeight + 3f;

        // subscribe to update events for caret blinking
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        // unsubscribe from updates when window is disabled
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        // blinking caret
        double t = EditorApplication.timeSinceStartup;
        if (t - m_lastBlinkTime > m_blinkInterval)
        {
            m_lastBlinkTime = t;
            m_caretVisible = !m_caretVisible;
            Repaint(); // Redraw the window to show/hide the caret
        }
    }

    // Set up initial JSON
    public void InitializeForNewSubcomponent(
        string subsystemType,
        string chronoVehicleDataRoot,
        string templateName
    )
    {
        if (m_core == null)
            m_core = new UChRichTextEditorCore();

        JObject def = UChVehGenJSONUtils.GetDefaultSubcomponentJson(subsystemType, templateName);
        if (def == null)
        {
            def = new JObject
            {
                ["Name"] = $"New {subsystemType}",
                ["Type"] = subsystemType
            };
        }

        string loadedJson = def.ToString(Newtonsoft.Json.Formatting.Indented);
        loadedJson = loadedJson.Replace("\r\n", "\n").Replace("\r", "");
        loadedJson = m_core.InlineNumericArrays(loadedJson);

        m_core.InitializeWithJson(subsystemType, chronoVehicleDataRoot, loadedJson);
    }

    // If we'rve already got a JSON to kick this window off with
    public void InitializeWithDirectJson(string subsystemType, string chronoVehicleDataRoot, string rawJson)
    {
        if (m_core == null)
            m_core = new UChRichTextEditorCore();

        rawJson = rawJson.Replace("\r\n", "\n").Replace("\r", "");
        rawJson = m_core.InlineNumericArrays(rawJson);

        // Initialise the core with the given raw JSON
        m_core.InitializeWithJson(subsystemType, chronoVehicleDataRoot, rawJson);
    }


    [MenuItem("Window/Example/UChJSONRawWindow")]
    public static void OpenWindow()
    {
        GetWindow<UChJSONRawWindow>("JSON Raw Editor");
    }

    private void OnGUI()
    {
        // toolbar at top
        DrawToolbar();

        // label showing the current subcomponent type from m_core
        EditorGUILayout.LabelField($"Raw JSON Editor - {m_core.SubcomponentType}", EditorStyles.boldLabel);

        // info/error message about JSON validity
        if (string.IsNullOrEmpty(m_core.ParseError))
            EditorGUILayout.HelpBox("JSON appears valid.", MessageType.Info);
        else
            EditorGUILayout.HelpBox("JSON parse error:\n" + m_core.ParseError, MessageType.Error);

        // track old scroll (so we can detect if there was a scroll mid-drag)
        oldScrollPos = m_scrollPos;

        // main scroll content area
        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos, GUILayout.ExpandHeight(true));
        {
            // keyboard input
            HandleKeyboard();

            // Grab all lines of text from the core
            string[] lines = m_core.GetLines();

            // Calculate the total height needed to display all lines
            float contentHeight = lines.Length * m_lineHeight;
            // Get a rect of that height within the scroll view
            m_contentRect = EditorGUILayout.GetControlRect(false, contentHeight);

            // Render each line
            for (int i = 0; i < lines.Length; i++)
            {
                // position each line
                Rect lineRect = new Rect(
                    m_contentRect.x,
                    m_contentRect.y + i * m_lineHeight,
                    m_contentRect.width,
                    m_lineHeight
                );

                // line number area
                Rect numRect = new Rect(lineRect.x, lineRect.y, 40, m_lineHeight);
                GUI.Label(numRect, (i + 1).ToString("D3"), m_lineNumStyle);

                // indent dotted lines
                if (m_showIndentDots)
                {
                    float leftX = numRect.xMax + 5;
                    float yPos = lineRect.y;
                    DrawIndentDottedLines(leftX, yPos, lines[i], m_lineHeight);
                }

                Rect txtRect = new Rect(
                    numRect.xMax + 5,
                    lineRect.y,
                    lineRect.width - 45,
                    m_lineHeight
                );

                // highlight if in selection
                if (m_core.HasSelection && m_core.LineInSelectionRange(i))
                {
                    Color old = GUI.color;
                    GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                    GUI.Box(txtRect, GUIContent.none);
                    GUI.color = old;
                }

                // colourise
                string coloredLine = m_core.ColorizeJSONLine(lines[i], i);
                GUI.Label(txtRect, coloredLine, m_textStyle);

                // caret
                if (m_core.HasFocus && i == m_core.CaretLine && m_caretVisible)
                {
                    DrawCaret(txtRect, lines[i]);
                }
            }

            // after everything's drawn, check the mouse handling
            HandleMouse();
        }
        EditorGUILayout.EndScrollView();

        // If the user scrolled while dragging, re-calc
        if (m_scrollPos != oldScrollPos && m_core.IsDraggingSelection)
        {
            RecalcSelectionAfterScroll(Event.current.mousePosition);
        }

        // save and close buttons int the bottom horizontal layout
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Save JSON As...", GUILayout.Height(28)))
        {
            m_core.SaveJsonAs();
        }

        // close using a delayed call to avoid IMGUI errors
        if (GUILayout.Button("Close", GUILayout.Height(28)))
        {
            EditorApplication.delayCall += () => Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void HandleKeyboard()
    {
        if (!m_core.HasFocus) return;

        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            // standard shortcuts: Ctrl/Cmd + Z/Y/X/C/V
            bool ctrlOrCmd = e.control || e.command;
            if (ctrlOrCmd)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Z:
                        // Override Unity's undo
                        m_core.DoUndo();
                        e.Use(); // consume event so unity doesn't also do an undo
                        return;

                    case KeyCode.Y:
                        m_core.DoRedo();
                        e.Use();
                        return;

                    case KeyCode.X:
                        m_core.DoCut();
                        e.Use();
                        return;

                    case KeyCode.C:
                        m_core.DoCopy();
                        e.Use();
                        return;

                    case KeyCode.V:
                        m_core.DoPaste();
                        e.Use();
                        return;
                }
            }

            // Blink caret reset
            m_caretVisible = true;
            m_lastBlinkTime = EditorApplication.timeSinceStartup;

            // Normal arrow / delete / enter logic
            switch (e.keyCode)
            {
                case KeyCode.UpArrow:
                    if (e.shift)
                    {
                        if (!m_core.HasSelection) m_core.StartLineSelection();
                        m_core.MoveCaretLineUp();
                        m_core.UpdateSelectionEnd();
                    }
                    else
                    {
                        m_core.ClearSelection();
                        m_core.MoveCaretLineUp();
                    }
                    e.Use();
                    break;

                case KeyCode.DownArrow:
                    if (e.shift)
                    {
                        if (!m_core.HasSelection) m_core.StartLineSelection();
                        m_core.MoveCaretLineDown();
                        m_core.UpdateSelectionEnd();
                    }
                    else
                    {
                        m_core.ClearSelection();
                        m_core.MoveCaretLineDown();
                    }
                    e.Use();
                    break;

                case KeyCode.LeftArrow:
                    m_core.ClearSelection();
                    m_core.MoveCaretLeft();
                    e.Use();
                    break;

                case KeyCode.RightArrow:
                    m_core.ClearSelection();
                    m_core.MoveCaretRight();
                    e.Use();
                    break;

                case KeyCode.Backspace:
                    m_core.DoBackspace();
                    e.Use();
                    break;

                case KeyCode.Delete:
                    m_core.DoDelete();
                    e.Use();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    m_core.InsertNewLineWithIndent();
                    e.Use();
                    break;

                default:
                    // typed char
                    char c = e.character;
                    if (c >= ' ' && !char.IsControl(c))
                    {
                        m_core.InsertText(c.ToString());
                        e.Use();
                    }
                    break;
            }
        }
    }

    private void HandleMouse()
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            // focus on left-click
            GUI.FocusControl(null);
            m_core.SetFocus(true);

            m_caretVisible = true;
            m_lastBlinkTime = EditorApplication.timeSinceStartup;

            var (clickedLine, offsetX) = GetLineAndOffsetFromMouse(e.mousePosition);
            int col = m_core.MeasureColumn(clickedLine, offsetX, m_textStyle);

            // position caret at clicked line/column
            m_core.SetCaretLineColumn(clickedLine, col);

            // Clear any old selection, so single click doesn't select the line!!
            m_core.ClearSelection();

            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0)
        {
            if (!m_core.IsDraggingSelection)
            {
                m_core.BeginDragSelection();
            }

            // update selection if dragging is active
            var (dragLine, offsetX) = GetLineAndOffsetFromMouse(e.mousePosition);
            int col = m_core.MeasureColumn(dragLine, offsetX, m_textStyle);

            m_core.SetCaretLineColumn(dragLine, col);
            m_core.UpdateSelectionEnd();

            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && m_core.IsDraggingSelection)
        {
            m_core.EndDragSelection();
            e.Use();
        }
    }

    // Called if scrolling while dragging. Re-check the mouse coords so as not to end up with the pointer out of sync of the display
    private void RecalcSelectionAfterScroll(Vector2 mousePos)
    {
        if (!m_core.IsDraggingSelection) return;

        var (dragLine, offsetX) = GetLineAndOffsetFromMouse(mousePos);
        int col = m_core.MeasureColumn(dragLine, offsetX, m_textStyle);

        m_core.SetCaretLineColumn(dragLine, col);
        m_core.UpdateSelectionEnd();
        Repaint();
    }

    //  measure from m_contentRect.y to get the line index - this is a little hacky, because Unity had some issues lining things up
    private (int lineIndex, float offsetX) GetLineAndOffsetFromMouse(Vector2 mousePos)
    {
        // localY is how far the mouse is from the top of contentRect
        float localY = mousePos.y - m_contentRect.y;

        int line = Mathf.FloorToInt(localY / m_lineHeight);
        string[] lines = m_core.GetLines();
        line = Mathf.Clamp(line, 0, lines.Length - 1);

        // For x offset
        float textStartX = 40f + 5f;

        float localX = mousePos.x - (m_contentRect.x + textStartX);
        if (localX < 0) localX = 0;

        return (line, localX);
    }

    // drawing the caret the correct size
    private void DrawCaret(Rect txtRect, string line)
    {
        int col = m_core.CaretColumn;
        if (col < 0) col = 0;
        if (col > line.Length) col = line.Length;

        string sub = line.Substring(0, col);
        Vector2 sizeSub = m_textStyle.CalcSize(new GUIContent(sub));

        float xCaret = txtRect.x + sizeSub.x;
        float yCaret = txtRect.y;

        Color old = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(xCaret, yCaret, 2f, m_lineHeight), Texture2D.whiteTexture);
        GUI.color = old;
    }

    // indentation dotted lines
    private void DrawIndentDottedLines(float startX, float y, string line, float lineH)
    {
        float currentX = startX;
        Color dotColor = new Color(0.7f, 0.7f, 0.7f, 1.0f);

        int idx = 0;
        while (idx < line.Length)
        {
            if (line[idx] == '\t')
            {
                float tabWidth = m_textStyle.CalcSize(new GUIContent("\t")).x;
                DrawOneIndentDotLine(currentX, y, lineH, dotColor);
                currentX += tabWidth;
                idx++;
            }
            else if (line[idx] == ' ')
            {
                float spaceWidth = m_textStyle.CalcSize(new GUIContent(" ")).x;
                DrawOneIndentDotLine(currentX, y, lineH, dotColor);
                currentX += spaceWidth;
                idx++;
            }
            else break;
        }
        Handles.color = Color.white;
    }

    private void DrawOneIndentDotLine(float x, float y, float lineH, Color dotColor)
    {
        Handles.color = dotColor;
        Handles.DrawDottedLine(new Vector3(x, y + 1, 0),
                               new Vector3(x, y + lineH - 1, 0), 2f);
    }
    
    //////////////
    // toolbar handling and undo/redo, cut/copy/paste/any others..
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Undo", EditorStyles.toolbarButton)) m_core.DoUndo();
        if (GUILayout.Button("Redo", EditorStyles.toolbarButton)) m_core.DoRedo();
        GUILayout.Space(10);

        if (GUILayout.Button("Cut (Line/s)", EditorStyles.toolbarButton)) m_core.DoCut();
        if (GUILayout.Button("Copy (Line/s)", EditorStyles.toolbarButton)) m_core.DoCopy();
        if (GUILayout.Button("Paste (Line/s)", EditorStyles.toolbarButton)) m_core.DoPaste();

        GUILayout.Space(20);

        m_showIndentDots = GUILayout.Toggle(m_showIndentDots, "Show/Hide Indentation", EditorStyles.toolbarButton);

        EditorGUILayout.EndHorizontal();
    }
}
