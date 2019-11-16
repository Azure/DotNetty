// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    /**
     * The state of the current detection.
     */
    public enum ProtocolDetectionState
    {
        /**
         * Need more data to detect the protocol.
         */
        NEEDS_MORE_DATA,

        /**
         * The data was invalid.
         */
        INVALID,

        /**
         * Protocol was detected,
         */
        DETECTED
    }
}
