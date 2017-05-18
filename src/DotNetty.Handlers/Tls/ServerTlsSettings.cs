// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System.Collections.Generic;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;

    public sealed class ServerTlsSettings : TlsSettings
    {
        public const string DefaultCertficateSelectorKey = "Default";

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
            this.Certificates = new Dictionary<string, X509Certificate>
            {
                { DefaultCertficateSelectorKey, certificate }
            };
            this.NegotiateClientCertificate = negotiateClientCertificate;
        }

        public ServerTlsSettings(IDictionary<string, X509Certificate> hostnameCertificateMapping, bool negotiateClientCertificate, bool checkCertificateRevocation, SslProtocols enabledProtocols)
            : base(enabledProtocols, checkCertificateRevocation)
        {
            this.Certificates = hostnameCertificateMapping;
            this.NegotiateClientCertificate = negotiateClientCertificate;
            this.SniEnabled = true; // assuming SNI intended since this variant of constructor called
        }

        public IDictionary<string, X509Certificate> Certificates { get; }

        public bool NegotiateClientCertificate { get; }

        public bool SniEnabled { get; }
    }
}