// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Base64;
    using Xunit;

    public class Base64Test
    {
        static Random rand = new Random();

        [Fact]
        public void TestRandomDecode()
        {
            int minLength = 1;
            int maxLength = rand.Next(1000, 3000);
            for (int i = 0; i < 16; ++i)
            {
                byte[] bytes = new byte[rand.Next(minLength, maxLength)];
                rand.NextBytes(bytes);

                var base64String = Convert.ToBase64String(bytes);
                var buff = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(base64String));
                var expectedDecoded = Unpooled.CopiedBuffer(Convert.FromBase64String(base64String));

                TestDecode(buff, expectedDecoded);
            }
        }

        [Fact]
        public void TestRandomEncode()
        {
            int minLength = 1;
            int maxLength = rand.Next(1000, 3000);

            for (int i = 0; i < 16; i++)
            {
                byte[] bytes = new byte[rand.Next(minLength, maxLength)];
                rand.NextBytes(bytes);

                var buff = Unpooled.CopiedBuffer(bytes);
                var base64String = Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks).Replace("\r", "");
                var expectedEncoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(base64String));

                TestEncode(buff, expectedEncoded);
            }
        }

        public static IByteBuffer PooledBuffer(byte[] bytes)
        {
            var buffer = PooledByteBufferAllocator.Default.Buffer(bytes.Length);
            buffer.WriteBytes(bytes);
            return buffer;
        }

        [Fact]
        public void TestPooledBufferEncode()
        {
            var src = PooledBuffer(Encoding.ASCII.GetBytes("____abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"));
            var expectedEncoded = PooledBuffer(Encoding.ASCII.GetBytes("YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5emFiY2Rl"));
            var buff = src.Slice(3, src.ReadableBytes - 3);
            buff.ReadByte();
            TestEncode(buff, expectedEncoded);
        }

        [Fact]
        public void TestPooledBufferDecode()
        {
            var src = PooledBuffer(Encoding.ASCII.GetBytes("____YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5emFiY2Rl"));
            var expectedDecoded = PooledBuffer(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"));
            var buff = src.Slice(3, src.ReadableBytes - 3);
            buff.ReadByte();

            TestDecode(buff, expectedDecoded);
        }

        [Fact]
        public void TestNotAddNewLineWhenEndOnLimit()
        {
            var src = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("____abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"));
            var expectedEncoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5emFiY2Rl"));
            var buff = src.Slice(3, src.ReadableBytes - 3);
            buff.ReadByte();
            TestEncode(buff, expectedEncoded);
        }

        [Fact]
        public void TestSimpleDecode()
        {
            var src = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("____YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5emFiY2Rl"));
            var expectedDecoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"));
            var buff = src.Slice(3, src.ReadableBytes - 3);
            buff.ReadByte();

            TestDecode(buff, expectedDecoded);
        }

        [Fact]
        public void TestCompositeBufferDecoder()
        {
            var s = "YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5emFiY2Rl";
            var src1 = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(s.Substring(0, 10)));
            var src2 = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(s.Substring(10)));
            var src = new CompositeByteBuffer(src1.Allocator, 2, src1, src2);
            var expectedDecoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"));

            TestDecode(src, expectedDecoded);
        }

        [Fact]
        public void TestCompositeBufferEncoder()
        {
            var s = "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz12345678";
            var src1 = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(s.Substring(0, 10)));
            var src2 = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(s.Substring(10)));
            var src = new CompositeByteBuffer(src1.Allocator, 2, src1, src2);

            var expectedEncoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5ejEyMzQ1\nNjc4"));
            TestEncode(src, expectedEncoded);
        }

        [Fact]
        public void TestDecodeWithLineBreak()
        {
            var src = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5ejEyMzQ1\nNjc4"));
            var expectedDecoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz12345678"));

            TestDecode(src, expectedDecoded);
        }

        [Fact]
        public void TestAddNewLine()
        {
            var src = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz12345678"));
            var expectedEncoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5ejEyMzQ1\nNjc4"));
            TestEncode(src, expectedEncoded);
        }

        [Fact]
        public void TestEncodeEmpty()
        {
            var src = Unpooled.Empty;
            var expected = Unpooled.Empty;
            TestEncode(src, expected);
        }

        [Fact]
        public void TestPaddingNewline()
        {
            var certString = "-----BEGIN CERTIFICATE-----\n" +
                "MIICqjCCAjGgAwIBAgICI1YwCQYHKoZIzj0EATAmMSQwIgYDVQQDDBtUcnVzdGVk\n" +
                "IFRoaW4gQ2xpZW50IFJvb3QgQ0EwIhcRMTYwMTI0MTU0OTQ1LTA2MDAXDTE2MDQy\n" +
                "NTIyNDk0NVowYzEwMC4GA1UEAwwnREMgMGRlYzI0MGYtOTI2OS00MDY5LWE2MTYt\n" +
                "YjJmNTI0ZjA2ZGE0MREwDwYDVQQLDAhEQyBJUFNFQzEcMBoGA1UECgwTVHJ1c3Rl\n" +
                "ZCBUaGluIENsaWVudDB2MBAGByqGSM49AgEGBSuBBAAiA2IABOB7pZYC24sF5gJm\n" +
                "OHXhasxmrNYebdtSAiQRgz0M0pIsogsFeTU/W0HTlTOqwDDckphHESAKHVxa6EBL\n" +
                "d+/8HYZ1AaCmXtG73XpaOyaRr3TipJl2IaJzwuehgDHs0L+qcqOB8TCB7jAwBgYr\n" +
                "BgEBEAQEJgwkMGRlYzI0MGYtOTI2OS00MDY5LWE2MTYtYjJmNTI0ZjA2ZGE0MCMG\n" +
                "CisGAQQBjCHbZwEEFQwTNDkwNzUyMjc1NjM3MTE3Mjg5NjAUBgorBgEEAYwh22cC\n" +
                "BAYMBDIwNTkwCwYDVR0PBAQDAgXgMAkGA1UdEwQCMAAwHQYDVR0OBBYEFGWljaKj\n" +
                "wiGqW61PgLL/zLxj4iirMB8GA1UdIwQYMBaAFA2FRBtG/dGnl0iXP2uKFwJHmEQI\n" +
                "MCcGA1UdJQQgMB4GCCsGAQUFBwMCBggrBgEFBQcDAQYIKwYBBQUHAwkwCQYHKoZI\n" +
                "zj0EAQNoADBlAjAQFP8rMLUxl36u8610LsSCiRG8pP3gjuLaaJMm3tjbVue/TI4C\n" +
                "z3iL8i96YWK0VxcCMQC7pf6Wk3RhUU2Sg6S9e6CiirFLDyzLkaWxuCnXcOwTvuXT\n" +
                "HUQSeUCp2Q6ygS5qKyc=\n" +
                "-----END CERTIFICATE-----";

            var expected = "MIICqjCCAjGgAwIBAgICI1YwCQYHKoZIzj0EATAmMSQwIgYDVQQDDBtUcnVzdGVkIFRoaW4gQ2xp\n" +
                "ZW50IFJvb3QgQ0EwIhcRMTYwMTI0MTU0OTQ1LTA2MDAXDTE2MDQyNTIyNDk0NVowYzEwMC4GA1UE\n" +
                "AwwnREMgMGRlYzI0MGYtOTI2OS00MDY5LWE2MTYtYjJmNTI0ZjA2ZGE0MREwDwYDVQQLDAhEQyBJ\n" +
                "UFNFQzEcMBoGA1UECgwTVHJ1c3RlZCBUaGluIENsaWVudDB2MBAGByqGSM49AgEGBSuBBAAiA2IA\n" +
                "BOB7pZYC24sF5gJmOHXhasxmrNYebdtSAiQRgz0M0pIsogsFeTU/W0HTlTOqwDDckphHESAKHVxa\n" +
                "6EBLd+/8HYZ1AaCmXtG73XpaOyaRr3TipJl2IaJzwuehgDHs0L+qcqOB8TCB7jAwBgYrBgEBEAQE\n" +
                "JgwkMGRlYzI0MGYtOTI2OS00MDY5LWE2MTYtYjJmNTI0ZjA2ZGE0MCMGCisGAQQBjCHbZwEEFQwT\n" +
                "NDkwNzUyMjc1NjM3MTE3Mjg5NjAUBgorBgEEAYwh22cCBAYMBDIwNTkwCwYDVR0PBAQDAgXgMAkG\n" +
                "A1UdEwQCMAAwHQYDVR0OBBYEFGWljaKjwiGqW61PgLL/zLxj4iirMB8GA1UdIwQYMBaAFA2FRBtG\n" +
                "/dGnl0iXP2uKFwJHmEQIMCcGA1UdJQQgMB4GCCsGAQUFBwMCBggrBgEFBQcDAQYIKwYBBQUHAwkw\n" +
                "CQYHKoZIzj0EAQNoADBlAjAQFP8rMLUxl36u8610LsSCiRG8pP3gjuLaaJMm3tjbVue/TI4Cz3iL\n" +
                "8i96YWK0VxcCMQC7pf6Wk3RhUU2Sg6S9e6CiirFLDyzLkaWxuCnXcOwTvuXTHUQSeUCp2Q6ygS5q\n" +
                "Kyc=";

            X509Certificate cert = FromString(certString);
            var src = Unpooled.WrappedBuffer(cert.GetRawCertData());
            var expectedEncoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(expected));
            TestEncode(src, expectedEncoded);
        }

        private static X509Certificate FromString(string cert)
        {
            return new X509Certificate(Encoding.ASCII.GetBytes(cert));
        }

        private static void TestEncode(IByteBuffer src, IByteBuffer expected)
        {
            var encoded = Base64.Encode(src, true, Base64Dialect.STANDARD);
            try
            {
                Assert.NotNull(encoded);
                Assert.Equal(expected, encoded);
            }
            finally
            {
                src.Release();
                expected.Release();
                encoded.Release();
            }
        }

        private static void TestDecode(IByteBuffer src, IByteBuffer expected)
        {
            var decoded = Base64.Decode(src, Base64Dialect.STANDARD);
            try
            {
                Assert.NotNull(decoded);
                Assert.Equal(expected, decoded);
            }
            finally
            {
                src.Release();
                expected.Release();
                decoded.Release();
            }
        }
    }
}