// <copyright file="OwinResponseExtensions.cs" company="Microsoft Open Technologies, Inc.">
// Copyright 2013 Microsoft Open Technologies, Inc. All rights reserved.
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

using Owin.Types.Helpers;
using System.Collections.Generic;

namespace Owin.Types.Extensions
{
#region OwinResponseExtensions.Cookies

    internal static partial class OwinRequestExtensions
    {
        public static OwinResponse AddCookie(this OwinResponse response, string key, string value)
        {
            OwinHelpers.AddCookie(response, key, value);
            return response;
        }

        public static OwinResponse AddCookie(this OwinResponse response, string key, string value, CookieOptions options)
        {
            OwinHelpers.AddCookie(response, key, value, options);
            return response;
        }

        public static OwinResponse DeleteCookie(this OwinResponse response, string key)
        {
            OwinHelpers.DeleteCookie(response, key);
            return response;
        }

        public static OwinResponse DeleteCookie(this OwinResponse response, string key, CookieOptions options)
        {
            OwinHelpers.DeleteCookie(response, key, options);
            return response;
        }
    }
#endregion

#region OwinResponseExtensions

    [System.CodeDom.Compiler.GeneratedCode("App_Packages", "")]
    internal static partial class OwinResponseExtensions
    {
    }
#endregion

}
