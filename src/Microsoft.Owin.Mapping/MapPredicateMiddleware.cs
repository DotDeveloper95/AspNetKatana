﻿// <copyright file="MapPredicateMiddleware.cs" company="Katana contributors">
//   Copyright 2011-2012 Katana contributors
// </copyright>
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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Owin.Mapping
{
    using AppFunc = Func<IDictionary<string, object>, Task>;
    using Predicate = Func<IDictionary<string, object>, bool>;

    public class MapPredicateMiddleware
    {
        private readonly AppFunc _next;
        private readonly AppFunc _branch;
        private readonly Predicate _predicate;

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "By design")]
        public MapPredicateMiddleware(AppFunc next, AppFunc branch, Predicate predicate)
        {
            if (next == null)
            {
                throw new ArgumentNullException("next");
            }
            if (branch == null)
            {
                throw new ArgumentNullException("branch");
            }
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }
            _next = next;
            _branch = branch;
            _predicate = predicate;
        }

        public Task Invoke(IDictionary<string, object> environment)
        {
            if (_predicate(environment))
            {
                return _branch(environment);
            }

            return _next(environment);
        }
    }
}
