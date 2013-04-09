// <copyright file="FormsValidateIdentityContext.cs" company="Microsoft Open Technologies, Inc.">
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

using System.Security.Principal;

namespace Microsoft.Owin.Security.Forms
{
    public class FormsValidateIdentityContext
    {
        public FormsValidateIdentityContext(IIdentity identity)
        {
            Identity = identity;
        }

        public IIdentity Identity { get; private set; }

        public void ReplaceIdentity(IIdentity identity)
        {
            Identity = identity;
        }

        public void RejectIdentity()
        {
            Identity = null;
        }
    }
}
