// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;

    /**
     * Result of detecting a protocol.
     *
     * @param <T> the type of the protocol
     */
    public sealed class ProtocolDetectionResult<T>
    {

        static readonly ProtocolDetectionResult<T> NEEDS_MORE_DATE = new ProtocolDetectionResult<T>(ProtocolDetectionState.NEEDS_MORE_DATA, default(T));
        static readonly ProtocolDetectionResult<T> INVALID = new ProtocolDetectionResult<T>(ProtocolDetectionState.INVALID, default(T));

        readonly ProtocolDetectionState state;
        readonly T result;

        /**
         * Returns a {@link ProtocolDetectionResult} that signals that more data is needed to detect the protocol.
         */
        public static ProtocolDetectionResult<T> NeedsMoreData()
        {
            return NEEDS_MORE_DATE;
        }

        /**
         * Returns a {@link ProtocolDetectionResult} that signals the data was invalid for the protocol.
         */
        public static ProtocolDetectionResult<T> Invalid()
        {
            return INVALID;
        }

        /**
         * Returns a {@link ProtocolDetectionResult} which holds the detected protocol.
         */
        public static ProtocolDetectionResult<T> Detected(T protocol)
        {
            if (protocol == null)
            {
                throw new ArgumentNullException(nameof(protocol));
            }
            return new ProtocolDetectionResult<T>(ProtocolDetectionState.DETECTED, protocol);
        }

        private ProtocolDetectionResult(ProtocolDetectionState state, T result)
        {
            this.state = state;
            this.result = result;
        }

        /**
         * Return the {@link ProtocolDetectionState}. If the state is {@link ProtocolDetectionState#DETECTED} you
         * can retrieve the protocol via {@link #detectedProtocol()}.
         */
        public ProtocolDetectionState State()
        {
            return this.state;
        }

        /**
         * Returns the protocol if {@link #state()} returns {@link ProtocolDetectionState#DETECTED}, otherwise {@code null}.
         */
        public T DetectedProtocol()
        {
            return this.result;
        }
    }
}
