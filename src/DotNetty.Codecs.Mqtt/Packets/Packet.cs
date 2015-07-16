// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public abstract class Packet
    {
        public abstract PacketType PacketType { get; }

        public virtual bool Duplicate
        {
            get { return false; }
        }

        public virtual QualityOfService QualityOfService
        {
            get { return QualityOfService.AtMostOnce; }
        }

        public virtual bool RetainRequested
        {
            get { return false; }
        }

        public override string ToString()
        {
            return string.Format("{0}[Type={1}, QualityOfService={2}, Duplicate={3}, Retain={4}]", this.GetType().Name, this.PacketType, this.QualityOfService, this.Duplicate, this.RetainRequested);
        }
    }
}