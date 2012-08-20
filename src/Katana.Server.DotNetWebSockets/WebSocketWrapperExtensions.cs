﻿using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Net.WebSockets;
using System.Web.WebSockets;

namespace Katana.Server.DotNetWebSockets
{
    #pragma warning disable 811
    using WebSocketFunc =
        Func
        <
        // SendAsync
            Func
            <
                ArraySegment<byte> /* data */,
                int /* messageType */,
                bool /* endOfMessage */,
                CancellationToken /* cancel */,
                Task
            >,
        // ReceiveAsync
            Func
            <
                ArraySegment<byte> /* data */,
                CancellationToken /* cancel */,
                Task
                <
                    Tuple
                    <
                        int /* messageType */,
                        bool /* endOfMessage */,
                        int? /* count */,
                        int? /* closeStatus */,
                        string /* closeStatusDescription */
                    >
                >
            >,
        // CloseAsync
            Func
            <
                int /* closeStatus */,
                string /* closeDescription */,
                CancellationToken /* cancel */,
                Task
            >,
        // Complete
            Task
        >;
    #pragma warning restore 811

    using WebSocketReceiveTuple = Tuple
        <
            int /* messageType */,
            bool /* endOfMessage */,
            int? /* count */,
            int? /* closeStatus */,
            string /* closeStatusDescription */
        >;

    // This class is a 4.5 dependent middleware that HttpListener or AspNet can load at startup if they detect they are running on .NET 4.5.
    // This permits those server wrappers to remain as 4.0 components while still providing 4.5 functionality.
    public static class WebSocketWrapperExtensions
    {
        public static IAppBuilder UseAspNetWebSocketWrapper(this IAppBuilder builder)
        {
            return builder.UseFunc<AppDelegate>(WebSocketWrapperExtensions.AspNetMiddleware);
        }

        public static IAppBuilder UseHttpListenerWebSocketWrapper(this IAppBuilder builder)
        {
            return builder.UseFunc<AppDelegate>(WebSocketWrapperExtensions.HttpListenerMiddleware);
        }

        public static AppDelegate AspNetMiddleware(AppDelegate app)
        {
            return async call =>
            {
                HttpContextBase context = call.Environment.Get<HttpContextBase>("System.Web.HttpContextBase");

                bool isWebSocketRequest = false;
                if (context != null)
                {
                    // Not implemented by custom contexts or FakeN.Web.
                    try
                    {
                        if (context.IsWebSocketRequest)
                        {
                            isWebSocketRequest |= true;
                        }
                    }
                    catch (NotImplementedException) { }
                }

                if (isWebSocketRequest)
                {
                    bool doingWebSocketUpgrade = false;

                    // Replace owin.CallCompleted.  Asp.Net requires us to 'end' the request context before it will
                    // invoke the callback from AcceptWebSocketRequest. Replace it so the app doesn't pre-maturely cleanup.
                    Task callCompletion = call.Environment.Get<Task>(Constants.CallCompletedKey);
                    TaskCompletionSource<object> webSocketCompletion = new TaskCompletionSource<object>();
                    Task ignored = callCompletion.Then(() =>
                    {
                        if (!doingWebSocketUpgrade)
                        {
                            webSocketCompletion.TrySetResult(null);
                        }
                    })
                    .Catch(errorInfo => 
                    {
                        webSocketCompletion.TrySetException(errorInfo.Exception);
                        return errorInfo.Handled();
                    });
                    call.Environment[Constants.CallCompletedKey] = webSocketCompletion.Task;

                    call.Environment[Constants.WebSocketSupportKey] = Constants.WebSocketSupport;
                    ResultParameters result = await app(call);
                    WebSocketFunc webSocketFunc = result.Properties.Get<WebSocketFunc>(Constants.WebSocketFuncKey);
                    
                    // If the app requests a websocket upgrade, provide a fake body delegate to do so.
                    if (result.Status == 101 && webSocketFunc != null)
                    {
                        string subProtocol = null;
                        string[] subProtocols;
                        if (result.Headers.TryGetValue("Sec-WebSocket-Protocol", out subProtocols) && subProtocols.Length > 0)
                        {
                            subProtocol = subProtocols[0];
                            result.Headers.Remove("Sec-WebSocket-Protocol");
                        }

                        AspNetWebSocketOptions options = new AspNetWebSocketOptions();
                        options.SubProtocol = subProtocol;

                        result.Body = stream =>
                        {
                            doingWebSocketUpgrade = true;

                            try
                            {
                                context.AcceptWebSocketRequest(async webSocketContext =>
                                {
                                    try
                                    {
                                        OwinWebSocketWrapper wrapper = new OwinWebSocketWrapper(webSocketContext);
                                        await webSocketFunc(wrapper.SendAsync, wrapper.ReceiveAsync, wrapper.CloseAsync);
                                        await wrapper.CleanupAsync();
                                        webSocketCompletion.TrySetResult(null);
                                    }
                                    catch (Exception ex)
                                    {
                                        webSocketCompletion.TrySetException(ex);
                                    }
                                }, options);
                            }
                            catch (Exception ex)
                            {
                                webSocketCompletion.TrySetException(ex);
                                throw;
                            }

                            // Do not block.  AspNet requires the request to 'complete' before it will invoke the websocket callback.
                            return Task.FromResult<object>(null);
                        };
                    }

                    return result;
                }
                else
                {
                    return await app(call);
                }
            };
        }

        public static AppDelegate HttpListenerMiddleware(AppDelegate app)
        {
            return async call =>
            {
                HttpListenerContext context = call.Environment.Get<HttpListenerContext>("System.Net.HttpListenerContext");
                
                if (context != null && context.Request.IsWebSocketRequest)
                {
                    call.Environment[Constants.WebSocketSupportKey] = Constants.WebSocketSupport;
                    ResultParameters result = await app(call);
                    WebSocketFunc webSocketFunc = result.Properties.Get<WebSocketFunc>(Constants.WebSocketFuncKey);

                    // If the app requests a websocket upgrade, provide a fake body delegate to do so.
                    if (result.Status == 101 && webSocketFunc != null)
                    {
                        string subProtocol = null;
                        string[] subProtocols;
                        if (result.Headers.TryGetValue("Sec-WebSocket-Protocol", out subProtocols) && subProtocols.Length > 0)
                        {
                            subProtocol = subProtocols[0];
                            result.Headers.Remove("Sec-WebSocket-Protocol");
                        }

                        // TODO: Other parameters?

                        result.Body = async stream =>
                        {
                            WebSocketContext webSocketContext = await context.AcceptWebSocketAsync(subProtocol);
                            OwinWebSocketWrapper wrapper = new OwinWebSocketWrapper(webSocketContext);
                            await webSocketFunc(wrapper.SendAsync, wrapper.ReceiveAsync, wrapper.CloseAsync);
                            await wrapper.CleanupAsync();
                        };
                    }

                    return result;
                }
                else
                {
                    return await app(call);
                }
            };
        }
    }
}
