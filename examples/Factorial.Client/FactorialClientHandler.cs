// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Factorial.Client
{
    using System.Numerics;
    using System;
    using System.Collections.Concurrent;
    using DotNetty.Transport.Channels;

    public class FactorialClientHandler : SimpleChannelInboundHandler<BigInteger>
    {
        private IChannelHandlerContext ctx;
        private int receivedMessages;
        private int next = 1;
        readonly ConcurrentQueue<BigInteger> answer = new ConcurrentQueue<BigInteger>();

        public BigInteger GetFactorial()
        {
            bool interrupted = false;
            BigInteger result = new BigInteger(1);
            try
            {
                for (;;)
                {
                    try
                    {
                        if (this.answer.TryDequeue(out result))
                            return result;
                    }
                    catch
                    {
                        interrupted = true;
                    }
                }
            }
            finally
            {
                if (interrupted)
                {
                    System.Threading.Thread.CurrentThread.Interrupt();
                }
            }
        }

        public FactorialClientHandler()
        {
        }

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            this.ctx = ctx;
            this.SendNumbers();
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, BigInteger msg)
        {
            this.receivedMessages++;
            this.answer.Enqueue(msg);
            if (this.receivedMessages == FactorialClientSettings.Count)
            {
                Console.WriteLine("Factorial of {0} is: {1}", FactorialClientSettings.Count, msg);
                ctx.CloseAsync();
            }
        }

        private void SendNumbers()
        {
            for (int i = 0; i < 4096 && next <= FactorialClientSettings.Count; i++)
            {
                ctx.WriteAsync(new BigInteger(next));
                next++;
            }
            ctx.Flush();
        }
    }
}
