﻿// <copyright file="FacebookAuthenticationMiddleware.cs" company="Microsoft Open Technologies, Inc.">
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

using Microsoft.Owin.Logging;
using Microsoft.Owin.Security.DataHandler;
using Microsoft.Owin.Security.DataProtection;
using Microsoft.Owin.Security.Infrastructure;
using Owin;

namespace Microsoft.Owin.Security.Facebook
{
    public class FacebookAuthenticationMiddleware : AuthenticationMiddleware<FacebookAuthenticationOptions>
    {
        private readonly ILogger _logger;

        public FacebookAuthenticationMiddleware(
            OwinMiddleware next,
            IAppBuilder app,
            FacebookAuthenticationOptions options)
            : base(next, options)
        {
            _logger = app.CreateLogger<FacebookAuthenticationMiddleware>();

            if (Options.Provider == null)
            {
                Options.Provider = new FacebookAuthenticationProvider();
            }
            if (Options.StateDataFormat == null)
            {
                var dataProtector = app.CreateDataProtector(
                    typeof(FacebookAuthenticationMiddleware).FullName, 
                    Options.AuthenticationType);
                Options.StateDataFormat = new ExtraDataFormat(dataProtector);
            }
        }

        protected override AuthenticationHandler<FacebookAuthenticationOptions> CreateHandler()
        {
            return new FacebookAuthenticationHandler(_logger);
        }
    }
}
