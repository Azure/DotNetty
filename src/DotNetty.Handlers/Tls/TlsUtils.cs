// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// Utilities for TLS packets.
    static class TlsUtils
    {
        const int MAX_PLAINTEXT_LENGTH = 16 * 1024; // 2^14
        const int MAX_COMPRESSED_LENGTH = MAX_PLAINTEXT_LENGTH + 1024;
        const int MAX_CIPHERTEXT_LENGTH = MAX_COMPRESSED_LENGTH + 1024;

        // Header (5) + Data (2^14) + Compression (1024) + Encryption (1024) + MAC (20) + Padding (256)
        public const int MAX_ENCRYPTED_PACKET_LENGTH = MAX_CIPHERTEXT_LENGTH + 5 + 20 + 256;


        /// change cipher spec
        public const int SSL_CONTENT_TYPE_CHANGE_CIPHER_SPEC = 20;

        /// alert
        public const int SSL_CONTENT_TYPE_ALERT = 21;

        /// handshake
        public const int SSL_CONTENT_TYPE_HANDSHAKE = 22;

        /// application data
        public const int SSL_CONTENT_TYPE_APPLICATION_DATA = 23;

        /// the length of the ssl record header (in bytes)
        public const int SSL_RECORD_HEADER_LENGTH = 5;

        // Not enough data in buffer to parse the record length
        public const int NOT_ENOUGH_DATA = -1;

        // data is not encrypted
        public const int NOT_ENCRYPTED = -2;

        /// <summary>
        ///     Return how much bytes can be read out of the encrypted data. Be aware that this method will not increase
        ///     the readerIndex of the given <see cref="IByteBuffer"/>.
        /// </summary>
        /// <param name="buffer">
        ///     The <see cref="IByteBuffer"/> to read from. Be aware that it must have at least
        ///     <see cref="SSL_RECORD_HEADER_LENGTH"/> bytes to read,
        ///     otherwise it will throw an <see cref="ArgumentException"/>.
        /// </param>
        /// <param name="offset">Offset to record start.</param>
        /// <returns>
        ///     The length of the encrypted packet that is included in the buffer. This will
        ///     return <c>-1</c> if the given <see cref="IByteBuffer"/> is not encrypted at all.
        /// </returns>
        public static int GetEncryptedPacketLength(IByteBuffer buffer, int offset)
        {
            int packetLength = 0;

            // SSLv3 or TLS - Check ContentType
            switch (buffer.GetByte(offset))
            {
                case SSL_CONTENT_TYPE_CHANGE_CIPHER_SPEC:
                case SSL_CONTENT_TYPE_ALERT:
                case SSL_CONTENT_TYPE_HANDSHAKE:
                case SSL_CONTENT_TYPE_APPLICATION_DATA:
                    break;
                default:
                    // SSLv2 or bad data
                    return -1;
            }
            // SSLv3 or TLS - Check ProtocolVersion
            int majorVersion = buffer.GetByte(offset + 1);
            if (majorVersion == 3)
            {
                // SSLv3 or TLS
                packetLength = buffer.GetUnsignedShort(offset + 3) + SSL_RECORD_HEADER_LENGTH;
                if (packetLength <= SSL_RECORD_HEADER_LENGTH)
                {
                    // Neither SSLv3 or TLSv1 (i.e. SSLv2 or bad data)
                    return -1;
                }
            }
            else
            {
                // Neither SSLv3 or TLSv1 (i.e. SSLv2 or bad data)
                return -1;
            }

            return packetLength;
        }

        public static void NotifyHandshakeFailure(IChannelHandlerContext ctx, Exception cause)
        {
            // We have may haven written some parts of data before an exception was thrown so ensure we always flush.
            // See https://github.com/netty/netty/issues/3900#issuecomment-172481830
            ctx.Flush();
            ctx.FireUserEventTriggered(new TlsHandshakeCompletionEvent(cause));
            ctx.CloseAsync();
        }
    }
}