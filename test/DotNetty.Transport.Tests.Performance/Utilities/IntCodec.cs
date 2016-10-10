namespace DotNetty.Transport.Tests.Performance.Utilities
{
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class IntCodec : ChannelHandlerAdapter
    {
        public IntCodec(bool releaseMessages = false)
        {
            this.ReleaseMessages = releaseMessages;
        }

        public bool ReleaseMessages { get; }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is IByteBuffer)
            {
                var buf = (IByteBuffer)message;
                int integer = buf.ReadInt();
                if (this.ReleaseMessages)
                {
                    ReferenceCountUtil.SafeRelease(message);
                }
                context.FireChannelRead(integer);
            }
            else
            {
                context.FireChannelRead(message);
            }
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            if (message is int)
            {
                IByteBuffer buf = Unpooled.Buffer(4).WriteInt((int)message);
                return context.WriteAsync(buf);
            }
            return context.WriteAsync(message);
        }
    }
}