﻿// <copyright file="GoogleAuthenticationMiddleware.cs" company="Microsoft Open Technologies, Inc.">
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
using Microsoft.Owin.Security.DataProtection;
using Microsoft.Owin.Security.ModelSerializer;
using Microsoft.Owin.Security.TextEncoding;

namespace Microsoft.Owin.Security.Google
{
    public class GoogleAuthenticationMiddleware : AuthenticationMiddleware<GoogleAuthenticationOptions>
    {
        private readonly ProtectionHandler<IDictionary<string, string>> _extraProtectionHandler;

        public GoogleAuthenticationMiddleware(
            OwinMiddleware next,
            GoogleAuthenticationOptions options)
            : base(next, options)
        {
            if (Options.Provider == null)
            {
                Options.Provider = new GoogleAuthenticationProvider();
            }
            IDataProtection dataProtection = Options.DataProtection;
            if (Options.DataProtection == null)
            {
                dataProtection = DataProtectionProviders.Default.Create("GoogleAuthenticationMiddleware", Options.AuthenticationType);
            }

            _extraProtectionHandler = new ProtectionHandler<IDictionary<string, string>>(
                ModelSerializers.Extra,
                dataProtection,
                TextEncodings.Base64Url);
        }

        protected override AuthenticationHandler<GoogleAuthenticationOptions> CreateHandler()
        {
            return new GoogleAuthenticationHandler(_extraProtectionHandler);
        }
    }
}
