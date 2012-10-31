﻿// <copyright file="OwinHttpListenerTests.cs" company="Katana contributors">
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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Owin.Host.HttpListener.Tests
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    // These tests measure that the core HttpListener wrapper functions as expected in normal and exceptional scenarios.
    // NOTE: These tests require SetupProject.bat to be run as admin from a VS command prompt once per machine.
    public class OwinHttpListenerTests
    {
        private static readonly string[] HttpServerAddress = new string[] { "http://+:8080/BaseAddress/" };
        private const string HttpClientAddress = "http://localhost:8080/BaseAddress/";
        private static readonly string[] HttpsServerAddress = new string[] { "https://+:9090/BaseAddress/" };
        private const string HttpsClientAddress = "https://localhost:9090/BaseAddress/";

        private readonly AppFunc _notImplemented = env => { throw new NotImplementedException(); };

        [Fact]
        public void OwinHttpListener_CreatedStartedStoppedDisposed_Success()
        {
            OwinHttpListener listener = new OwinHttpListener(_notImplemented, HttpServerAddress, null);
            using (listener)
            {
                listener.Start();
                listener.Stop();
            }
        }

        // HTTPS requires pre-configuring the server cert to work
        [Fact]
        public void OwinHttpListener_HttpsCreatedStartedStoppedDisposed_Success()
        {
            OwinHttpListener listener = new OwinHttpListener(_notImplemented, HttpsServerAddress, null);
            using (listener)
            {
                listener.Start();
                listener.Stop();
            }
        }

        [Fact]
        public void Ctor_NullDelegate_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new OwinHttpListener(null, HttpServerAddress, null));
        }

        [Fact]
        public void Ctor_BadServerAddress_Throws()
        {
            Assert.Throws<ArgumentException>(() => 
                new OwinHttpListener(_notImplemented, new string[]
                {
                    "http://host:9090/BadPathDoesntEndInSlash"
                }, null));
        }

        [Fact]
        public async Task EndToEnd_GetRequest_Success()
        {
            OwinHttpListener listener = new OwinHttpListener(env => TaskHelpers.Completed(), HttpServerAddress, null);
            HttpResponseMessage response = await SendGetRequest(listener, HttpClientAddress);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, response.Content.Headers.ContentLength.Value);
        }

        [Fact]
        public async Task EndToEnd_SingleThreadedTwoGetRequests_Success()
        {
            OwinHttpListener listener = new OwinHttpListener(env => TaskHelpers.Completed(), HttpServerAddress, null);
            using (listener)
            {
                listener.Start();
                HttpClient client = new HttpClient();
                string result = await client.GetStringAsync(HttpClientAddress);
                Assert.Equal(string.Empty, result);
                result = await client.GetStringAsync(HttpClientAddress);
                Assert.Equal(string.Empty, result);
            }
        }

        [Fact]
        public async Task EndToEnd_GetRequestWithDispose_Success()
        {
            bool callCancelled = false;

            OwinHttpListener listener = new OwinHttpListener(
                env =>
                {
                    GetCallCancelled(env).Register(() => callCancelled = true);
                    return TaskHelpers.Completed();
                },
                HttpServerAddress, null);

            HttpResponseMessage response = await SendGetRequest(listener, HttpClientAddress);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, response.Content.Headers.ContentLength.Value);
            await Task.Delay(1);
            Assert.False(callCancelled);
        }

        [Fact]
        public async Task EndToEnd_HttpsGetRequest_Success()
        {
            OwinHttpListener listener = new OwinHttpListener(
                env =>
                {
                    object obj;
                    Assert.True(env.TryGetValue("ssl.ClientCertificate", out obj));
                    Assert.NotNull(obj);
                    Assert.IsType<X509Certificate2>(obj);
                    return TaskHelpers.Completed();
                },
                HttpsServerAddress, null);

            HttpResponseMessage response = await SendGetRequest(listener, HttpsClientAddress, ClientCertificateOption.Automatic);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, response.Content.Headers.ContentLength.Value);
        }

        [Fact]
        public async Task EndToEnd_HttpsGetRequestNoClientCert_Success()
        {
            OwinHttpListener listener = new OwinHttpListener(
                env =>
                {
                    object obj;
                    Assert.False(env.TryGetValue("owin.ClientCertificate", out obj));
                    return TaskHelpers.Completed();
                },
                HttpsServerAddress, null);

            HttpResponseMessage response = await SendGetRequest(listener, HttpsClientAddress, ClientCertificateOption.Manual);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, response.Content.Headers.ContentLength.Value);
        }

        [Fact]
        public async Task AppDelegate_ThrowsSync_500Error()
        {
            OwinHttpListener listener = new OwinHttpListener(_notImplemented, HttpServerAddress, null);
            HttpResponseMessage response = await SendGetRequest(listener, HttpClientAddress);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(0, response.Content.Headers.ContentLength.Value);
        }

        [Fact]
        public async Task AppDelegate_ReturnsExceptionAsync_500Error()
        {
            bool callCancelled = false;

            OwinHttpListener listener = new OwinHttpListener(
                async env =>
                {
                    GetCallCancelled(env).Register(() => callCancelled = true);
                    await Task.Delay(1);
                    throw new NotImplementedException();
                },
                HttpServerAddress, null);

            HttpResponseMessage response = await SendGetRequest(listener, HttpClientAddress);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(0, response.Content.Headers.ContentLength.Value);
            Assert.True(callCancelled);
        }

        [Fact]
        public async Task Body_PostEchoRequest_Success()
        {
            bool callCancelled = false;

            OwinHttpListener listener = new OwinHttpListener(
                env =>
                {
                    GetCallCancelled(env).Register(() => callCancelled = true);
                    var requestHeaders = env.Get<IDictionary<string, string[]>>("owin.RequestHeaders");
                    var responseHeaders = env.Get<IDictionary<string, string[]>>("owin.ResponseHeaders");
                    responseHeaders.Add("Content-Length", requestHeaders["Content-Length"]);

                    Stream requestStream = env.Get<Stream>("owin.RequestBody");
                    Stream responseStream = env.Get<Stream>("owin.ResponseBody");

                    return requestStream.CopyToAsync(responseStream, 1024);
                },
                HttpServerAddress, null);

            using (listener)
            {
                listener.Start();
                HttpClient client = new HttpClient();
                string dataString = "Hello World";
                HttpResponseMessage result = await client.PostAsync(HttpClientAddress, new StringContent(dataString));
                result.EnsureSuccessStatusCode();
                Assert.Equal(dataString.Length, result.Content.Headers.ContentLength.Value);
                Assert.Equal(dataString, await result.Content.ReadAsStringAsync());
                Assert.False(callCancelled);
            }
        }

        [Fact]
        public void BodyDelegate_ThrowsSync_ConnectionClosed()
        {
            bool callCancelled = false;
            OwinHttpListener listener = new OwinHttpListener(
                env =>
                {
                    GetCallCancelled(env).Register(() => callCancelled = true);
                    var responseHeaders = env.Get<IDictionary<string, string[]>>("owin.ResponseHeaders");
                    responseHeaders.Add("Content-Length", new string[] { "10" });

                    Stream responseStream = env.Get<Stream>("owin.ResponseBody");
                    responseStream.WriteByte(0xFF);

                    throw new NotImplementedException();
                },
                HttpServerAddress, null);

            try
            {
                // TODO: XUnit 2.0 adds support for Assert.Throws<...>(async () => await myTask);
                // that way we can specify the correct exception type.
                Assert.Throws<AggregateException>(() => SendGetRequest(listener, HttpClientAddress).Result);
            }
            finally
            {
                Assert.True(callCancelled);
            }
        }

        [Fact]
        public void BodyDelegate_ThrowsAsync_ConnectionClosed()
        {
            bool callCancelled = false;

            OwinHttpListener listener = new OwinHttpListener(
                env =>
                {
                    GetCallCancelled(env).Register(() => callCancelled = true);
                    var responseHeaders = env.Get<IDictionary<string, string[]>>("owin.ResponseHeaders");
                    responseHeaders.Add("Content-Length", new string[] { "10" });

                    Stream responseStream = env.Get<Stream>("owin.ResponseBody");
                    responseStream.WriteByte(0xFF);

                    return TaskHelpers.FromError(new NotImplementedException());
                },
                HttpServerAddress, null);

            try
            {
                Assert.Throws<AggregateException>(() => SendGetRequest(listener, HttpClientAddress).Result);
            }
            finally
            {
                Assert.True(callCancelled);
            }
        }

        [Fact]
        public void TimeoutArgs_Default_Infinite()
        {
            OwinHttpListener listener = new OwinHttpListener(_notImplemented, HttpServerAddress, null);
            Assert.Equal(Timeout.InfiniteTimeSpan, listener.MaxRequestLifetime);
        }

        [Fact]
        public void TimeoutArgs_Negative_Throws()
        {
            OwinHttpListener listener = new OwinHttpListener(_notImplemented, HttpServerAddress, null);
            Assert.Throws<ArgumentOutOfRangeException>(() => listener.MaxRequestLifetime = TimeSpan.FromSeconds(-1));
        }

        [Fact]
        public void TimeoutArgs_Infiniate_Success()
        {
            OwinHttpListener listener = new OwinHttpListener(_notImplemented, HttpServerAddress, null);
            listener.MaxRequestLifetime = Timeout.InfiniteTimeSpan;
            Assert.Equal(Timeout.InfiniteTimeSpan, listener.MaxRequestLifetime);
        }

        [Fact]
        public void TimeoutArgs_Huge_Success()
        {
            OwinHttpListener listener = new OwinHttpListener(_notImplemented, HttpServerAddress, null);
            listener.MaxRequestLifetime = TimeSpan.FromSeconds(int.MaxValue);
            Assert.Equal(int.MaxValue, listener.MaxRequestLifetime.TotalSeconds);
        }

        [Fact]
        public async Task Timeout_GetRequestWithinTimeout_Success()
        {
            OwinHttpListener listener = new OwinHttpListener(env => TaskHelpers.Completed(), HttpServerAddress, null);
            listener.MaxRequestLifetime = TimeSpan.FromSeconds(1);

            HttpResponseMessage response = await SendGetRequest(listener, HttpClientAddress);
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task Timeout_GetRequestTimeoutDurringRequest_500Error()
        {
            OwinHttpListener listener = new OwinHttpListener(
                async env => { await Task.Delay(100); },
                HttpServerAddress, null);
            listener.MaxRequestLifetime = TimeSpan.FromMilliseconds(1);

            HttpResponseMessage result = await SendGetRequest(listener, HttpClientAddress);
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
            Assert.Equal(0, result.Content.Headers.ContentLength.Value);
        }

        [Fact]
        public async Task Timeout_GetRequestTimeoutDurringResponse_ConnectionClose()
        {
            OwinHttpListener listener = new OwinHttpListener(
                async env =>
                {
                    var responseHeaders = env.Get<IDictionary<string, string[]>>("owin.ResponseHeaders");
                    responseHeaders.Add("Content-Length", new string[] { "10" });

                    Stream responseStream = env.Get<Stream>("owin.ResponseBody");

                    await Task.Delay(1000);
                    await responseStream.WriteAsync(new byte[10], 0, 10);
                },
                HttpServerAddress, null);

            listener.MaxRequestLifetime = TimeSpan.FromMilliseconds(1);
            HttpResponseMessage response = await SendGetRequest(listener, HttpClientAddress);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        private static CancellationToken GetCallCancelled(IDictionary<string, object> env)
        {
            return env.Get<CancellationToken>("owin.CallCancelled");
        }

        private Task<HttpResponseMessage> SendGetRequest(OwinHttpListener listener, string address)
        {
            return SendGetRequest(listener, address, ClientCertificateOption.Automatic);
        }

        private async Task<HttpResponseMessage> SendGetRequest(OwinHttpListener listener, string address, ClientCertificateOption certOptions)
        {
            using (listener)
            {
                listener.Start();

                WebRequestHandler handler = new WebRequestHandler();

                // Ignore server cert errors.
                handler.ServerCertificateValidationCallback = (a, b, c, d) => true;
                handler.ClientCertificateOptions = certOptions;

                HttpClient client = new HttpClient(handler);
                return await client.GetAsync(address);
            }
        }
    }
}
