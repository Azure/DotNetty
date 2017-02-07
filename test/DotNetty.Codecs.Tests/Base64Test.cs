//// Copyright (c) Microsoft. All rights reserved.
//// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//namespace DotNetty.Codecs.Tests
//{
//    using System;
//    using System.Security.Cryptography.X509Certificates;
//    using System.Text;
//    using DotNetty.Buffers;
//    using DotNetty.Codecs.Base64;
//    using Xunit;
//    using Xunit.Abstractions;

//    public class Base64Test
//    {
//        readonly ITestOutputHelper output;

//        public Base64Test(ITestOutputHelper output)
//        {
//            this.output = output;
//        }

//        [Fact]
//        public void TestRandomDecode()
//        {
//            int minLength = 1;
//            var rand = new Random();
//            int maxLength = rand.Next(1000, 3000);
//            for (int i = 0; i < 16; ++i)
//            {
//                var bytes = new byte[rand.Next(minLength, maxLength)];
//                rand.NextBytes(bytes);

//                string base64String = Convert.ToBase64String(bytes);
//                IByteBuffer buff = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(base64String));
//                IByteBuffer expectedDecoded = Unpooled.CopiedBuffer(Convert.FromBase64String(base64String));

//                TestDecode(buff, expectedDecoded);
//            }
//        }

//        [Fact]
//        public void TestRandomEncode()
//        {
//            int minLength = 1;
//            var rand = new Random();
//            int maxLength = rand.Next(1000, 3000);

//            for (int i = 0; i < 16; i++)
//            {
//                var bytes = new byte[rand.Next(minLength, maxLength)];
//                rand.NextBytes(bytes);

//                IByteBuffer buff = Unpooled.WrappedBuffer(bytes);
//                string base64String = Convert.ToBase64String(bytes).Replace("\r", "");
//                IByteBuffer expectedEncoded = Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(base64String));

//                this.TestEncode(buff, expectedEncoded, false);
//            }
//        }

//        public static IByteBuffer PooledBuffer(byte[] bytes)
//        {
//            IByteBuffer buffer = PooledByteBufferAllocator.Default.Buffer(bytes.Length);
//            buffer.WriteBytes(bytes);
//            return buffer;
//        }

//        [Fact]
//        public void TestPooledBufferEncode()
//        {
//            IByteBuffer src = PooledBuffer(Encoding.ASCII.GetBytes("____abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"));
//            IByteBuffer expectedEncoded = PooledBuffer(Encoding.ASCII.GetBytes("YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5emFiY2Rl"));
//            IByteBuffer buff = src.Slice(3, src.ReadableBytes - 3);
//            buff.ReadByte();
//            this.TestEncode(buff, expectedEncoded);
//        }

//        [Fact]
//        public void TestPooledBufferDecode()
//        {
//            IByteBuffer src = PooledBuffer(Encoding.ASCII.GetBytes("____YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5emFiY2Rl"));
//            IByteBuffer expectedDecoded = PooledBuffer(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"));
//            IByteBuffer buff = src.Slice(3, src.ReadableBytes - 3);
//            buff.ReadByte();

//            TestDecode(buff, expectedDecoded);
//        }

//        [Fact]
//        public void TestNotAddNewLineWhenEndOnLimit()
//        {
//            IByteBuffer src = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("____abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"));
//            IByteBuffer expectedEncoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5emFiY2Rl"));
//            IByteBuffer buff = src.Slice(3, src.ReadableBytes - 3);
//            buff.ReadByte();
//            this.TestEncode(buff, expectedEncoded);
//        }

//        [Fact]
//        public void TestSimpleDecode()
//        {
//            IByteBuffer src = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("____YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5emFiY2Rl"));
//            IByteBuffer expectedDecoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"));
//            IByteBuffer buff = src.Slice(3, src.ReadableBytes - 3);
//            buff.ReadByte();

//            TestDecode(buff, expectedDecoded);
//        }

//        [Fact]
//        public void TestCompositeBufferDecoder()
//        {
//            string s = "YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5emFiY2Rl";
//            IByteBuffer src1 = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(s.Substring(0, 10)));
//            IByteBuffer src2 = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(s.Substring(10)));
//            var src = new CompositeByteBuffer(src1.Allocator, 2, src1, src2);
//            IByteBuffer expectedDecoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcde"));

//            TestDecode(src, expectedDecoded);
//        }

//        [Fact]
//        public void TestCompositeBufferEncoder()
//        {
//            string s = "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz12345678";
//            IByteBuffer src1 = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(s.Substring(0, 10)));
//            IByteBuffer src2 = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(s.Substring(10)));
//            var src = new CompositeByteBuffer(src1.Allocator, 2, src1, src2);

//            IByteBuffer expectedEncoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5ejEyMzQ1\nNjc4"));
//            this.TestEncode(src, expectedEncoded);
//        }

//        [Fact]
//        public void TestDecodeWithLineBreak()
//        {
//            IByteBuffer src = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5ejEyMzQ1\nNjc4"));
//            IByteBuffer expectedDecoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz12345678"));

//            TestDecode(src, expectedDecoded);
//        }

