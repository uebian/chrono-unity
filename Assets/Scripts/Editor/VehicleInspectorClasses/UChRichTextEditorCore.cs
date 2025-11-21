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
// This code is a rich text editor core for JSON, handling caret movement,
// selection, bracket matching, undo/redo, syntax highlighting,
// and JSON validation. This does not handle the window or input interaction
// =============================================================================

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using ChronoVehicleBuilder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class UChRichTextEditorCore
{
    private string m_textBuffer = "";
    // Cached array of lines from m_textBuffer to avoid splitting repeatedly
    private string[] m_cachedLines;
    // Stores the last known text buffer for tracking changes in m_textBuffer
    private string m_lastTextBuffer;
    // Holds any json parse errors from validation
    private string m_parseError = "";
    // string for subsystem type and root path
    private string m_chronoVehicleDataRoot = "";
    private string m_subcomponentType = "";
    // Caret (cursor) and selection management
    private int m_caretLine = 0;
    private int m_caretColumn = 0;
    private bool m_hasFocus = false;
    private bool m_hasSelection = false;
    private bool m_isDraggingSelection = false;
    private int m_selLineStart = 0;
    private int m_selLineEnd = 0;

    // variables for bracket matching
    private Dictionary<int, int> m_bracketPairs = new Dictionary<int, int>();
    private string m_bracketCachedText;
    private int? m_bracketIndexOnLine = null;
    private int? m_matchingBracketAbs = null;

    // undo/redo stacks for text buffer snapshots
    private Stack<string> undoStack = new Stack<string>();
    private Stack<string> redoStack = new Stack<string>();

    // m_subcomponentType (read-only)
    public string SubcomponentType => m_subcomponentType;
    // last parse error encountered
    public string ParseError => m_parseError;
    // whether the editor currently has focus
    public bool HasFocus => m_hasFocus;
    // whether a selection exists
    public bool HasSelection => m_hasSelection;
    // bool for if user is dragging a selection
    public bool IsDraggingSelection => m_isDraggingSelection;
    // caret line position
    public int CaretLine => m_caretLine;
    // caret column position
    public int CaretColumn => m_caretColumn;

    // There's two types of intialisation - with a json, or not - the direct one is in uchjsonrawwindow.
    // With a json sets up the editor core with a JSON string, a subsystem type, and a data root path
    public void InitializeWithJson(string subsystemType, string chronoVehicleDataRoot, string rawJson)
    {
        m_subcomponentType = subsystemType;
        m_chronoVehicleDataRoot = chronoVehicleDataRoot;

        m_textBuffer = rawJson;
        ValidateJson();
        PushUndo(m_textBuffer);    // Push the initial buffer onto the undo stack as the starting point

        // Reset caret position and selection
        m_caretLine = 0;
        m_caretColumn = 0;
        m_hasSelection = false;
    }

    // Splits the text buffer into lines if it has changed since last time
    public string[] GetLines()
    {
        if (m_textBuffer != m_lastTextBuffer)
        {
            m_lastTextBuffer = m_textBuffer;
            m_cachedLines = m_textBuffer.Split('\n');
        }
        return m_cachedLines;
    }

    public void SetFocus(bool focus)
    {
        m_hasFocus = focus;
    }

    // Clears any current selection
    public void ClearSelection()
    {
        m_hasSelection = false;
    }

    // Begins a selection from the current caret line
    public void StartLineSelection()
    {
        m_hasSelection = true;
        m_selLineStart = m_caretLine;
        m_selLineEnd = m_caretLine;
    }

    // Updates the selection end line to the current caret line
    public void UpdateSelectionEnd()
    {
        m_selLineEnd = m_caretLine;
    }

    // Marks that the user has begun dragging a selection
    public void BeginDragSelection()
    {
        m_isDraggingSelection = true;
        StartLineSelection();
    }

    // Marks that the user has finished dragging a selection
    public void EndDragSelection()
    {
        m_isDraggingSelection = false;
    }

    // Checks if a specific line index is within the current selection range
    public bool LineInSelectionRange(int line)
    {
        int minL = Mathf.Min(m_selLineStart, m_selLineEnd);
        int maxL = Mathf.Max(m_selLineStart, m_selLineEnd);
        return (line >= minL && line <= maxL);
    }

    // Move caret up/down
    public void MoveCaretLineUp()
    {
        string[] lines = GetLines();
        if (m_caretLine > 0)
        {
            m_caretLine--;
            // Adjust caret column if it exceeds the new line length
            if (m_caretColumn > lines[m_caretLine].Length)
                m_caretColumn = lines[m_caretLine].Length;
        }
        UpdateBracketMatching();
    }

    // Moves caret down one line if possible and updates bracket matching
    public void MoveCaretLineDown()
    {
        string[] lines = GetLines();
        if (m_caretLine < lines.Length - 1)
        {
            m_caretLine++;
            // Adjust caret column if it exceeds the new line length
            if (m_caretColumn > lines[m_caretLine].Length)
                m_caretColumn = lines[m_caretLine].Length;
        }
        UpdateBracketMatching();
    }

    // Move caret left/right
    public void MoveCaretLeft()
    {
        string[] lines = GetLines();
        if (m_caretColumn > 0)
        {
            m_caretColumn--;
        }
        else
        {
            if (m_caretLine > 0)
            {
                m_caretLine--;
                m_caretColumn = lines[m_caretLine].Length;
            }
        }
        UpdateBracketMatching();
    }

    public void MoveCaretRight()
    {
        string[] lines = GetLines();
        if (m_caretColumn < lines[m_caretLine].Length)
        {
            m_caretColumn++;
        }
        else
        {
            if (m_caretLine < lines.Length - 1)
            {
                m_caretLine++;
                m_caretColumn = 0;
            }
        }
        UpdateBracketMatching();
    }

    // Set caret position
    public void SetCaretLineColumn(int line, int col)
    {
        string[] lines = GetLines();
        if (line < 0) line = 0;
        if (line >= lines.Length) line = lines.Length - 1;
        m_caretLine = line;

        if (col < 0) col = 0;
        if (col > lines[m_caretLine].Length) col = lines[m_caretLine].Length;
        m_caretColumn = col;

        UpdateBracketMatching();
    }

    // insert text
    public void InsertText(string txt)
    {
        string[] lines = GetLines();
        RemoveSelectedLinesIfAny();
        if (m_caretLine < 0 || m_caretLine >= lines.Length) return;

        string line = lines[m_caretLine];
        if (m_caretColumn > line.Length) m_caretColumn = line.Length;

        string before = line.Substring(0, m_caretColumn);
        string after = line.Substring(m_caretColumn);

        lines[m_caretLine] = before + txt + after;
        m_caretColumn += txt.Length;

        m_textBuffer = string.Join("\n", lines);
        ValidateJson();        // Validate JSON again after modification
        PushUndo(m_textBuffer); // Push state to undo stack
        UpdateBracketMatching();
    }

    // Backspace, Delete Handling
    public void DoBackspace()
    {
        // If there's a line selection, remove those lines entirely and stop
        if (RemoveSelectedLinesIfAny())
        {
            ValidateJson();
            PushUndo(m_textBuffer);
            UpdateBracketMatching();
            return;
        }

        // do the normal single-character logic:
        string[] lines = GetLines();
        if (m_caretLine < 0 || m_caretLine >= lines.Length) return;

        string line = lines[m_caretLine];
        if (m_caretColumn > line.Length) m_caretColumn = line.Length;

        if (m_caretColumn > 0)
        {
            // remove one char
            string before = line.Substring(0, m_caretColumn - 1);
            string after = line.Substring(m_caretColumn);
            lines[m_caretLine] = before + after;
            m_caretColumn--;
        }
        else
        {
            // merge with previous line
            if (m_caretLine > 0)
            {
                int prev = m_caretLine - 1;
                string prevLine = lines[prev];
                m_caretColumn = prevLine.Length;
                lines[prev] = prevLine + line;
                List<string> li = new List<string>(lines);
                li.RemoveAt(m_caretLine);
                m_caretLine--;
                lines = li.ToArray();
            }
        }

        m_textBuffer = string.Join("\n", lines);
        ValidateJson();
        PushUndo(m_textBuffer);
        UpdateBracketMatching();
    }

    // Handles delete key, deleting the character under the caret or merging lines
    public void DoDelete()
    {
        // If there's a line selection, remove all those lines
        if (RemoveSelectedLinesIfAny())
        {
            ValidateJson();
            PushUndo(m_textBuffer);
            UpdateBracketMatching();
            return;
        }

        // single character delete:
        string[] lines = GetLines();
        if (m_caretLine < 0 || m_caretLine >= lines.Length) return;

        string line = lines[m_caretLine];
        if (m_caretColumn > line.Length) m_caretColumn = line.Length;

        if (m_caretColumn < line.Length)
        {
            string before = line.Substring(0, m_caretColumn);
            string after = line.Substring(m_caretColumn + 1);
            lines[m_caretLine] = before + after;
            m_textBuffer = string.Join("\n", lines);
        }
        else
        {
            // merge with next line
            if (m_caretLine < lines.Length - 1)
            {
                string nextLine = lines[m_caretLine + 1];
                lines[m_caretLine] = line + nextLine;
                List<string> li = new List<string>(lines);
                li.RemoveAt(m_caretLine + 1);
                m_textBuffer = string.Join("\n", li);
            }
        }

        ValidateJson();
        PushUndo(m_textBuffer);
        UpdateBracketMatching();
    }

    // Inserts a new line at the caret position, copying the indentation from the original line
    public void InsertNewLineWithIndent()
    {
        string[] lines = GetLines();
        RemoveSelectedLinesIfAny();
        if (m_caretLine < 0 || m_caretLine >= lines.Length) return;

        string oldLine = lines[m_caretLine];
        if (m_caretColumn > oldLine.Length) m_caretColumn = oldLine.Length;

        // Find leading whitespace for indentation
        int i = 0;
        while (i < oldLine.Length && (oldLine[i] == ' ' || oldLine[i] == '\t')) i++;
        string indent = oldLine.Substring(0, i);

        string before = oldLine.Substring(0, m_caretColumn);
        string after = oldLine.Substring(m_caretColumn);

        // Current line becomes everything before the caret
        lines[m_caretLine] = before;
        List<string> li = new List<string>(lines);
        // Insert a new line with the same indentation, followed by the text after the caret
        li.Insert(m_caretLine + 1, indent + after);

        // Move caret to the new line, positioned at the end of the indentation
        m_caretLine++;
        m_caretColumn = indent.Length;

        m_textBuffer = string.Join("\n", li);
        ValidateJson();
        PushUndo(m_textBuffer);
        UpdateBracketMatching();
    }

    // If a selection exists, remove all lines in that selection range
    private bool RemoveSelectedLinesIfAny()
    {
        if (!m_hasSelection) return false;

        int start = Mathf.Min(m_selLineStart, m_selLineEnd);
        int end = Mathf.Max(m_selLineStart, m_selLineEnd);

        string[] lines = GetLines();
        List<string> li = new List<string>(lines);
        for (int i = end; i >= start; i--)
        {
            if (i >= 0 && i < li.Count)
            {
                li.RemoveAt(i);
            }
        }
        m_caretLine = start;
        if (m_caretLine >= li.Count) m_caretLine = li.Count - 1;
        if (m_caretLine < 0) m_caretLine = 0;
        m_caretColumn = 0;

        m_textBuffer = string.Join("\n", li);
        m_hasSelection = false;

        return true; // indicates lines were removed
    }

    // Determines the character column based on a mouse x-position in pixels
    public int MeasureColumn(int lineIndex, float xPos, GUIStyle style)
    {
        string[] lines = GetLines();
        if (lineIndex < 0 || lineIndex >= lines.Length) return 0;

        string line = lines[lineIndex];
        int length = line.Length;
        
        // Exclude trailing newline characters from measurement
        while (length > 0 && (line[length - 1] == '\n' || line[length - 1] == '\r'))
        {
            length--;
        }

        // Create a temporary style without rich text for accurate measurement
        GUIStyle measureStyle = new GUIStyle(style);
        measureStyle.richText = false;

        // If xPos is before the first character, return 0
        float firstCharLeft = measureStyle.CalcSize(new GUIContent("")).x;  // typically 0
        if (xPos <= firstCharLeft)
            return 0;

        // Measure cumulative widths character by character
        for (int i = 0; i < length; i++)
        {
            float leftBoundary = measureStyle.CalcSize(new GUIContent(line.Substring(0, i))).x;
            float rightBoundary = measureStyle.CalcSize(new GUIContent(line.Substring(0, i + 1))).x;

            // Check if xPos is within the bounds of character i
            if (xPos >= leftBoundary && xPos <= rightBoundary)
            {
                // Find the midpoint to decide whether caret goes before or after this character
                float mid = (leftBoundary + rightBoundary) * 0.5f;
                return (xPos < mid) ? i : i + 1;
            }
        }

        // If xPos is beyond the last character, place caret at end of line (excluding newlines)
        return length;
    }

    // Applies syntax highlighting to a single line of JSON
    public string ColorizeJSONLine(string rawLine, int lineIndex)
    {
        int? bracketA = null;
        int? bracketB = null;

        // If we're on the caret line and there's a bracket index, store it
        if (lineIndex == m_caretLine && m_bracketIndexOnLine.HasValue)
            bracketA = m_bracketIndexOnLine.Value;

        // If there's a matching bracket absolute index, translate that to line/col
        if (m_matchingBracketAbs.HasValue)
        {
            var (ml, mc) = AbsoluteToLineCol(m_matchingBracketAbs.Value, GetLines());
            if (ml == lineIndex)
                bracketB = mc;
        }

        // Iterate through the line character by character, building a highlighted string
        bool inString = false;
        int i = 0;
        StringBuilder sb = new StringBuilder();

        while (i < rawLine.Length)
        {
            char c = rawLine[i];
            if (inString)
            {
                // If there's a quote that isn't escaped, leave  the string
                if (c == '\"' && !IsQuoteEscaped(rawLine, i))
                {
                    inString = false;
                    sb.Append("\"</color>");
                    i++;
                }
                else
                {
                    sb.Append(EscapeRichTextSymbols(c.ToString()));
                    i++;
                }
            }
            else
            {
                // Not in a string
                if (c == '\"')
                {
                    inString = true;
                    sb.Append("<color=#43D24C>\"");
                    i++;
                }
                else if ("{}[]()".IndexOf(c) >= 0)
                {
                    // Highlight brackets, and make them bold if they match caret bracket
                    bool bold = (i == bracketA || i == bracketB);
                    string colorCode = bold ? "#FF0000" : "#FFA500";
                    string piece = $"<color={colorCode}>{EscapeRichTextSymbols(c.ToString())}</color>";
                    if (bold) piece = "<b>" + piece + "</b>";
                    sb.Append(piece);
                    i++;
                }
                else if (c == ':')
                {
                    // Colons in magenta
                    sb.Append("<color=#FF00FF>:</color>");
                    i++;
                }
                else if (c == ',')
                {
                    // Commas in gray
                    sb.Append("<color=#999999>,</color>");
                    i++;
                }
                else if (IsNumberChar(c))
                {
                    // Capture entire numeric token
                    int start = i;
                    while (i < rawLine.Length && IsNumberChar(rawLine[i]))
                        i++;
                    string token = rawLine.Substring(start, i - start);
                    if (Regex.IsMatch(token, "^-?\\d+(\\.\\d+)?$"))
                        sb.Append($"<color=#499CFF>{EscapeRichTextSymbols(token)}</color>");
                    else
                        sb.Append(EscapeRichTextSymbols(token));
                }
                else if (char.IsLetter(c))
                {
                    // Capture keywords (true, false, null)
                    int start = i;
                    while (i < rawLine.Length && char.IsLetter(rawLine[i]))
                        i++;
                    string token = rawLine.Substring(start, i - start);
                    if (token == "true" || token == "false" || token == "null")
                        sb.Append($"<color=#FF4949>{EscapeRichTextSymbols(token)}</color>");
                    else
                        sb.Append(EscapeRichTextSymbols(token));
                }
                else
                {
                    // For other characters (spaces, etc.)
                    sb.Append(EscapeRichTextSymbols(c.ToString()));
                    i++;
                }
            }
        }

        // If the line ends while still in a string, close the color tag
        if (inString)
        {
            sb.Append("</color>");
            inString = false;
        }

        return sb.ToString();
    }

    // Escapes special RichText symbols (&, <, >) so they are not interpreted incorrectly
    public static string EscapeRichTextSymbols(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    // Bracket Matching
    private void UpdateBracketMatching()
    {
        RecomputeBracketPairsIfNeeded();

        m_bracketIndexOnLine = null;
        m_matchingBracketAbs = null;

        string[] lines = GetLines();
        if (m_caretLine < 0 || m_caretLine >= lines.Length) return;

        string line = lines[m_caretLine];
        if (m_caretColumn < 0) m_caretColumn = 0;
        if (m_caretColumn > line.Length) m_caretColumn = line.Length;

        int caretAbs = LineColToAbsolute(m_caretLine, m_caretColumn, lines);
        if (caretAbs < 0 || caretAbs >= m_textBuffer.Length) return;

        int bracketPos = -1;
        // Check if caret is on a bracket
        if (IsBracketAtPos(m_textBuffer, caretAbs))
        {
            bracketPos = caretAbs;
        }
        // Or if caret is immediately after a bracket
        else if (caretAbs > 0 && IsBracketAtPos(m_textBuffer, caretAbs - 1))
        {
            bracketPos = caretAbs - 1;
        }

        if (bracketPos < 0) return;

        var (bl, bc) = AbsoluteToLineCol(bracketPos, lines);
        m_bracketIndexOnLine = bc;

        // If there's a matching bracket in the dictionary, store it
        if (m_bracketPairs.TryGetValue(bracketPos, out int matchPos))
        {
            m_matchingBracketAbs = matchPos;
        }
    }

    // Recomputes bracket pairs if the text buffer changed since last computed them
    private void RecomputeBracketPairsIfNeeded()
    {
        if (m_bracketCachedText != m_textBuffer)
        {
            m_bracketCachedText = m_textBuffer;
            m_bracketPairs = ComputeBracketPairs(m_textBuffer);
        }
    }

    // Computes matching pairs for brackets (round, square, curly) while ignoring those inside strings
    private Dictionary<int, int> ComputeBracketPairs(string text)
    {
        var pairs = new Dictionary<int, int>();
        var stack = new Stack<(char c, int i)>();
        bool inString = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (!inString)
            {
                if (c == '\"' && !IsQuoteEscaped(text, i))
                {
                    inString = true;
                }
                else if (IsOpenBracket(c))
                {
                    stack.Push((c, i));
                }
                else if (IsCloseBracket(c))
                {
                    // If top of stack is a matching bracket, pop and record pairs
                    if (stack.Count > 0)
                    {
                        var top = stack.Peek();
                        if (IsMatchingPair(top.c, c))
                        {
                            stack.Pop();
                            pairs[top.i] = i;
                            pairs[i] = top.i;
                        }
                    }
                }
            }
            else
            {
                // Check if leaving the string
                if (c == '\"' && !IsQuoteEscaped(text, i))
                {
                    inString = false;
                }
            }
        }
        return pairs;
    }

    // Checks if a given position in the text is a bracket character
    private bool IsBracketAtPos(string text, int pos)
    {
        if (pos < 0 || pos >= text.Length) return false;
        char c = text[pos];
        return "{}[]()".IndexOf(c) >= 0;
    }

    // Checks if a character is an opening bracket
    private bool IsOpenBracket(char c) => (c == '{' || c == '[' || c == '(');

    // Checks if a character is a closing bracket
    private bool IsCloseBracket(char c) => (c == '}' || c == ']' || c == ')');

    // Determines if two brackets form a matching pair
    private bool IsMatchingPair(char open, char close)
    {
        return (open == '{' && close == '}')
            || (open == '[' && close == ']')
            || (open == '(' && close == ')');
    }

    // Checks if a quote at a given index is preceded by an odd number of backslashes, indicating it is escaped
    private bool IsQuoteEscaped(string txt, int quoteIndex)
    {
        int slashCount = 0;
        int idx = quoteIndex - 1;
        while (idx >= 0 && txt[idx] == '\\')
        {
            slashCount++;
            idx--;
        }
        return (slashCount % 2) == 1;
    }

    // Converts a line/column position to an absolute index in the text buffer
    private int LineColToAbsolute(int lineIndex, int col, string[] lines)
    {
        int sum = 0;
        for (int i = 0; i < lineIndex; i++)
        {
            sum += lines[i].Length;
            if (i < lines.Length - 1) sum++; // account for newline
        }
        return sum + col;
    }

    // Converts an absolute index in the text buffer to a line/column position
    private (int line, int col) AbsoluteToLineCol(int absPos, string[] lines)
    {
        int sum = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            int lineLen = lines[i].Length;
            int endOfLine = sum + lineLen;
            int afterLine = endOfLine + ((i < lines.Length - 1) ? 1 : 0);

            if (absPos <= endOfLine)
            {
                int c = absPos - sum;
                if (c < 0) c = 0;
                if (c > lineLen) c = lineLen;
                return (i, c);
            }
            else if (absPos < afterLine)
            {
                return (i + 1, 0);
            }
            sum = afterLine;
        }
        return (lines.Length - 1, lines[lines.Length - 1].Length);
    }

    // Checks if a character is part of a numeric token
    private bool IsNumberChar(char c)
    {
        return (char.IsDigit(c) || c == '-' || c == '.');
    }

    // ------------------
    // This is slightly specialised.. given a JSON string (e.g. already indented), 
    // uses bracket matching for [ and ] to find array ranges. 
    // Then checks if that array is purely numeric. If it is, unify them to one line: [1, 2, 3, 4].
    // There's the possibility this could make mistakes, but this is neater than having a new line after every comma
    public string InlineNumericArrays(string json)
    {
        // build square-bracket pairs ignoring strings
        var bracketPairs = ComputeBracketPairs(json);

        // Build the final string in a pass, skipping 
        // any bracket pairs unified. This is tracked so
        // as to not process them multiple times
        var visited = new HashSet<int>();
        var sb = new StringBuilder();

        int i = 0;
        while (i < json.Length)
        {
            // If this is an opening bracket '[' 
            // and we have a matching pair
            if (json[i] == '['
                && bracketPairs.TryGetValue(i, out int closePos)
                && !visited.Contains(i)
                && !visited.Contains(closePos))
            {
                // Check if it's purely numeric
                if (IsPurelyNumericArray(json, i, closePos))
                {
                    // unify
                    string singleLine = BuildSingleLineArray(json, i, closePos);
                    sb.Append(singleLine);

                    // mark them visited
                    visited.Add(i);
                    visited.Add(closePos);

                    // skip ahead
                    i = closePos + 1;
                    continue;
                }
            }

            // default: just copy char
            sb.Append(json[i]);
            i++;
        }

        return sb.ToString();
    }

    // Check if everything inside [ ... ] is numeric (ignoring whitespace and commas).
    // Parse them using JArray to see if it succeeds 
    // and if every element is number type
    private bool IsPurelyNumericArray(string text, int openPos, int closePos)
    {
        int length = closePos - openPos - 1;
        if (length < 1) return false;

        // Extract the substring inside the brackets
        string inner = text.Substring(openPos + 1, length);

        // Attempt to parse it as a JArray
        // then check if every token is numeric
        string candidate = "[" + inner + "]";
        try
        {
            var arr = JsonCommentHandling.ParseJToken(candidate) as JArray;
            if (arr == null) return false;

            // Check each element
            foreach (var item in arr)
            {
                if (item.Type != JTokenType.Integer
                    && item.Type != JTokenType.Float)
                {
                    return false;  // Contains a string, object, etc
                }
            }
            // If we get here => purely numeric
            return true;
        }
        catch
        {
            // parse error => not purely numeric
            return false;
        }
    }

    // builds a single-line string for the bracketed content
    // E.g. turning multiple lines or spaced items into [1, 2, 3, 4]
    private string BuildSingleLineArray(string text, int openPos, int closePos)
    {
        int length = closePos - openPos - 1;
        string inner = text.Substring(openPos + 1, length);

        // We'll parse it again via JArray 
        // and just re-serialize it with Formatting.None 
        // so it becomes [1, 2, 3, 4] on one line
        // or [1.0, 2.5] etc.
        string candidate = "[" + inner + "]";
        var arr = JsonCommentHandling.ParseJToken(candidate) as JArray;
        if (arr == null)
            return candidate;

        // Single-line serialization
        string singleLine = arr.ToString(Formatting.None);

        // That includes the brackets, so we can just return it
        return singleLine;
    }


    // Validate + Save
    public void ValidateJson()
    {
        try
        {
            JsonCommentHandling.ParseJObject(m_textBuffer ?? string.Empty);
            m_parseError = string.Empty;
        }
        catch (Exception ex)
        {
            m_parseError = ex.Message;
        }
    }

    // Prompts user to save the current JSON to a file, with option to override parse errors
    public void SaveJsonAs()
    {
        if (!string.IsNullOrEmpty(m_parseError))
        {
            bool ok = EditorUtility.DisplayDialog(
                "JSON Parse Error",
                "Not valid JSON. Save anyway?",
                "Yes", "No"
            );
            if (!ok) return;
        }

        string path = EditorUtility.SaveFilePanel(
            "Save JSON as...",
            m_chronoVehicleDataRoot,
            $"New_{m_subcomponentType}.json", "json"
        );
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            File.WriteAllText(path, m_textBuffer);
            AssetDatabase.Refresh();
            Debug.Log("Saved JSON to " + path);
        }
        catch (Exception ex)
        {
            Debug.LogError("Save error: " + ex.Message);
        }
    }

    // Undo / Redo
    public void DoUndo()
    {
        if (undoStack.Count > 1)
        {
            // Pop the current state
            string top = undoStack.Pop();
            redoStack.Push(top);

            // Revert to the previous state
            m_textBuffer = undoStack.Peek();
            ValidateJson();

            string[] lines = GetLines();
            if (m_caretLine >= lines.Length) m_caretLine = lines.Length - 1;
            if (m_caretLine < 0) m_caretLine = 0;
            if (m_caretColumn > lines[m_caretLine].Length)
                m_caretColumn = lines[m_caretLine].Length;
            m_hasSelection = false;

            UpdateBracketMatching();
        }
    }

    // Performs a Redo operation, re-applying the last undone change
    public void DoRedo()
    {
        if (redoStack.Count > 0)
        {
            // Pop the top of redo and push it to undo
            string txt = redoStack.Pop();
            undoStack.Push(txt);
            m_textBuffer = txt;

            ValidateJson();
            string[] lines = GetLines();
            if (m_caretLine >= lines.Length) m_caretLine = lines.Length - 1;
            if (m_caretLine < 0) m_caretLine = 0;
            if (m_caretColumn > lines[m_caretLine].Length)
                m_caretColumn = lines[m_caretLine].Length;
            m_hasSelection = false;

            UpdateBracketMatching();
        }
    }

    // Cut / Copy / Paste
    public void DoCut()
    {
        DoCopy(); // Copy to clipboard first
        string[] lines = GetLines();
        List<string> li = new List<string>(lines);

        if (!m_hasSelection)
        {
            if (m_caretLine >= 0 && m_caretLine < li.Count)
            {
                li.RemoveAt(m_caretLine);
                if (m_caretLine >= li.Count) m_caretLine = li.Count - 1;
            }
        }
        else
        {
            int start = Mathf.Min(m_selLineStart, m_selLineEnd);
            int end = Mathf.Max(m_selLineStart, m_selLineEnd);
            for (int i = end; i >= start; i--)
            {
                if (i >= 0 && i < li.Count) li.RemoveAt(i);
            }
            m_caretLine = start;
            if (m_caretLine >= li.Count) m_caretLine = li.Count - 1;
            m_hasSelection = false;
        }

        if (m_caretLine < 0) m_caretLine = 0;
        m_caretColumn = 0;

        m_textBuffer = string.Join("\n", li);
        ValidateJson();
        PushUndo(m_textBuffer);
        UpdateBracketMatching();
    }

    // Copies selected lines (or the caret line if no selection) to the clipboard
    public void DoCopy()
    {
        string[] lines = GetLines();
        if (!m_hasSelection)
        {
            if (m_caretLine >= 0 && m_caretLine < lines.Length)
            {
                EditorGUIUtility.systemCopyBuffer = lines[m_caretLine];
            }
        }
        else
        {
            int start = Mathf.Min(m_selLineStart, m_selLineEnd);
            int end = Mathf.Max(m_selLineStart, m_selLineEnd);
            List<string> sel = new List<string>();
            for (int i = start; i <= end; i++)
            {
                if (i >= 0 && i < lines.Length)
                    sel.Add(lines[i]);
            }
            EditorGUIUtility.systemCopyBuffer = string.Join("\n", sel);
        }
    }

    // Pastes the clipboard contents at the current caret position (one line per clipboard line)
    public void DoPaste()
    {
        string clip = EditorGUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(clip)) return;
        string[] clipLines = clip.Split('\n');

        string[] lines = GetLines();
        List<string> li = new List<string>(lines);

        if (m_caretLine < 0) m_caretLine = 0;
        if (m_caretLine >= li.Count) m_caretLine = li.Count - 1;

        // Insert each line from clipboard after the caret line
        for (int i = 0; i < clipLines.Length; i++)
        {
            li.Insert(m_caretLine + 1, clipLines[i]);
            m_caretLine++;
        }
        m_caretColumn = 0;

        m_textBuffer = string.Join("\n", li);
        ValidateJson();
        PushUndo(m_textBuffer);
        UpdateBracketMatching();
    }

    // If undo called
    private void PushUndo(string txt)
    {
        if (undoStack.Count == 0 || undoStack.Peek() != txt)
        {
            undoStack.Push(txt);
            redoStack.Clear();
        }
    }

    // Phew.
}
