// <copyright file="DefaultServiceProvider.cs" company="Katana contributors">
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Owin.Hosting.Services
{
    public class DefaultServiceProvider : IServiceProvider
    {
        private readonly IDictionary<Type, Func<object>> _services = new Dictionary<Type, Func<object>>();
        private readonly IDictionary<Type, List<Func<object>>> _priorServices = new Dictionary<Type, List<Func<object>>>();

        public DefaultServiceProvider()
        {
            _services[typeof(IServiceProvider)] = () => this;
        }

        public object GetService(Type serviceType)
        {
            return GetSingleService(serviceType) ?? GetMultiService(serviceType);
        }

        private object GetSingleService(Type serviceType)
        {
            Func<object> serviceFactory;
            return _services.TryGetValue(serviceType, out serviceFactory)
                ? serviceFactory.Invoke()
                : null;
        }

        private object GetMultiService(Type collectionType)
        {
            if (collectionType.IsGenericType &&
                collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                Type serviceType = collectionType.GetGenericArguments().Single();
                Type listType = typeof(List<>).MakeGenericType(serviceType);
                var services = (IList)Activator.CreateInstance(listType);

                Func<object> serviceFactory;
                if (_services.TryGetValue(serviceType, out serviceFactory))
                {
                    services.Add(serviceFactory());

                    List<Func<object>> prior;
                    if (_priorServices.TryGetValue(serviceType, out prior))
                    {
                        foreach (var factory in prior)
                        {
                            services.Add(factory());
                        }
                    }
                }
                return services;
            }
            return null;
        }

        public DefaultServiceProvider RemoveAll<T>()
        {
            _services.Remove(typeof(T));
            _priorServices.Remove(typeof(T));
            return this;
        }

        public DefaultServiceProvider AddInstance<TService>(object instance)
        {
            return Add(typeof(TService), () => instance);
        }

        public DefaultServiceProvider Add<TService, TImplementation>()
        {
            return Add(typeof(TService), typeof(TImplementation));
        }

        public DefaultServiceProvider Add(Type serviceType, Type implementationType)
        {
            Func<IServiceProvider, object> factory = ActivatorUtils.CreateFactory(implementationType);
            return Add(serviceType, () => factory(this));
        }

        public DefaultServiceProvider Add(Type serviceType, Func<object> serviceFactory)
        {
            Func<object> existing;
            if (_services.TryGetValue(serviceType, out existing))
            {
                List<Func<object>> prior;
                if (_priorServices.TryGetValue(serviceType, out prior))
                {
                    prior.Add(existing);
                }
                else
                {
                    prior = new List<Func<object>> { existing };
                    _priorServices.Add(serviceType, prior);
                }
            }
            _services[serviceType] = serviceFactory;
            return this;
        }
    }
}
