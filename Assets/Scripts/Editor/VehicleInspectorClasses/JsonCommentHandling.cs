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
//
// Utility helpers for parsing Chrono JSON files that may include non-standard
// C++ style comments (some of the chrono ones contain it). Puts it all in 
// one class so its dealt with consistently
// =============================================================================

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChronoVehicleBuilder
{
    public static class JsonCommentHandling
    {
        private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Load,
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Replace
        };

        public static JObject ParseJObject(string json, string contextLabel = null)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            string sanitized = StripComments(json);

            using (var stringReader = new StringReader(sanitized))
            using (var jsonReader = CreateReader(stringReader))
            {
                try
                {
                    return JObject.Load(jsonReader, LoadSettings);
                }
                catch (JsonReaderException ex) when (!string.IsNullOrEmpty(contextLabel))
                {
                    throw new JsonReaderException(
                        $"{contextLabel}: {ex.Message}",
                        ex.Path,
                        ex.LineNumber,
                        ex.LinePosition,
                        ex);
                }
            }
        }

        public static JToken ParseJToken(string json, string contextLabel = null)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            string sanitized = StripComments(json);

            using (var stringReader = new StringReader(sanitized))
            using (var jsonReader = CreateReader(stringReader))
            {
                try
                {
                    return JToken.Load(jsonReader, LoadSettings);
                }
                catch (JsonReaderException ex) when (!string.IsNullOrEmpty(contextLabel))
                {
                    throw new JsonReaderException(
                        $"{contextLabel}: {ex.Message}",
                        ex.Path,
                        ex.LineNumber,
                        ex.LinePosition,
                        ex);
                }
            }
        }

        public static bool TryParseJObject(string json, out JObject result)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                result = null;
                return false;
            }

            try
            {
                result = ParseJObject(json);
                return true;
            }
            catch (JsonException)
            {
                result = null;
                return false;
            }
        }

        public static bool TryParseJToken(string json, out JToken result)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                result = null;
                return false;
            }

            try
            {
                result = ParseJToken(json);
                return true;
            }
            catch (JsonException)
            {
                result = null;
                return false;
            }
        }

        private static JsonTextReader CreateReader(TextReader textReader)
        {
            var reader = new JsonTextReader(textReader)
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double,
                Culture = CultureInfo.InvariantCulture,
                CloseInput = false
            };

            return reader;
        }

        private static string StripComments(string json)
        {
            var sb = new StringBuilder(json.Length);
            bool inString = false;
            bool inSingleLineComment = false;
            bool inMultiLineComment = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                char next = i + 1 < json.Length ? json[i + 1] : '\0';

                if (inSingleLineComment)
                {
                    if (c == '\n' || c == '\r')
                    {
                        inSingleLineComment = false;
                        sb.Append(c);
                    }
                    continue;
                }

                if (inMultiLineComment)
                {
                    if (c == '*' && next == '/')
                    {
                        inMultiLineComment = false;
                        i++;
                    }
                    else if (c == '\n' || c == '\r')
                    {
                        sb.Append(c);
                    }
                    continue;
                }

                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && next != '\0')
                    {
                        sb.Append(next);
                        i++;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    sb.Append(c);
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    inSingleLineComment = true;
                    i++;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    inMultiLineComment = true;
                    i++;
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
