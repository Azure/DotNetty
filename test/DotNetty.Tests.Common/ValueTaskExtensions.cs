// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    public static class ValueTaskExtensions
    {
        public static async void CloseOnComplete(this ValueTask task, IChannel channel)
        {
            try
            {
                await task;
            }
            finally
            {
                channel.CloseAsync();
            }

        }
        
        
        public static async void CloseOnComplete(this ValueTask task, IChannelHandlerContext ctx)
        {
            try
            {
                await task;
            }
            finally
            {
                ctx.CloseAsync();
            }
        }
    }
}