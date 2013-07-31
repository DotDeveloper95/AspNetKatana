﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Owin.Logging;
using Microsoft.Owin.Security.DataHandler;
using Microsoft.Owin.Security.DataProtection;
using Microsoft.Owin.Security.Infrastructure;
using Owin;

namespace Microsoft.Owin.Security.OAuth
{
    public class OAuthAuthorizationServerMiddleware : AuthenticationMiddleware<OAuthAuthorizationServerOptions>
    {
        private readonly ILogger _logger;

        public OAuthAuthorizationServerMiddleware(
            OwinMiddleware next,
            IAppBuilder app,
            OAuthAuthorizationServerOptions options)
            : base(next, options)
        {
            _logger = app.CreateLogger<OAuthAuthorizationServerMiddleware>();

            if (Options.Provider == null)
            {
                Options.Provider = new OAuthAuthorizationServerProvider();
            }
            if (Options.AuthorizationCodeFormat == null)
            {
                IDataProtector dataProtecter = app.CreateDataProtector(
                    typeof(OAuthAuthorizationServerMiddleware).FullName,
                    "Authentication_Code", "v1");

                Options.AuthorizationCodeFormat = new TicketDataFormat(dataProtecter);
            }
            if (Options.AccessTokenFormat == null)
            {
                IDataProtector dataProtecter = app.CreateDataProtector(
                    typeof(OAuthAuthorizationServerMiddleware).Namespace,
                    "Access_Token", "v1");
                Options.AccessTokenFormat = new TicketDataFormat(dataProtecter);
            }
            if (Options.RefreshTokenFormat == null)
            {
                IDataProtector dataProtecter = app.CreateDataProtector(
                    typeof(OAuthAuthorizationServerMiddleware).Namespace,
                    "Refresh_Token", "v1");
                Options.RefreshTokenFormat = new TicketDataFormat(dataProtecter);
            }
            if (Options.AuthorizationCodeProvider == null)
            {
                Options.AuthorizationCodeProvider = new AuthenticationTokenProvider();
            }
            if (Options.AccessTokenProvider == null)
            {
                Options.AccessTokenProvider = new AuthenticationTokenProvider();
            }
            if (Options.RefreshTokenProvider == null)
            {
                Options.RefreshTokenProvider = new AuthenticationTokenProvider();
            }
        }

        protected override AuthenticationHandler<OAuthAuthorizationServerOptions> CreateHandler()
        {
            return new OAuthAuthorizationServerHandler(_logger);
        }
    }
}
