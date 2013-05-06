// <copyright file="FacebookAuthenticationHandler.cs" company="Microsoft Open Technologies, Inc.">
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

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Owin.Helpers;
using Microsoft.Owin.Infrastructure;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Microsoft.Owin.Security.Facebook
{
    internal class FacebookAuthenticationHandler : AuthenticationHandler<FacebookAuthenticationOptions>
    {
        private readonly ILogger _logger;

        public FacebookAuthenticationHandler(ILogger logger)
        {
            _logger = logger;
        }

        protected override async Task<AuthenticationTicket> AuthenticateCore()
        {
            _logger.WriteVerbose("AuthenticateCore");

            AuthenticationExtra extra = null;

            try
            {
                string code = null;
                string state = null;

                IDictionary<string, string[]> query = Request.GetQuery();
                string[] values;
                if (query.TryGetValue("code", out values) && values != null && values.Length == 1)
                {
                    code = values[0];
                }
                if (query.TryGetValue("state", out values) && values != null && values.Length == 1)
                {
                    state = values[0];
                }
                if (query.TryGetValue("error", out values) && values != null && values.Length == 1)
                {
                    AddErrorDetail("error", values[0]);
                }
                if (query.TryGetValue("error_code", out values) && values != null && values.Length == 1)
                {
                    AddErrorDetail("error_code", values[0]);
                }
                if (query.TryGetValue("error_description", out values) && values != null && values.Length == 1)
                {
                    AddErrorDetail("error_description", values[0]);
                }
                if (query.TryGetValue("error_reason", out values) && values != null && values.Length == 1)
                {
                    AddErrorDetail("error_reason", values[0]);
                }

                extra = Options.StateDataHandler.Unprotect(state);
                if (extra == null)
                {
                    return null;
                }

                string tokenEndpoint =
                    "https://graph.facebook.com/oauth/access_token";

                string requestPrefix = Request.Scheme + "://" + Request.Host;
                string redirectUri = requestPrefix + Request.PathBase + Options.ReturnEndpointPath;

                string tokenRequest = "grant_type=authorization_code" +
                    "&code=" + Uri.EscapeDataString(code) +
                    "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                    "&client_id=" + Uri.EscapeDataString(Options.AppId) +
                    "&client_secret=" + Uri.EscapeDataString(Options.AppSecret);

                WebRequest webRequest = WebRequest.Create(tokenEndpoint + "?" + tokenRequest);
                WebResponse webResponse = await webRequest.GetResponseAsync();

                NameValueCollection form;
                using (var reader = new StreamReader(webResponse.GetResponseStream()))
                {
                    string text = await reader.ReadToEndAsync();
                    form = WebHelpers.ParseNameValueCollection(text);
                }

                string accessToken = form["access_token"];
                string expires = form["expires"];

                string graphApiEndpoint =
                    "https://graph.facebook.com/me";

                webRequest = WebRequest.Create(graphApiEndpoint + "?access_token=" + Uri.EscapeDataString(accessToken));
                webResponse = await webRequest.GetResponseAsync();
                JObject user;
                using (var reader = new StreamReader(webResponse.GetResponseStream()))
                {
                    user = JObject.Parse(await reader.ReadToEndAsync());
                }

                var context = new FacebookAuthenticatedContext(Request.Environment, user, accessToken);
                context.Identity = new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, context.Id, "http://www.w3.org/2001/XMLSchema#string", Options.AuthenticationType),
                        new Claim(ClaimTypes.Name, context.Username, "http://www.w3.org/2001/XMLSchema#string", Options.AuthenticationType),
                        new Claim(ClaimTypes.Email, context.Email, "http://www.w3.org/2001/XMLSchema#string", Options.AuthenticationType),
                        new Claim("urn:facebook:name", context.Name, "http://www.w3.org/2001/XMLSchema#string", Options.AuthenticationType),
                        new Claim("urn:facebook:link", context.Link, "http://www.w3.org/2001/XMLSchema#string", Options.AuthenticationType),
                    },
                    Options.AuthenticationType,
                    ClaimsIdentity.DefaultNameClaimType,
                    ClaimsIdentity.DefaultRoleClaimType);

                context.Extra = extra;

                await Options.Provider.Authenticated(context);

                return new AuthenticationTicket(context.Identity, context.Extra);
            }
            catch (Exception ex)
            {
                _logger.WriteError(ex.Message);
            }
            return new AuthenticationTicket(null, extra);
        }

        protected override async Task ApplyResponseChallenge()
        {
            _logger.WriteVerbose("ApplyResponseChallenge");

            if (Response.StatusCode != 401)
            {
                return;
            }

            AuthenticationResponseChallenge challenge = Helper.LookupChallenge(Options.AuthenticationType, Options.AuthenticationMode);

            if (challenge != null)
            {
                string requestPrefix = Request.Scheme + "://" + Request.Host;

                string currentQueryString = Request.QueryString;
                string currentUri = string.IsNullOrEmpty(currentQueryString)
                    ? requestPrefix + Request.PathBase + Request.Path
                    : requestPrefix + Request.PathBase + Request.Path + "?" + currentQueryString;

                string redirectUri = requestPrefix + Request.PathBase + Options.ReturnEndpointPath;

                AuthenticationExtra extra = challenge.Extra;
                if (string.IsNullOrEmpty(extra.RedirectUrl))
                {
                    extra.RedirectUrl = currentUri;
                }

                string state = Options.StateDataHandler.Protect(extra);

                string authorizationEndpoint =
                    "https://www.facebook.com/dialog/oauth" +
                        "?response_type=code" +
                        "&client_id=" + Uri.EscapeDataString(Options.AppId) +
                        "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                        "&scope=" + Uri.EscapeDataString(Options.Scope) +
                        "&state=" + Uri.EscapeDataString(state);

                Response.Redirect(authorizationEndpoint);
            }
        }

        public override async Task<bool> Invoke()
        {
            return await InvokeReplyPath();
        }

        private async Task<bool> InvokeReplyPath()
        {
            _logger.WriteVerbose("InvokeReplyPath");

            if (Options.ReturnEndpointPath != null &&
                String.Equals(Options.ReturnEndpointPath, Request.Path, StringComparison.OrdinalIgnoreCase))
            {
                // TODO: error responses

                AuthenticationTicket ticket = await Authenticate();

                var context = new FacebookReturnEndpointContext(Request.Environment, ticket, ErrorDetails);
                context.SignInAsAuthenticationType = Options.SignInAsAuthenticationType;
                context.RedirectUri = ticket.Extra.RedirectUrl;

                await Options.Provider.ReturnEndpoint(context);

                if (context.SignInAsAuthenticationType != null &&
                    context.Identity != null)
                {
                    ClaimsIdentity grantIdentity = context.Identity;
                    if (!string.Equals(grantIdentity.AuthenticationType, context.SignInAsAuthenticationType, StringComparison.Ordinal))
                    {
                        grantIdentity = new ClaimsIdentity(grantIdentity.Claims, context.SignInAsAuthenticationType, grantIdentity.NameClaimType, grantIdentity.RoleClaimType);
                    }
                    Response.Grant(grantIdentity, context.Extra);
                }

                if (!context.IsRequestCompleted && context.RedirectUri != null)
                {
                    string redirectUri = context.RedirectUri;
                    if (ErrorDetails != null)
                    {
                        redirectUri = WebUtilities.AddQueryString(redirectUri, ErrorDetails);
                    }
                    Response.Redirect(redirectUri);
                    context.RequestCompleted();
                }

                return context.IsRequestCompleted;
            }
            return false;
        }
    }
}
