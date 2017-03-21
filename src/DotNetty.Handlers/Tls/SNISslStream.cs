// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Handlers.Tls
{
    using System;
    using System.IO;
    using System.Net.Security;
    using System.Text;

    public class SniSslStream : SslStream
    {
        const int MaxPeekSize = 512;
        readonly PeekableStream innerStream;

        public SniSslStream(Stream innerStream)
            : base(innerStream)
        {
            this.innerStream = new PeekableStream(innerStream, MaxPeekSize);
        }

        public SniSslStream(Stream innerStream, bool leaveInnerStreamOpen)
            : base(innerStream, leaveInnerStreamOpen)
        {
            this.innerStream = new PeekableStream(innerStream, MaxPeekSize); ;
        }

        public SniSslStream(Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback)
            : base(innerStream, leaveInnerStreamOpen, userCertificateValidationCallback)
        {
            this.innerStream = new PeekableStream(innerStream, MaxPeekSize); ;
        }

        public SniSslStream(Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback, LocalCertificateSelectionCallback userCertificateSelectionCallback)
            : base(innerStream, leaveInnerStreamOpen, userCertificateValidationCallback, userCertificateSelectionCallback)
        {
            this.innerStream = new PeekableStream(innerStream, MaxPeekSize); ;
        }

        public SniSslStream(Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback, LocalCertificateSelectionCallback userCertificateSelectionCallback, EncryptionPolicy encryptionPolicy)
            : base(innerStream, leaveInnerStreamOpen, userCertificateValidationCallback, userCertificateSelectionCallback, encryptionPolicy)
        {
            this.innerStream = new PeekableStream(innerStream, MaxPeekSize); ;
        }

        public string GetServerName()
        {
            try
            {
                // Using https://tools.ietf.org/html/rfc5246

                var buffer = new byte[5];
                if (this.innerStream.Peek(buffer, 0, 5) != 5)
                    return null;

                var contentType = (ContentType)buffer[0];
                if (contentType != ContentType.Handshake)
                {
                    return null;
                }
                // byte 1 and 2 are protocol version. Should accept 0x03xx
                if (buffer[1] != 3)
                {
                    return null;
                }
                ushort length = (ushort)(buffer[3] * 256 + buffer[4]);
                if (length > MaxPeekSize)
                {
                    // size too long compared as expected
                    return null;
                }
                var fragment = new byte[length];
                if (this.innerStream.Peek(fragment, 0, length) != length)
                    return null;

                var handshakeType = (HandshakeType)fragment[0];
                if (handshakeType != HandshakeType.ClientHello)
                {
                    return null;
                }

                uint size;
                /* bytes in message: uint24 length */
                size = (uint)(fragment[1] * 256 * 256 + fragment[2] * 256 + fragment[3]);

                if (size > fragment.Length - 4)
                    return null;

                byte[] clientHello = new byte[size];
                int shortLength = (int)size;
                Array.Copy(fragment, 4, clientHello, 0, shortLength);
                int index = 0;
                byte clientVersionMajor = clientHello[index++];
                byte clientVersionMinor = clientHello[index++];
                if (clientVersionMajor != 3 || clientVersionMinor != 3)
                {
                    // not TLS 1.2
                    return null;
                }

                // 32 bytes of random : don't use
                index += 32;

                byte sessionIdLength = clientHello[index++];
                // skip sessionId
                if (sessionIdLength > 32)
                {
                    // invalid
                    return null;
                }
                index += sessionIdLength;

                ushort cipherSuiteLength = (ushort)(clientHello[index++] * 256 + clientHello[index++]);
                if (cipherSuiteLength < 2 || cipherSuiteLength > 65534)
                {
                    // invalid length
                    return null;
                }
                // Skip CipherSuite
                index += cipherSuiteLength;

                ushort compressionMethodLength = (ushort)(clientHello[index++]);
                if (compressionMethodLength < 1 || compressionMethodLength > 255)
                {
                    return null;
                }
                // Skip compression method
                index += compressionMethodLength;

                ushort extensionsLength = (ushort)(clientHello[index++] * 256 + clientHello[index++]);
                if (extensionsLength < 0 || extensionsLength > 65535)
                {
                    return null;
                }

                // Not good !extensionsLength is the total size of extensions in bytes, not number of extensions !
                //for (int extensionIndex = 0; extensionIndex < extensionsLength; ++extensionIndex)
                int startOfExtensionsIndex = index;
                while (index < startOfExtensionsIndex + extensionsLength)
                {
                    // ExtensionType over 2 bytes
                    // ExtensionDataLength over 2 bytes
                    // extension data over ExtensionDataLength bytes
                    var extensionType = (ExtensionType)(clientHello[index++] * 256 + clientHello[index++]);
                    ushort extensionDataLength = (ushort)(clientHello[index++] * 256 + clientHello[index++]);
                    if (extensionType == ExtensionType.ServerName)
                    {
                        var extensionData = new byte[extensionDataLength];
                        Array.Copy(clientHello, index, extensionData, 0, extensionDataLength);
                        int eIndex = 0;
                        ushort namesListLength = (ushort)(extensionData[eIndex++] * 256 + extensionData[eIndex++]);
                        if (namesListLength < 1 || namesListLength > 65535)
                            return null;
                        for (int nameIndex = 0; nameIndex < namesListLength; ++nameIndex)
                        {
                            var nameType = (NameType)extensionData[eIndex++];
                            if (nameType == NameType.HostName)
                            {
                                ushort nameLength = (ushort)(extensionData[eIndex++] * 256 + extensionData[eIndex++]);
                                var name = new byte[nameLength];
                                Array.Copy(extensionData, eIndex, name, 0, nameLength);
                                eIndex += nameLength;
                                string hostName = Encoding.ASCII.GetString(name);
                                return hostName;
                            }
                            else
                            {
                                // unsupported nameType
                                return null;
                            }
                        }
                    }
                    // Just skip data
                    index += extensionDataLength;

                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        enum NameType : byte
        {
            HostName = 0
        }

        enum ExtensionType : ushort
        {
            ServerName = 0,
            MaxFragmentLength = 1,
            ClientCertificateUrl = 2,
            TrustedCaKeys = 3,
            TruncatedHmac = 4,
            StatusRequest = 5
        }

        enum HandshakeType : byte
        {
            HelloRequest = 0,
            ClientHello = 1,
            ServerHello = 2,
            Certificate = 11,
            ServerKeyExchange = 12,
            CertificateRequest = 13,
            ServerHelloDone = 14,
            CertificateVerify = 15,
            ClientKeyExchange = 16,
            Finished = 20
        }

        enum ContentType : byte
        {
            ChangeCipherSpec = 20,
            Handshake = 22,
            Alert = 21,
            ApplicationData = 23,

        }
    }
}