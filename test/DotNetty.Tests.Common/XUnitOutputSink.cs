// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
    using Xunit.Abstractions;

    public class XUnitOutputSink : IObserver<EventEntry>
    {
        static readonly object LockObject = new object();
        readonly ITestOutputHelper output;
        readonly IEventTextFormatter formatter;

        public XUnitOutputSink(ITestOutputHelper output, IEventTextFormatter formatter)
        {
            this.output = output;
            this.formatter = formatter;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(EventEntry value)
        {
            string formattedValue = value.TryFormatAsString(this.formatter);
            if (formattedValue == null)
            {
                return;
            }
            this.OnNext(formattedValue);
        }

        void OnNext(string entry)
        {
            lock (LockObject)
            {
                try
                {
                    this.output.WriteLine(entry);
                }
                catch (Exception ex)
                {
                    SemanticLoggingEventSource.Log.CustomSinkUnhandledFault(ex.ToString());
                }
            }
        }
    }

    public static class XUnitOutputLog
    {
        public static SinkSubscription<XUnitOutputSink> LogToTestOutput(this IObservable<EventEntry> eventStream, ITestOutputHelper output, IEventTextFormatter formatter = null)
        {
            formatter = formatter ?? new EventTextFormatter();
            var sink = new XUnitOutputSink(output, formatter);
            return new SinkSubscription<XUnitOutputSink>(eventStream.Subscribe(sink), sink);
        }
    }
}