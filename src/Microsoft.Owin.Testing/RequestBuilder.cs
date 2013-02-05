// <copyright file="RequestBuilder.cs" company="Katana contributors">
//   Copyright 2011-2013 Katana contributors
// </copyright>
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Owin.Testing
{
    public class RequestBuilder
    {
        private readonly TestServer _server;
        private readonly HttpRequestMessage _req;

        public RequestBuilder(TestServer server, string path)
        {
            _server = server;
            _req = new HttpRequestMessage(HttpMethod.Get, "http://localhost" + path);
        }

        public RequestBuilder And(Action<HttpRequestMessage> configure)
        {
            configure(_req);
            return this;
        }

        public RequestBuilder Header(string name, string value)
        {
            if (!_req.Headers.TryAddWithoutValidation(name, value))
            {
                _req.Content.Headers.TryAddWithoutValidation(name, value);
            }
            return this;
        }

        public Task<HttpResponseMessage> SendAsync(string method)
        {
            _req.Method = new HttpMethod(method);
            return _server.HttpClient.SendAsync(_req);
        }
    }
}
