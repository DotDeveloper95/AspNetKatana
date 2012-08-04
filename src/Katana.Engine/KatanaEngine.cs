﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Katana.Engine.Utils;
using Owin;
using Katana.Engine.Settings;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Katana.Engine
{
    public class KatanaEngine : IKatanaEngine
    {
        private readonly IKatanaSettings _settings;

        public KatanaEngine(IKatanaSettings settings)
        {
            _settings = settings;
        }

        public IDisposable Start(StartInfo info)
        {
            ResolveOutput(info);
            InitializeBuilder(info);
            ResolveServerFactory(info);
            InitializeServerFactory(info);
            ResolveApp(info);
            return StartServer(info);
        }

        void ResolveOutput(StartInfo info)
        {
            if (info.Output != null) return;

            if (!string.IsNullOrWhiteSpace(info.OutputFile))
            {
                info.Output = new StreamWriter(info.OutputFile, true);
            }
            else
            {
                info.Output = _settings.DefaultOutput;
            }
        }

        private void InitializeBuilder(StartInfo info)
        {
            if (info.Builder == null)
            {
                info.Builder = _settings.BuilderFactory.Invoke();
            }

            var portString = (info.Port ?? _settings.DefaultPort ?? 8080).ToString(CultureInfo.InvariantCulture);

            var address = new Dictionary<string, object>
            {
                {"scheme", info.Scheme ?? _settings.DefaultScheme},
                {"host", info.Host ?? _settings.DefaultHost},
                {"port", portString},
                {"path", info.Path ?? ""},
            };

            info.Builder.Properties["host.Addresses"] = new List<IDictionary<string, object>> { address };
            info.Builder.Properties["host.TraceOutput"] = info.Output;
        }

        private void ResolveServerFactory(StartInfo info)
        {
            if (info.ServerFactory != null) return;

            var serverName = info.Server ?? _settings.DefaultServer;

            // TODO: error message for server assembly not found
            var serverAssembly = Assembly.Load(_settings.ServerAssemblyPrefix + serverName);

            // TODO: error message for assembly does not have ServerFactory attribute
            info.ServerFactory = serverAssembly.GetCustomAttributes(false)
                .Cast<Attribute>()
                .Single(x => x.GetType().Name == "ServerFactory");
        }

        private void InitializeServerFactory(StartInfo info)
        {
            var initializeMethod = info.ServerFactory.GetType().GetMethod("Initialize", new[] { typeof(IDictionary<string, object>) });
            if (initializeMethod != null)
            {
                initializeMethod.Invoke(info.ServerFactory, new object[] { info.Builder.Properties });
            }
        }

        private void ResolveApp(StartInfo info)
        {
            if (info.App == null)
            {
                var startup = _settings.Loader.Load(info.Startup);
                info.App = info.Builder.Build<AppDelegate>(startup);
            }
            info.App = info.Builder.Build<AppDelegate>(
                builder => builder
                    .Use<AppDelegate>(app => Encapsulate.Middleware(app, info.Output))
                    .Use<object>(_ => info.App)
                );
        }

        private IDisposable StartServer(StartInfo info)
        {
            // TODO: Katana#2: Need the ability to detect multiple Create methods, AppTaskDelegate and AppDelegate,
            // then choose the most appropriate one based on the next item in the pipeline.
            var serverFactoryMethod = info.ServerFactory.GetType().GetMethod("Create");
            if (serverFactoryMethod == null)
            {
                throw new ApplicationException("ServerFactory must a single public Create method");
            }
            var parameters = serverFactoryMethod.GetParameters();
            if (parameters.Length != 2)
            {
                throw new ApplicationException("ServerFactory Create method must take two parameters");
            }
            if (parameters[1].ParameterType != typeof(IDictionary<string, object>))
            {
                throw new ApplicationException("ServerFactory Create second parameter must be of type IDictionary<string,object>");
            }

            // let's see if we don't have the correct callable type for this server factory
            var isExpectedAppType = parameters[0].ParameterType.IsInstanceOfType(info.App);
            if (!isExpectedAppType)
            {
                var buildMethod = typeof(IAppBuilder).GetMethod("Build");
                var buildMethodForType = buildMethod.MakeGenericMethod(parameters[0].ParameterType);

                Action<IAppBuilder> passAppToBuilder = builder => builder.Use<object>(_ => info.App);
                info.App = buildMethodForType.Invoke(info.Builder, new object[] { passAppToBuilder });
            }

            return (IDisposable)serverFactoryMethod.Invoke(info.ServerFactory, new[] { info.App, info.Builder.Properties });
        }
    }
}
