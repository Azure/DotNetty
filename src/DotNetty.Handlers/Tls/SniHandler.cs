// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Net.Security;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    public sealed class SniHandler : ByteToMessageDecoder
    {
        // Maximal number of ssl records to inspect before fallback to the default server TLS setting (aligned with netty) 
        const int MAX_SSL_RECORDS = 4;
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(SniHandler));
        readonly Func<Stream, SslStream> sslStreamFactory;
        readonly ServerTlsSniSettings serverTlsSniSettings;

        bool handshakeFailed;
        bool suppressRead;
        bool readPending;

        public SniHandler(ServerTlsSniSettings settings)
            : this(stream => new SslStream(stream, true), settings)
        {
        }

        public SniHandler(Func<Stream, SslStream> sslStreamFactory, ServerTlsSniSettings settings)
        {
            Contract.Requires(settings != null);
            Contract.Requires(sslStreamFactory != null);
            this.sslStreamFactory = sslStreamFactory;
            this.serverTlsSniSettings = settings;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (!this.suppressRead && !this.handshakeFailed)
            {
                int writerIndex = input.WriterIndex;
                Exception error = null;
                try
                {
                    bool continueLoop = true;
                    for (int i = 0; i < MAX_SSL_RECORDS && continueLoop; i++)
                    {
                        int readerIndex = input.ReaderIndex;
                        int readableBytes = writerIndex - readerIndex;
                        if (readableBytes < TlsUtils.SSL_RECORD_HEADER_LENGTH)
                        {
                            // Not enough data to determine the record type and length.
                            return;
                        }

                        int command = input.GetByte(readerIndex);
                        // tls, but not handshake command
                        switch (command)
                        {
                            case TlsUtils.SSL_CONTENT_TYPE_CHANGE_CIPHER_SPEC:
                            case TlsUtils.SSL_CONTENT_TYPE_ALERT:
                                int len = TlsUtils.GetEncryptedPacketLength(input, readerIndex);

                                // Not an SSL/TLS packet
                                if (len == TlsUtils.NOT_ENCRYPTED)
                                {
                                    this.handshakeFailed = true;
                                    var e = new NotSslRecordException(
                                        "not an SSL/TLS record: " + ByteBufferUtil.HexDump(input));
                                    input.SkipBytes(input.ReadableBytes);

                                    TlsUtils.NotifyHandshakeFailure(context, e);
                                    throw e;
                                }
                                if (len == TlsUtils.NOT_ENOUGH_DATA ||
                                    writerIndex - readerIndex - TlsUtils.SSL_RECORD_HEADER_LENGTH < len)
                                {
                                    // Not enough data
                                    return;
                                }

                                // increase readerIndex and try again.
                                input.SkipBytes(len);
                                continue;

                            case TlsUtils.SSL_CONTENT_TYPE_HANDSHAKE:
                                int majorVersion = input.GetByte(readerIndex + 1);

                                // SSLv3 or TLS
                                if (majorVersion == 3)
                                {
                                    int packetLength = input.GetUnsignedShort(readerIndex + 3) + TlsUtils.SSL_RECORD_HEADER_LENGTH;

                                    if (readableBytes < packetLength)
                                    {
                                        // client hello incomplete; try again to decode once more data is ready.
                                        return;
                                    }

                                    // See https://tools.ietf.org/html/rfc5246#section-7.4.1.2
                                    //
                                    // Decode the ssl client hello packet.
                                    // We have to skip bytes until SessionID (which sum to 43 bytes).
                                    //
                                    // struct {
                                    //    ProtocolVersion client_version;
                                    //    Random random;
                                    //    SessionID session_id;
                                    //    CipherSuite cipher_suites<2..2^16-2>;
                                    //    CompressionMethod compression_methods<1..2^8-1>;
                                    //    select (extensions_present) {
                                    //        case false:
                                    //            struct {};
                                    //        case true:
                                    //            Extension extensions<0..2^16-1>;
                                    //    };
                                    // } ClientHello;
                                    //

                                    int endOffset = readerIndex + packetLength;
                                    int offset = readerIndex + 43;

                                    if (endOffset - offset < 6)
                                    {
                                        continueLoop = false;
                                        break;
                                    }

                                    int sessionIdLength = input.GetByte(offset);
                                    offset += sessionIdLength + 1;

                                    int cipherSuitesLength = input.GetUnsignedShort(offset);
                                    offset += cipherSuitesLength + 2;

                                    int compressionMethodLength = input.GetByte(offset);
                                    offset += compressionMethodLength + 1;

                                    int extensionsLength = input.GetUnsignedShort(offset);
                                    offset += 2;
                                     int extensionsLimit = offset + extensionsLength;

                                    if (extensionsLimit > endOffset)
                                    {
                                        // Extensions should never exceed the record boundary.
                                        continueLoop = false;
                                        break;
                                    }

                                    for (;;)
                                    {
                                        if (extensionsLimit - offset < 4)
                                        {
                                            continueLoop = false;
                                            break;
                                        }

                                        int extensionType = input.GetUnsignedShort(offset);
                                        offset += 2;

                                        int extensionLength = input.GetUnsignedShort(offset);
                                        offset += 2;

                                        if (extensionsLimit - offset < extensionLength)
                                        {
                                            continueLoop = false;
                                            break;
                                        }

                                        // SNI
                                        // See https://tools.ietf.org/html/rfc6066#page-6
                                        if (extensionType == 0)
                                        {
                                            offset += 2;
                                            if (extensionsLimit - offset < 3)
                                            {
                                                continueLoop = false;
                                                break;
                                            }

                                            int serverNameType = input.GetByte(offset);
                                            offset++;

                                            if (serverNameType == 0)
                                            {
                                                int serverNameLength = input.GetUnsignedShort(offset);
                                                offset += 2;

                                                if (serverNameLength <= 0 || extensionsLimit - offset < serverNameLength)
                                                {
                                                    continueLoop = false;
                                                    break;
                                                }

                                                string hostname = input.ToString(offset, serverNameLength, Encoding.UTF8);
                                                //try
                                                //{
                                                //    select(ctx, IDN.toASCII(hostname,
                                                //                            IDN.ALLOW_UNASSIGNED).toLowerCase(Locale.US));
                                                //}
                                                //catch (Throwable t)
                                                //{
                                                //    PlatformDependent.throwException(t);
                                                //}

                                                var idn = new IdnMapping()
                                                {
                                                    AllowUnassigned = true
                                                };

                                                hostname = idn.GetAscii(hostname);
#if NETSTANDARD1_3
                                                // TODO: netcore does not have culture sensitive tolower()
                                                hostname = hostname.ToLowerInvariant();
#else
                                                hostname = hostname.ToLower(new CultureInfo("en-US"));
#endif
                                                this.Select(context, hostname);
                                                return;
                                            }
                                            else
                                            {
                                                // invalid enum value
                                                continueLoop = false;
                                                break;
                                            }
                                        }

                                        offset += extensionLength;
                                    }
                                }

                                break;
                            // Fall-through
                            default:
                                //not tls, ssl or application data, do not try sni
                                continueLoop = false;
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    error = e;

                    // unexpected encoding, ignore sni and use default
                    if (Logger.DebugEnabled)
                    {
                        Logger.Warn($"Unexpected client hello packet: {ByteBufferUtil.HexDump(input)}", e);
                    }
                }

                if (this.serverTlsSniSettings.DefaultServerHostName != null)
                {
                    // Just select the default server TLS setting
                    this.Select(context, this.serverTlsSniSettings.DefaultServerHostName); 
                }
                else
                {
                    this.handshakeFailed = true;
                    var e = new DecoderException($"failed to get the server TLS setting {error}");
                    TlsUtils.NotifyHandshakeFailure(context, e);
                    throw e;
                }
            }
        }

        async void Select(IChannelHandlerContext context, string hostName)
        {
            Contract.Requires(hostName != null);
            this.suppressRead = true;
            try
            {
                var serverTlsSetting = await this.serverTlsSniSettings.ServerTlsSettingMap(hostName);
                this.ReplaceHandler(context, serverTlsSetting);
            }
            catch (Exception ex)
            {
                this.ExceptionCaught(context, new DecoderException($"failed to get the server TLS setting for {hostName}, {ex}"));
            }
            finally
            {
                this.suppressRead = false;
                if (this.readPending)
                {
                    this.readPending = false;
                    context.Read();
                }
            }
        }

        void ReplaceHandler(IChannelHandlerContext context, ServerTlsSettings serverTlsSetting)
        {
            Contract.Requires(serverTlsSetting != null);
            var tlsHandler = new TlsHandler(this.sslStreamFactory, serverTlsSetting);
            context.Channel.Pipeline.Replace(this, nameof(TlsHandler), tlsHandler);
        }

        public override void Read(IChannelHandlerContext context)
        {
            if (this.suppressRead)
            {
                this.readPending = true;
            }
            else
            {
                base.Read(context);
            }
        }
    }
}
