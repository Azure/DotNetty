// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;

    public sealed class ServerTlsSettings : TlsSettings
    {
        public ServerTlsSettings(X509Certificate certificate)
            : this(false, certificate)
        {
        }

        public ServerTlsSettings(bool checkCertificateRevocation, X509Certificate certificate)
            : this(SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, checkCertificateRevocation, certificate)
        {
        }

        public ServerTlsSettings(SslProtocols enabledProtocols, bool checkCertificateRevocation, X509Certificate certificate)
            : base(enabledProtocols, checkCertificateRevocation)
        {
            this.Certificate = certificate;
        }

        public X509Certificate Certificate { get; }
    }
}