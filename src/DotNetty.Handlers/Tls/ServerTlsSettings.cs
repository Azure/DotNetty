// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;

    public sealed class ServerTlsSettings : TlsSettings
    {
        public ServerTlsSettings(X509Certificate certificate)
            : this(certificate, false)
        {
        }

        public ServerTlsSettings(X509Certificate certificate, bool negotiateClientCertificate)
            : this(certificate, negotiateClientCertificate, false)
        {
        }

        public ServerTlsSettings(X509Certificate certificate, bool negotiateClientCertificate, bool checkCertificateRevocation)
            : this(certificate, negotiateClientCertificate, checkCertificateRevocation, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12)
        {
        }

        public ServerTlsSettings(X509Certificate certificate, bool negotiateClientCertificate, bool checkCertificateRevocation, SslProtocols enabledProtocols)
            : base(enabledProtocols, checkCertificateRevocation)
        {
            this.Certificate = certificate;
            this.NegotiateClientCertificate = negotiateClientCertificate;
        }

        public X509Certificate Certificate { get; }

        public bool NegotiateClientCertificate { get; }
    }
}