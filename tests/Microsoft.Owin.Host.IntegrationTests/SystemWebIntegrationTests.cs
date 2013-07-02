﻿// <copyright file="SystemWebIntegration.cs" company="Microsoft Open Technologies, Inc.">
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
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Routing;
using Owin;
using Xunit;
using Xunit.Extensions;

#if NET40
namespace Microsoft.Owin.Host40.IntegrationTests
#else
namespace Microsoft.Owin.Host45.IntegrationTests
#endif
{
    public class SystemWebIntegrationTests : TestBase
    {
        public void ModuleAndHandlerEnvKeys(IAppBuilder app)
        {
            app.UseErrorPage();
            app.UseHandler((context, next) =>
            {
                context.Set("test.IntegratedPipleine", "before");
                return next(context)
                    .Then(() =>
                    {
                        Assert.Equal("after", context.Get<string>("test.IntegratedPipleine"));
                    });
            });

            RouteTable.Routes.MapOwinPath("/", app2 =>
            {
                app2.UseApp(context2 =>
                {
                    Assert.Equal("before", context2.Get<string>("test.IntegratedPipleine"));
                    context2.Set("test.IntegratedPipleine", "after");
                    return TaskHelpers.Completed();
                });
            });
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.SystemWeb")]
        public Task ModuleAndHandlerEnvKeys_SharedEnv(string serverName)
        {
            int port = RunWebServer(
                serverName,
                ModuleAndHandlerEnvKeys);

            return SendRequestAsync(port);
        }

        public void ModuleAndHandlerSyncException(IAppBuilder app)
        {
            app.UseErrorPage();
            app.UseHandler((context, next) =>
            {
                // Expect async exception from the handler.
                return next(context)
                    .Then(() =>
                    {
                        Assert.True(false, "Handler exception expected");
                    })
                    .Catch(catchInfo =>
                    {
                        Assert.IsType<NotFiniteNumberException>(catchInfo.Exception);
                        return catchInfo.Handled();
                    });
            });

            RouteTable.Routes.MapOwinPath("/", app2 =>
            {
                app2.UseApp(context2 =>
                {
                    // Sync exception should become async before module sees it.
                    throw new NotFiniteNumberException("Handler exception");
                });
            });
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.SystemWeb")]
        public Task ModuleAndHandlerSyncException_ModuleSeesHandlerException(string serverName)
        {
            int port = RunWebServer(
                serverName,
                ModuleAndHandlerSyncException);

            return SendRequestAsync(port);
        }

        public void ModuleAndHandlerAsyncException(IAppBuilder app)
        {
            app.UseErrorPage();
            app.UseHandler((context, next) =>
            {
                // Expect async exception from the handler.
                return next(context)
                    .Then(() =>
                    {
                        Assert.True(false, "Handler exception expected");
                    })
                    .Catch(catchInfo =>
                    {
                        Assert.IsType<NotFiniteNumberException>(catchInfo.Exception);
                        return catchInfo.Handled();
                    });
            });

            RouteTable.Routes.MapOwinPath("/", app2 =>
            {
                app2.UseApp(context2 =>
                {
                    return TaskHelpers.FromError(new NotFiniteNumberException("Handler exception"));
                });
            });
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.SystemWeb")]
        public Task ModuleAndHandlerAsyncException_ModuleSeesHandlerException(string serverName)
        {
            int port = RunWebServer(
                serverName,
                ModuleAndHandlerAsyncException);

            return SendRequestAsync(port);
        }

        private Task SendRequestAsync(int port)
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            return client.GetAsync("http://localhost:" + port)
                .Then(response =>
                {
                    Assert.Equal(String.Empty, response.Content.ReadAsStringAsync().Result);
                    response.EnsureSuccessStatusCode();
                });
        }
    }
}
