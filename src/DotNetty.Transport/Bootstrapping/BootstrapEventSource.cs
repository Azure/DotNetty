// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System;
    using System.Diagnostics.Tracing;

    [EventSource(Name = "DotNetty-Bootstrap")]
    public class BootstrapEventSource : EventSource
    {
        const int VerboseEventId = 1;
        const int InfoEventId = 2;
        const int WarningEventId = 3;
        const int ErrorEventId = 4;

        public static readonly BootstrapEventSource Log = new BootstrapEventSource();

        BootstrapEventSource()
        {
        }

        public bool IsVerboseEnabled
        {
            get { return this.IsEnabled(EventLevel.Verbose, EventKeywords.None); }
        }

        public bool IsInfoEnabled
        {
            get { return this.IsEnabled(EventLevel.Informational, EventKeywords.None); }
        }

        public bool IsWarningEnabled
        {
            get { return this.IsEnabled(EventLevel.Warning, EventKeywords.None); }
        }

        public bool IsErrorEnabled
        {
            get { return this.IsEnabled(EventLevel.Error, EventKeywords.None); }
        }

        [Event(VerboseEventId, Level = EventLevel.Verbose)]
        public void Verbose(string message, string info)
        {
            if (this.IsVerboseEnabled)
            {
                this.WriteEvent(VerboseEventId, message, info);
            }
        }

        [Event(InfoEventId, Level = EventLevel.Informational)]
        public void Info(string message, string info)
        {
            if (this.IsInfoEnabled)
            {
                this.WriteEvent(InfoEventId, message, info);
            }
        }

        [NonEvent]
        public void Warning(string message)
        {
            this.Warning(message, string.Empty);
        }

        [NonEvent]
        public void Warning(string message, Exception exception)
        {
            if (this.IsWarningEnabled)
            {
                this.Warning(message, exception == null ? string.Empty : exception.ToString());
            }
        }

        [Event(WarningEventId, Level = EventLevel.Warning)]
        public void Warning(string message, string exception)
        {
            if (this.IsWarningEnabled)
            {
                this.WriteEvent(WarningEventId, message, exception);
            }
        }

        [NonEvent]
        public void Error(string message, Exception exception)
        {
            if (this.IsErrorEnabled)
            {
                this.Error(message, exception == null ? string.Empty : exception.ToString());
            }
        }

        [Event(ErrorEventId, Level = EventLevel.Error)]
        public void Error(string message, string exception)
        {
            if (this.IsErrorEnabled)
            {
                this.WriteEvent(ErrorEventId, message, exception);
            }
        }
    }
}