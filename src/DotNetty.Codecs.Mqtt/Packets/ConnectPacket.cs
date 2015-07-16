// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    using DotNetty.Buffers;

    public sealed class ConnectPacket : Packet
    {
        public override PacketType PacketType
        {
            get { return PacketType.CONNECT; }
        }

        public string ProtocolName { get; set; }

        public int ProtocolVersion { get; set; }

        public bool CleanSession { get; set; }

        public bool HasWill
        {
            get { return this.WillTopic != null; }
        }

        public QualityOfService WillQualityOfService { get; set; }

        public bool WillRetain { get; set; }

        public bool HasPassword
        {
            get { return this.Password != null; }
        }

        public bool HasUsername
        {
            get { return this.Username != null; }
        }

        public int KeepAliveInSeconds { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string ClientId { get; set; }

        public string WillTopic { get; set; }

        public IByteBuffer WillMessage { get; set; }
    }
}