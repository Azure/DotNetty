// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public enum QualityOfService
    {
        AtMostOnce = 0,
        AtLeastOnce = 0x1,
        ExactlyOnce = 0x2,
        Reserved = 0x3,
        Failure = 0x80
    }
}