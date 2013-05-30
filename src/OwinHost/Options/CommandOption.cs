// <copyright file="CommandOption.cs" company="Microsoft Open Technologies, Inc.">
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

namespace OwinHost.Options
{
    public class CommandOption
    {
        public CommandOption(string name, string shortName, string description, Action<Command, string> accept)
        {
            Name = name;
            ShortName = shortName;
            Description = description;
            Accept = accept;
            Predicate = value =>
            {
                if (value.StartsWith("--", StringComparison.Ordinal))
                {
                    return string.Equals(value.Substring(2), name, StringComparison.OrdinalIgnoreCase);
                }
                if (value.StartsWith("-", StringComparison.Ordinal))
                {
                    return string.Equals(value.Substring(1), shortName, StringComparison.Ordinal);
                }
                return false;
            };
        }

        public string Name { get; private set; }
        public string ShortName { get; private set; }
        public string Description { get; private set; }

        public Func<string, bool> Predicate { get; private set; }
        public Action<Command, string> Accept { get; private set; }
    }
}
