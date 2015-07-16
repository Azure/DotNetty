// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.End2End
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    public class TestScenarioRunner : ChannelHandlerAdapter
    {
        IEnumerator<TestScenarioStep> testScenario;
        readonly Func<Func<object>, IEnumerable<TestScenarioStep>> testScenarioProvider;
        readonly TaskCompletionSource completion;
        object lastReceivedMessage;

        public TestScenarioRunner(Func<Func<object>, IEnumerable<TestScenarioStep>> testScenarioProvider, TaskCompletionSource completion)
        {
            Contract.Requires(completion != null);
            this.testScenarioProvider = testScenarioProvider;
            this.completion = completion;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            this.testScenario = this.testScenarioProvider(() => this.lastReceivedMessage).GetEnumerator();
            this.ContinueScenarioExecution(context);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            this.lastReceivedMessage = message;
            this.ContinueScenarioExecution(context);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            //context.Flush();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            this.completion.TrySetException(exception);
            context.CloseAsync();
        }

        void ContinueScenarioExecution(IChannelHandlerContext context)
        {
            if (!this.testScenario.MoveNext())
            {
                context.CloseAsync()
                    .ContinueWith(
                        t => this.completion.TrySetException(t.Exception),
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                this.completion.TryComplete();
                return;
            }
            foreach (object message in this.testScenario.Current.SendMessages)
            {
                context.WriteAsync(message)
                    .ContinueWith(
                        t => this.completion.TrySetException(t.Exception),
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            }

            context.Flush();
        }
    }
}