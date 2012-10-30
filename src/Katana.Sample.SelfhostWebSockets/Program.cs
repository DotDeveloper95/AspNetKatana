﻿// <copyright file="Program.cs" company="Katana contributors">
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
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Gate.Middleware;
using Katana.Engine;
using Katana.Engine.Settings;
using Microsoft.Owin.WebSockets.Samples;
using Owin;

namespace Katana.Sample.SelfhostWebSockets
{
    internal class Program
    {
        // Use this project to F5 test different applications and servers together.
        public static void Main(string[] args)
        {
            var settings = new KatanaSettings();

            KatanaEngine engine = new KatanaEngine(settings);

            var parameters = new StartParameters
            {
                Server = "Microsoft.Owin.Host.HttpListener",
                App = "Katana.Sample.SelfhostWebSockets.Program.Configuration", // Application
                Url = "http://+:8080/",
                /*
                OutputFile = string.Empty,
                Scheme = arguments.Scheme,
                Host = arguments.Host,
                Port = arguments.Port,
                Path = arguments.Path,
                 */
            };

            IDisposable server = engine.Start(new StartContext { Parameters = parameters });

            Console.WriteLine("Running, press any key to exit");

            RunSampleClient();

            Console.ReadKey();
        }

        public void Configuration(IAppBuilder builder)
        {
            var addresses = builder.Properties.Get<IList<IDictionary<string, object>>>("host.Addresses");
            addresses.Add(new Dictionary<string, object>
            {
                { "scheme", "http" },
                { "host", "*" },
                { "port", "8081" },
                { "path", "/hello" },
            });

            builder.UseShowExceptions();
            builder.Use(typeof(WebSocketEcho));
        }

        private static async void RunSampleClient()
        {
            try
            {
                ClientWebSocket webSocket = new ClientWebSocket();
                webSocket.Options.AddSubProtocol("Echo");
                webSocket.Options.AddSubProtocol("Chat");

                Console.WriteLine("Connecting");
                await webSocket.ConnectAsync(new Uri("ws://localhost:8080/"), CancellationToken.None);
                Console.WriteLine("Connected, sub-protocol: " + webSocket.SubProtocol);

                string message = "Hello World";
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);

                await webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                byte[] buffer = new byte[1024];
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                Console.WriteLine("MessageType: " + result.MessageType);
                Console.WriteLine("Echo: " + Encoding.UTF8.GetString(buffer, 0, result.Count));

                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "My Close Message", CancellationToken.None);

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                Console.WriteLine("MessageType: " + result.MessageType);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
