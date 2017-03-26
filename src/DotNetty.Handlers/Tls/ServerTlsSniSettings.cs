// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;

    public sealed class ServerTlsSniSettings : TlsSettings
    {
        public ServerTlsSniSettings(Func<string, X509Certificate2> serverCertificateSelector)
            : this(serverCertificateSelector, false)
        {
        }

        public ServerTlsSniSettings(Func<string, X509Certificate2> serverCertificateSelector, bool negotiateClientCertificate)
            : this(serverCertificateSelector, negotiateClientCertificate, false)
        {
        }

        public ServerTlsSniSettings(Func<string, X509Certificate2> serverCertificateSelector, bool negotiateClientCertificate, bool checkCertificateRevocation)
            : this(serverCertificateSelector, negotiateClientCertificate, checkCertificateRevocation, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12)
        {
        }

        public ServerTlsSniSettings(Func<string, X509Certificate2> serverCertificateSelector, bool negotiateClientCertificate, bool checkCertificateRevocation, SslProtocols enabledProtocols)
            : this(serverCertificateSelector, null, negotiateClientCertificate, checkCertificateRevocation, enabledProtocols)
        {
        }

        public ServerTlsSniSettings(Func<string, X509Certificate2> serverCertificateSelector, string defaultServerHostName, bool negotiateClientCertificate, bool checkCertificateRevocation, SslProtocols enabledProtocols)
            : base(enabledProtocols, checkCertificateRevocation)
        {
            Contract.Requires(serverCertificateSelector != null);
            this.ServerCertificateSelector = serverCertificateSelector;
            this.DefaultServerHostName = defaultServerHostName;
            this.NegotiateClientCertificate = negotiateClientCertificate;
        }

        public Func<string, X509Certificate2> ServerCertificateSelector { get; }

        public string DefaultServerHostName { get; } 

        public bool NegotiateClientCertificate { get; }
    }
}