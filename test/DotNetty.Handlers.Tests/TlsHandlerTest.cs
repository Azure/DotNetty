// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Handlers.Tls;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;
    using Xunit.Abstractions;

    public class TlsHandlerTest : TestBase
    {
        static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        public TlsHandlerTest(ITestOutputHelper output)
            : base(output)
        {
        }

        public static IEnumerable<object[]> GetTlsReadTestData()
        {
            var random = new Random(Environment.TickCount);
            var lengthVariations =
                new[]
                {
                    new[] { 1 },
                    new[] { 2, 8000, 300 },
                    new[] { 100, 0, 1000 },
                    new[] { 4 * 1024 - 10, 1, 0, 1 },
                    new[] { 0, 24000, 0, 1000 },
                    new[] { 0, 4000, 0 },
                    new[] { 16 * 1024 - 100 },
                    Enumerable.Repeat(0, 30).Select(_ => random.Next(0, 17000)).ToArray()
                };
            var boolToggle = new[] { false, true };
            var protocols = new[] { SslProtocols.Tls, SslProtocols.Tls11, SslProtocols.Tls12 };
            var writeStrategyFactories = new Func<IWriteStrategy>[]
            {
                () => new AsIsWriteStrategy(),
                () => new BatchingWriteStrategy(1, TimeSpan.FromMilliseconds(20), true),
                () => new BatchingWriteStrategy(4096, TimeSpan.FromMilliseconds(20), true),
                () => new BatchingWriteStrategy(32 * 1024, TimeSpan.FromMilliseconds(20), false)
            };

            return
                from frameLengths in lengthVariations
                from isClient in boolToggle
                from writeStrategyFactory in writeStrategyFactories
                from protocol in protocols
                select new object[] { frameLengths, isClient, writeStrategyFactory(), protocol };
        }


        [Theory]
        [MemberData(nameof(GetTlsReadTestData))]
        public async Task TlsRead(int[] frameLengths, bool isClient, IWriteStrategy writeStrategy, SslProtocols protocol)
        {
            this.Output.WriteLine($"frameLengths: {string.Join(", ", frameLengths)}");
            this.Output.WriteLine($"writeStrategy: {writeStrategy}");
            this.Output.WriteLine($"protocol: {protocol}");

            var executor = new SingleThreadEventExecutor("test executor", TimeSpan.FromMilliseconds(10));

            try
            {
                var writeTasks = new List<Task>();
                var pair = await SetupStreamAndChannelAsync(isClient, executor, writeStrategy, protocol, writeTasks).WithTimeout(TimeSpan.FromSeconds(10));
                EmbeddedChannel ch = pair.Item1;
                SslStream driverStream = pair.Item2;

                int randomSeed = Environment.TickCount;
                var random = new Random(randomSeed);
                IByteBuffer expectedBuffer = Unpooled.Buffer(16 * 1024);
                foreach (int len in frameLengths)
                {
                    var data = new byte[len];
                    random.NextBytes(data);
                    expectedBuffer.WriteBytes(data);
                    await driverStream.WriteAsync(data, 0, data.Length).WithTimeout(TimeSpan.FromSeconds(5));
                }
                await Task.WhenAll(writeTasks).WithTimeout(TimeSpan.FromSeconds(5));
                IByteBuffer finalReadBuffer = Unpooled.Buffer(16 * 1024);
                await ReadOutboundAsync(async () => ch.ReadInbound<IByteBuffer>(), expectedBuffer.ReadableBytes, finalReadBuffer, TestTimeout);
                Assert.True(ByteBufferUtil.Equals(expectedBuffer, finalReadBuffer), $"---Expected:\n{ByteBufferUtil.PrettyHexDump(expectedBuffer)}\n---Actual:\n{ByteBufferUtil.PrettyHexDump(finalReadBuffer)}");
                driverStream.Dispose();
            }
            finally
            {
                await executor.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));
            }
        }

        public static IEnumerable<object[]> GetTlsWriteTestData()
        {
            var random = new Random(Environment.TickCount);
            var lengthVariations =
                new[]
                {
                    new[] { 1 },
                    new[] { 2, 8000, 300 },
                    new[] { 100, 0, 1000 },
                    new[] { 4 * 1024 - 10, 1, -1, 0, -1, 1 },
                    new[] { 0, 24000, 0, -1, 1000 },
                    new[] { 0, 4000, 0 },
                    new[] { 16 * 1024 - 100 },
                    Enumerable.Repeat(0, 30).Select(_ => random.Next(0, 10) < 2 ? -1 : random.Next(0, 17000)).ToArray()
                };
            var boolToggle = new[] { false, true };
            var protocols = new[] { SslProtocols.Tls, SslProtocols.Tls11, SslProtocols.Tls12 };

            return
                from frameLengths in lengthVariations
                from isClient in boolToggle
                from protocol in protocols
                select new object[] { frameLengths, isClient, protocol };
        }

        [Theory]
        [MemberData(nameof(GetTlsWriteTestData))]
        public async Task TlsWrite(int[] frameLengths, bool isClient, SslProtocols protocol)
        {
            this.Output.WriteLine("frameLengths: " + string.Join(", ", frameLengths));

            var writeStrategy = new AsIsWriteStrategy();

            var executor = new SingleThreadEventExecutor("test executor", TimeSpan.FromMilliseconds(10));

            try
            {
                var writeTasks = new List<Task>();
                var pair = await SetupStreamAndChannelAsync(isClient, executor, writeStrategy, protocol, writeTasks);
                EmbeddedChannel ch = pair.Item1;
                SslStream driverStream = pair.Item2;

                int randomSeed = Environment.TickCount;
                var random = new Random(randomSeed);
                IByteBuffer expectedBuffer = Unpooled.Buffer(16 * 1024);
                foreach (IEnumerable<int> lengths in frameLengths.Split(x => x < 0))
                {
                    ch.WriteOutbound(lengths.Select(len =>
                    {
                        var data = new byte[len];
                        random.NextBytes(data);
                        expectedBuffer.WriteBytes(data);
                        return (object)Unpooled.WrappedBuffer(data);
                    }).ToArray());
                }

                IByteBuffer finalReadBuffer = Unpooled.Buffer(16 * 1024);
                var readBuffer = new byte[16 * 1024 * 10];
                await ReadOutboundAsync(
                    async () =>
                    {
                        int read = await driverStream.ReadAsync(readBuffer, 0, readBuffer.Length);
                        return Unpooled.WrappedBuffer(readBuffer, 0, read);
                    },
                    expectedBuffer.ReadableBytes, finalReadBuffer, TestTimeout);
                Assert.True(ByteBufferUtil.Equals(expectedBuffer, finalReadBuffer), $"---Expected:\n{ByteBufferUtil.PrettyHexDump(expectedBuffer)}\n---Actual:\n{ByteBufferUtil.PrettyHexDump(finalReadBuffer)}");
                driverStream.Dispose();
            }
            finally
            {
                await executor.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));
            }
        }

        static async Task<Tuple<EmbeddedChannel, SslStream>> SetupStreamAndChannelAsync(bool isClient, IEventExecutor executor, IWriteStrategy writeStrategy, SslProtocols protocol, List<Task> writeTasks)
        {
            X509Certificate2 tlsCertificate = TestResourceHelper.GetTestCertificate();
            string targetHost = tlsCertificate.GetNameInfo(X509NameType.DnsName, false);
            TlsHandler tlsHandler = isClient ?
                new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), new ClientTlsSettings(targetHost)) :
                TlsHandler.Server(tlsCertificate);
            //var ch = new EmbeddedChannel(new LoggingHandler("BEFORE"), tlsHandler, new LoggingHandler("AFTER"));
            var ch = new EmbeddedChannel(tlsHandler);

            IByteBuffer readResultBuffer = Unpooled.Buffer(4 * 1024);
            Func<ArraySegment<byte>, Task<int>> readDataFunc = async output =>
            {
                if (writeTasks.Count > 0)
                {
                    await Task.WhenAll(writeTasks).WithTimeout(TestTimeout);
                    writeTasks.Clear();
                }

                if (readResultBuffer.ReadableBytes < output.Count)
                {
                    await ReadOutboundAsync(async () => ch.ReadOutbound<IByteBuffer>(), output.Count - readResultBuffer.ReadableBytes, readResultBuffer, TestTimeout);
                }
                Assert.NotEqual(0, readResultBuffer.ReadableBytes);
                int read = Math.Min(output.Count, readResultBuffer.ReadableBytes);
                readResultBuffer.ReadBytes(output.Array, output.Offset, read);
                return read;
            };
            var mediationStream = new MediationStream(readDataFunc, input =>
            {
                Task task = executor.SubmitAsync(() => writeStrategy.WriteToChannelAsync(ch, input)).Unwrap();
                writeTasks.Add(task);
                return task;
            });

            var driverStream = new SslStream(mediationStream, true, (_1, _2, _3, _4) => true);
            if (isClient)
            {
                await Task.Run(() => driverStream.AuthenticateAsServerAsync(tlsCertificate)).WithTimeout(TimeSpan.FromSeconds(5));
            }
            else
            {
                await Task.Run(() => driverStream.AuthenticateAsClientAsync(targetHost, null, protocol, false)).WithTimeout(TimeSpan.FromSeconds(5));
            }
            writeTasks.Clear();

            return Tuple.Create(ch, driverStream);
        }

        static Task ReadOutboundAsync(Func<Task<IByteBuffer>> readFunc, int expectedBytes, IByteBuffer result, TimeSpan timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            int remaining = expectedBytes;
            return AssertEx.EventuallyAsync(
                async () =>
                {
                    TimeSpan readTimeout = timeout - stopwatch.Elapsed;
                    if (readTimeout <= TimeSpan.Zero)
                    {
                        return false;
                    }

                    IByteBuffer output = await readFunc().WithTimeout(readTimeout);//inbound ? ch.ReadInbound<IByteBuffer>() : ch.ReadOutbound<IByteBuffer>();
                    if (output != null)
                    {
                        remaining -= output.ReadableBytes;
                        result.WriteBytes(output);
                    }
                    return remaining <= 0;
                },
                TimeSpan.FromMilliseconds(10),
                timeout);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NoAutoReadHandshakeProgresses(bool dropChannelActive)
        {
            var readHandler = new ReadRegisterHandler();
            EmbeddedChannel ch = new EmbeddedChannel(EmbeddedChannelId.Instance, false, false,
               readHandler,
               TlsHandler.Client("dotnetty.com"),
               new ActivatingHandler(dropChannelActive)
           );

           ch.Configuration.AutoRead = false;
           ch.Start();
           Assert.False(ch.Configuration.AutoRead);
           Assert.True(ch.WriteOutbound(Unpooled.Empty));
           Assert.True(readHandler.ReadIssued);
        }

        class ReadRegisterHandler : ChannelHandlerAdapter
        {
            public bool ReadIssued { get; private set; }

            public override void Read(IChannelHandlerContext context)
            {
                this.ReadIssued = true;
                base.Read(context);
            }
        }

        class ActivatingHandler : ChannelHandlerAdapter
        {
            bool dropChannelActive;

            public ActivatingHandler(bool dropChannelActive)
            {
                this.dropChannelActive = dropChannelActive;
            }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                if (!dropChannelActive)
                {
                    context.FireChannelActive();
                }
            }
        }
    }
}
