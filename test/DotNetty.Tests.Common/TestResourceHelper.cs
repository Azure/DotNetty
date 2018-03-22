// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;

    public static class TestResourceHelper
    {
        public static X509Certificate2 GetTestCertificate()
        {
            byte[] certData;
            using (Stream resStream = typeof(TestResourceHelper).GetTypeInfo().Assembly.GetManifestResourceStream(typeof(TestResourceHelper).Namespace + "." + "dotnetty.com.pfx"))
            using (var memStream = new MemoryStream())
            {
                resStream.CopyTo(memStream);
                certData = memStream.ToArray();
            }

            return new X509Certificate2(certData, "password");
        }

        public static X509Certificate2 GetTestCertificate2()
        {
            byte[] certData;
            using (Stream resStream = typeof(TestResourceHelper).GetTypeInfo().Assembly.GetManifestResourceStream(typeof(TestResourceHelper).Namespace + "." + "contoso.com.pfx"))
            using (var memStream = new MemoryStream())
            {
                resStream.CopyTo(memStream);
                certData = memStream.ToArray();
            }

            return new X509Certificate2(certData, "password");
        }
    }
}