// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System.Net.Security;
    using System.Security.Authentication;

    public abstract class TlsSettings
    {
        protected TlsSettings(
            SslProtocols enabledProtocols, 
            bool checkCertificateRevocation,
            RemoteCertificateValidationCallback remoteCertificateValidationCallback,
            LocalCertificateSelectionCallback localCertificateSelectionCallback)
            :this(enabledProtocols, checkCertificateRevocation)
        {
            this.RemoteCertificateValidationCallback = remoteCertificateValidationCallback;
            this.LocalCertificateSelectionCallback = localCertificateSelectionCallback;
        }

        protected TlsSettings(
            SslProtocols enabledProtocols,
            bool checkCertificateRevocation,
            RemoteCertificateValidationCallback remoteCertificateValidationCallback)
            : this(enabledProtocols, checkCertificateRevocation)
        {
            this.RemoteCertificateValidationCallback = remoteCertificateValidationCallback;
        }

        protected TlsSettings(SslProtocols enabledProtocols, bool checkCertificateRevocation)
        {
            this.EnabledProtocols = enabledProtocols;
            this.CheckCertificateRevocation = checkCertificateRevocation;
        }

        public SslProtocols EnabledProtocols { get; }

        public bool CheckCertificateRevocation { get; }

        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback { get; }

        public LocalCertificateSelectionCallback LocalCertificateSelectionCallback { get; }
    }
}