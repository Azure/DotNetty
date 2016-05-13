// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public class ChannelGroupException : ChannelException, IEnumerable<KeyValuePair<IChannel, Exception>>
    {
        readonly IReadOnlyCollection<KeyValuePair<IChannel, Exception>> failed;

        public ChannelGroupException(IList<KeyValuePair<IChannel, Exception>> exceptions)
        {
            if (exceptions == null)
            {
                throw new ArgumentNullException(nameof(exceptions));
            }
            if (exceptions.Count == 0)
            {
                throw new ArgumentException("excetpions must be not empty.");
            }
            this.failed = new ReadOnlyCollection<KeyValuePair<IChannel, Exception>>(exceptions);
        }

        public IEnumerator<KeyValuePair<IChannel, Exception>> GetEnumerator() => this.failed.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.failed.GetEnumerator();
    }
}