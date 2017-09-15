// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Timeout
{
    /// <summary>
    /// A user event triggered by <see cref="IdleStateHandler"/> when a <see cref="DotNetty.Transport.Channels.IChannel"/> is idle.
    /// </summary>
    public class IdleStateEvent
    {
        public static readonly IdleStateEvent FirstReaderIdleStateEvent = new IdleStateEvent(IdleState.ReaderIdle, true);
        public static readonly IdleStateEvent ReaderIdleStateEvent = new IdleStateEvent(IdleState.ReaderIdle, false);
        public static readonly IdleStateEvent FirstWriterIdleStateEvent = new IdleStateEvent(IdleState.WriterIdle, true);
        public static readonly IdleStateEvent WriterIdleStateEvent = new IdleStateEvent(IdleState.WriterIdle, false);
        public static readonly IdleStateEvent FirstAllIdleStateEvent = new IdleStateEvent(IdleState.AllIdle, true);
        public static readonly IdleStateEvent AllIdleStateEvent = new IdleStateEvent(IdleState.AllIdle, false);

        /// <summary>
        /// Constructor for sub-classes.
        /// </summary>
        /// <param name="state">the <see cref="IdleStateEvent"/> which triggered the event.</param>
        /// <param name="first"><code>true</code> if its the first idle event for the <see cref="IdleStateEvent"/>.</param>
        protected IdleStateEvent(IdleState state, bool first)
        {
            this.State = state;
            this.First = first;
        }

        /// <summary>
        /// Returns the idle state.
        /// </summary>
        /// <value>The state.</value>
        public IdleState State
        {
            get;
            private set;
        }

        /// <summary>
        /// Returns <code>true</code> if this was the first event for the <see cref="IdleState"/>
        /// </summary>
        /// <returns><code>true</code> if first; otherwise, <code>false</code>.</returns>
        public bool First
        {
            get;
            private set;
        }
    }
}