//        [Fact]
//        public void TestAddNewLine()
//        {
//            IByteBuffer src = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz12345678"));
//            IByteBuffer expectedEncoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes("YWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4eXphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5ejEyMzQ1\nNjc4"));
//            this.TestEncode(src, expectedEncoded);
//        }

//        [Fact]
//        public void TestEncodeEmpty()
//        {
//            IByteBuffer src = Unpooled.Empty;
//            IByteBuffer expected = Unpooled.Empty;
//            this.TestEncode(src, expected);
//        }

//        [Fact]
//        public void TestPaddingNewline()
//        {
//            string expected = "MIICqjCCAjGgAwIBAgICI1YwCQYHKoZIzj0EATAmMSQwIgYDVQQDDBtUcnVzdGVkIFRoaW4gQ2xp\n" +
//                "ZW50IFJvb3QgQ0EwIhcRMTYwMTI0MTU0OTQ1LTA2MDAXDTE2MDQyNTIyNDk0NVowYzEwMC4GA1UE\n" +
//                "AwwnREMgMGRlYzI0MGYtOTI2OS00MDY5LWE2MTYtYjJmNTI0ZjA2ZGE0MREwDwYDVQQLDAhEQyBJ\n" +
//                "UFNFQzEcMBoGA1UECgwTVHJ1c3RlZCBUaGluIENsaWVudDB2MBAGByqGSM49AgEGBSuBBAAiA2IA\n" +
//                "BOB7pZYC24sF5gJmOHXhasxmrNYebdtSAiQRgz0M0pIsogsFeTU/W0HTlTOqwDDckphHESAKHVxa\n" +
//                "6EBLd+/8HYZ1AaCmXtG73XpaOyaRr3TipJl2IaJzwuehgDHs0L+qcqOB8TCB7jAwBgYrBgEBEAQE\n" +
//                "JgwkMGRlYzI0MGYtOTI2OS00MDY5LWE2MTYtYjJmNTI0ZjA2ZGE0MCMGCisGAQQBjCHbZwEEFQwT\n" +
//                "NDkwNzUyMjc1NjM3MTE3Mjg5NjAUBgorBgEEAYwh22cCBAYMBDIwNTkwCwYDVR0PBAQDAgXgMAkG\n" +
//                "A1UdEwQCMAAwHQYDVR0OBBYEFGWljaKjwiGqW61PgLL/zLxj4iirMB8GA1UdIwQYMBaAFA2FRBtG\n" +
//                "/dGnl0iXP2uKFwJHmEQIMCcGA1UdJQQgMB4GCCsGAQUFBwMCBggrBgEFBQcDAQYIKwYBBQUHAwkw\n" +
//                "CQYHKoZIzj0EAQNoADBlAjAQFP8rMLUxl36u8610LsSCiRG8pP3gjuLaaJMm3tjbVue/TI4Cz3iL\n" +
//                "8i96YWK0VxcCMQC7pf6Wk3RhUU2Sg6S9e6CiirFLDyzLkaWxuCnXcOwTvuXTHUQSeUCp2Q6ygS5q\n" +
//                "Kyc=";

//            string certString = "-----BEGIN CERTIFICATE-----\n" +
//                expected + "\n" +
//                "-----END CERTIFICATE-----";

//            X509Certificate cert = FromString(certString);
//            IByteBuffer src = Unpooled.WrappedBuffer(cert.Export(X509ContentType.Cert));
//            IByteBuffer expectedEncoded = Unpooled.CopiedBuffer(Encoding.ASCII.GetBytes(expected));
//            this.TestEncode(src, expectedEncoded);
//        }

//        static X509Certificate FromString(string cert)
//        {
//            return new X509Certificate(Encoding.ASCII.GetBytes(cert));
//        }

//        void TestEncode(IByteBuffer src, IByteBuffer expected)
//        {
//            TestEncode(src, expected, true);
//        }

//        void TestEncode(IByteBuffer src, IByteBuffer expected, bool breakLines)
//        {
//            IByteBuffer encoded = Base64.Encode(src, breakLines, Base64Dialect.STANDARD);
//            try
//            {
//                Assert.NotNull(encoded);
//                string expectedPretty = ByteBufferUtil.PrettyHexDump(expected);
//                string actualPretty = ByteBufferUtil.PrettyHexDump(encoded);
//                this.output.WriteLine("expected:\n" + expectedPretty);
//                this.output.WriteLine("actual:\n" + actualPretty);
//                if (expectedPretty != actualPretty)
//                {
//                    Assert.Equal(expectedPretty, actualPretty);
//                }
//            }
//            finally
//            {
//                src.Release();
//                expected.Release();
//                encoded.Release();
//            }
//        }

//        static void TestDecode(IByteBuffer src, IByteBuffer expected)
//        {
//            IByteBuffer decoded = Base64.Decode(src, Base64Dialect.STANDARD);
//            try
//            {
//                Assert.NotNull(decoded);
//                Assert.Equal(expected, decoded);
//            }
//            finally
//            {
//                src.Release();
//                expected.Release();
//                decoded.Release();
//            }
//        }
//    }
//}