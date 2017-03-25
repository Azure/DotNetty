// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;

    public sealed class ServerTlsSniSettings : TlsSettings
    {
        public ServerTlsSniSettings(Func<string, X509Certificate2> certificateSelector)
            : this(certificateSelector, false)
        {
        }

        public ServerTlsSniSettings(Func<string, X509Certificate2> certificateSelector, bool negotiateClientCertificate)
            : this(certificateSelector, negotiateClientCertificate, false)
        {
        }

        public ServerTlsSniSettings(Func<string, X509Certificate2> certificateSelector, bool negotiateClientCertificate, bool checkCertificateRevocation)
            : this(certificateSelector, negotiateClientCertificate, checkCertificateRevocation, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12)
        {
        }

        // Caller should decide what certifcate should be used when host name cannot be found from client hello
        public ServerTlsSniSettings(Func<string, X509Certificate2> certificateSelector, bool negotiateClientCertificate, bool checkCertificateRevocation, SslProtocols enabledProtocols)
            : base(enabledProtocols, checkCertificateRevocation)
        {
            this.CertificateSelector = certificateSelector;
            this.NegotiateClientCertificate = negotiateClientCertificate;
        }

        public Func<string, X509Certificate2> CertificateSelector { get; }

        public bool NegotiateClientCertificate { get; }
    }
}