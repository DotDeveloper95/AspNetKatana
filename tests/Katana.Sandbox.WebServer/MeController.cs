﻿// <copyright file="MeController.cs" company="Microsoft Open Technologies, Inc.">
// Copyright 2011-2013 Microsoft Open Technologies, Inc. All rights reserved.
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
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Microsoft.Owin;

namespace Katana.Sandbox.WebServer
{
    [Authorize]
    public class MeController : ApiController
    {
        public async Task<HttpResponseMessage> Get(HttpRequestMessage req)
        {
            var owinContext = new OwinContext((IDictionary<string, object>)req.Properties["MS_OwinEnvironment"]);
            var result = await owinContext.Authentication.AuthenticateAsync("Bearer");
            if (result == null || result.Identity == null)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            var identity = result.Identity;
            var extra = result.Properties;

            return new HttpResponseMessage
            {
                Content = new ObjectContent(
                    typeof(Me),
                    new Me
                    {
                        Details = identity.Claims
                            .Select(x => new Detail { Name = x.Type, Value = x.Value, Issuer = x.Issuer })
                            .ToList(),
                        Extra = extra.Dictionary
                            .Select(x => new Extra { Name = x.Key, Value = x.Value })
                            .ToList(),
                    },
                    new JsonMediaTypeFormatter())
            };
        }

        public class Me
        {
            public List<Detail> Details { get; set; }
            public List<Extra> Extra { get; set; }
        }

        public class Detail
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Issuer { get; set; }
        }

        public class Extra
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }
    }
}
