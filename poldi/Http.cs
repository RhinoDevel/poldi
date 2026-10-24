
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

// Marcel Timm, RhinoDevel, 2024nov19

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Poldi
{
    /// <summary>
    /// A simple HTTP client (e.g. used for the Wikipedia search).
    /// </summary>
    /// <remarks>
    /// Not really thread-safe (let's restrict usage to one request at a time).
    /// 
    /// No solution for the problem of retaining stale DNS entries (just to keep
    /// in mind).
    /// </remarks>
    internal static class Http
    {
        private static readonly string _userAgent =
            C.D<string>("http:userAgent");
        private static readonly int _timeout = // Seconds
            C.D<int>("http:timeout");

        private static readonly HttpClient _client;

        static Http()
        {
            _client = new HttpClient(
                new HttpClientHandler
                {
                    // Allow self-signed server certificates:
                    //
                    ServerCertificateCustomValidationCallback =
                        (message, cert, chain, errors) => { return true; }
                })
                {
                    Timeout = TimeSpan.FromSeconds(_timeout)
                };

            _client.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);
        }

        internal static async Task<string?> GetStrAsync(string url)
        {
            try
            {
                return await _client.GetStringAsync(url);
            }
            catch (Exception e)
            {
                Log.Line($"Error: Request to URL \"{url}\" failed with exception (msg.: \"{e.Message}\")!");
                return null;
            }
        }

        internal static async Task GetStrAsync(
            string url, Action<string?> onDone)
        {
            var str = await GetStrAsync(url);

            onDone(str);
        }
    }

}
