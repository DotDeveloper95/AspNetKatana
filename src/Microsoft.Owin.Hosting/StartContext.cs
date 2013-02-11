﻿// <copyright file="StartContext.cs" company="Microsoft Open Technologies, Inc.">
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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Owin.Hosting.ServerFactory;
using Owin;

namespace Microsoft.Owin.Hosting
{
    public class StartContext
    {
        public StartContext()
        {
            Options = new StartOptions();
            EnvironmentData = new List<KeyValuePair<string, object>>();
        }

        public StartOptions Options { get; set; }

        public IServerFactory ServerFactory { get; set; }
        public IAppBuilder Builder { get; set; }
        public object App { get; set; }
        public Action<IAppBuilder> Startup { get; set; }
        public TextWriter Output { get; set; }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "By design")]
        public IList<KeyValuePair<string, object>> EnvironmentData { get; private set; }
    }
}
