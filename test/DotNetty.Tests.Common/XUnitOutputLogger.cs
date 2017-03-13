// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System;
    using Microsoft.Extensions.Logging;
    using Xunit.Abstractions;

    public class XUnitOutputLogger : ILogger
    {
        static readonly object LockObject = new object();
        readonly string categoryName;
        readonly ITestOutputHelper output;

        public XUnitOutputLogger(string categoryName, ITestOutputHelper output)
        {
            this.categoryName = categoryName;
            this.output = output;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            lock (LockObject)
            {
                try
                {
                    this.output.WriteLine($"{DateTime.Now}\t[{logLevel.ToString()}]\t{eventId.Name}\t{this.categoryName}\t{formatter(state, exception)}\t{exception}");
                }
                catch (Exception ex)
                {
                    try
                    {
                        this.output.WriteLine("Failed to log event: " + ex);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) => new Disposable(() => { });
    }
}