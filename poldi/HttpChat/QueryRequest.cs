
//    P.O.L.D.I. - Private Offline Local Digital Intelligence
//    Copyright (C) 2026  Marcel Timm, RhinoDevel
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <https://www.gnu.org/licenses/>.

// Marcel Timm, 2025feb24

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poldi.HttpChat
{
    internal class QueryRequest
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        internal static QueryRequest? CreateFromJson(string json)
        {
            if (json.Trim().Length == 0)
            {
                Log.Line("Error: Given JSON is empty or white-space only!");
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<QueryRequest>(
                    json,
                    new JsonSerializerOptions
                    {
                        Converters =
                        {
                            new JsonStringEnumConverter()
                        }
                    });
            }
            catch (Exception e)
            {
                Log.Line($"Error: Exception with message \"{e.Message}\"!");
                return null;
            }
        }
    }
}
