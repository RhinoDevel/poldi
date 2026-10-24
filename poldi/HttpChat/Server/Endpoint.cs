
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

// RhinoDevel, Marcel Timm, 2025feb24

using System.Diagnostics;

namespace Poldi.HttpChat.Server
{
    internal class Endpoint
    {
        internal readonly string Path;
        internal readonly string HttpMethod;
        internal readonly OnRequestDelegate OnRequest;
        internal readonly string? ResponseContentType;
        internal readonly bool IgnoresLock;

        internal Endpoint(
            string path,
            string httpMethod,
            OnRequestDelegate onRequest,
            string? responseContentType,
            bool ignoresLock)
        {
            Debug.Assert(path.Trim().Length != 0);
            Debug.Assert(httpMethod.Trim().Length != 0);
            Debug.Assert(
                responseContentType == null
                    || responseContentType.Trim().Length != 0);

            Path = path;
            HttpMethod = httpMethod;
            OnRequest = onRequest;
            ResponseContentType = responseContentType;
            IgnoresLock = ignoresLock;
        }
    }
}
