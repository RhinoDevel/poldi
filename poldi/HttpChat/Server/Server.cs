
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

// RhinoDevel. Marcel Timm, 2025feb24

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;

namespace Poldi.HttpChat.Server
{
    internal static class Server
    {
        private static string? _url;
        private static List<Endpoint>? _endpoints;

        private static readonly object _lockObj = new();

        private static Endpoint GetEndpoint(string absPath)
        {
            Debug.Assert(_url != null && _endpoints != null);

            var absPathToUse = absPath.StartsWith('/')
                    ? absPath.Substring(1) : absPath;

            return _endpoints.First(
                endpoint =>
                {
                    return absPathToUse.StartsWith(endpoint.Path);
                });
        }

        /// <remarks>
        /// Returns no HTTP messages on error.
        /// 
        /// To-be-running in a non-main thread.
        /// 
        /// Will use the <see cref="_lockObj"/> (wait for it to be free) or not,
        /// depending on the endpoint responsible.
        /// </remarks>
        private static void ProcessRequest(
            HttpListenerContext httpListenerCtx)
        {
            try
            {
                string urlStr;
                Endpoint endpoint;

                Debug.Assert(httpListenerCtx.Request.Url != null);
                // TODO: This URL string is kind of wrong, if a reverse proxy is used!
                urlStr = httpListenerCtx.Request.Url.ToString();
                Log.Line($"Received request for \"{urlStr}\".");

                endpoint = GetEndpoint(
                    httpListenerCtx.Request.Url.AbsolutePath);

                //// Quick way to allow CORS.
                //httpListenerCtx.Response.AddHeader(
                //    "Access-Control-Allow-Origin", "*");
                //if (httpListenerCtx.Request.HttpMethod == "OPTIONS")
                //{
                //    httpListenerCtx.Response.AddHeader(
                //        "Access-Control-Allow-Methods",
                //        "GET, POST, OPTIONS");
                //    httpListenerCtx.Response.AddHeader(
                //        "Access-Control-Allow-Headers",
                //        "Content-Type, Authorization");
                //
                //    httpListenerCtx.Response.StatusCode = 204;
                //    httpListenerCtx.Response.Close();
                //    return;
                //}

                if (endpoint.HttpMethod != httpListenerCtx.Request.HttpMethod)
                {
                    Log.Line($"Error: Received request with unexpected HTTP method \"{endpoint.HttpMethod}\"for \"{urlStr}\"!");
                    return;
                }

                if (httpListenerCtx.Request.Url.Query.Length != 0)
                {
                    Log.Line($"Error: Received request with query \"{httpListenerCtx.Request.Url.Query}\", but queries are NOT supported!");
                    return;
                }

                if (endpoint.ResponseContentType != null)
                {
                    httpListenerCtx.Response.ContentType =
                        endpoint.ResponseContentType;
                }

                Debug.Assert(endpoint.OnRequest != null);

                if (endpoint.IgnoresLock)
                {
                    Log.Line($"Request for \"{urlStr}\" is IGNORING the lock.");

                    endpoint.OnRequest(
                        httpListenerCtx.Request, httpListenerCtx.Response);
                }
                else
                {
                    Log.Line($"Request for \"{urlStr}\" is waiting for lock..");

                    lock(_lockObj)
                    {
                        Log.Line($"Request for \"{urlStr}\" has acquired the lock.");

                        endpoint.OnRequest(
                            httpListenerCtx.Request, httpListenerCtx.Response);
                    }
                }

                return;
            }
            catch (Exception e)
            {
                Log.Line($"Error: Exception with message \"{e.Message}\"!");
                return;
            }
            finally
            {
                httpListenerCtx.Response.Close();
            }
        }

        internal static bool Exec(string url, List<Endpoint> endpoints)
        {
            var l = new HttpListener();
            bool cancelRequested = false;

            _url = url;
            _endpoints = endpoints;

            Console.CancelKeyPress +=
                (_, e) =>
                {
                    e.Cancel = true;
                    cancelRequested = true;
                    l.Close(); // Seems to be OK to call from another thread..
                };

            try
            {
                _endpoints.ForEach(
                    endpoint =>
                    {
                        var prefix = $"{_url}{endpoint.Path}/";

                        l.Prefixes.Add(prefix);

                        Log.Line($"Added HTTP \"{endpoint.HttpMethod}\" endpoint for \"{prefix}\".");
                    });

                l.Start();

                while (true)
                {
                    try
                    {
                        var ctx = l.GetContext();

                        new Thread(() => ProcessRequest(ctx)).Start();
                    }
                    catch(Exception)
                    {
                        if(cancelRequested)
                        {
                            Debug.Assert(!l.IsListening);
                            l = null;
                            break;
                        }
                        throw; // RE-throws
                    }
                }
                Log.Line("Shutting down HTTP server..");
                Debug.Assert(l == null);
                _url = null;
                _endpoints = null;
                return true;
            }
            catch (Exception e)
            {
                Log.Line($"Error: Exception with message \"{e.Message}\"!");
                l!.Stop();
                _url = null;
                _endpoints = null;
                return false;
            }
        }
    }
}
