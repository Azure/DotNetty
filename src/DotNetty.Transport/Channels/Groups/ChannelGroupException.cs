using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DotNetty.Transport.Channels.Groups
{
    public class ChannelGroupException : ChannelException, IEnumerable<KeyValuePair<IChannel, Exception>>
    {
        readonly IReadOnlyCollection<KeyValuePair<IChannel, Exception>> failed;

        public ChannelGroupException(IList<KeyValuePair<IChannel, Exception>> exceptions)
        {
            if (exceptions == null)
            {
                throw new ArgumentNullException("exceptions");
            }
            if (exceptions.Count == 0)
            {
                throw new ArgumentException("excetpions must be not empty.");
            }
            this.failed = new ReadOnlyCollection<KeyValuePair<IChannel, Exception>>(exceptions);
        }

        public IEnumerator<KeyValuePair<IChannel, Exception>> GetEnumerator()
        {
            return this.failed.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.failed.GetEnumerator();
        }
    }
}