// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using Microsoft.Owin.Security.OAuth.Messages;

namespace Microsoft.Owin.Security.OAuth
{
    public class OAuthValidateAuthorizeRequestContext : BaseValidatingContext
    {
        public OAuthValidateAuthorizeRequestContext(
            IOwinContext context,
            OAuthAuthorizationServerOptions options,
            AuthorizeEndpointRequest authorizeRequest,
            OAuthLookupClientContext clientContext) : base(context, options)
        {
            AuthorizeRequest = authorizeRequest;
            ClientContext = clientContext;
        }

        public AuthorizeEndpointRequest AuthorizeRequest { get; private set; }
        public OAuthLookupClientContext ClientContext { get; private set; }
    }
}
