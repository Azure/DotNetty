namespace UWPEcho.Client
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using DotNetty.Handlers.Logging;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using System.Text;
    using DotNetty.Buffers;
    using System.Threading;

    public class EchoClientHandler : ChannelHandlerAdapter
    {
        readonly IByteBuffer initialMessage;
        readonly Action<object> logger;

        public EchoClientHandler(Action<object> logger)
        {
            this.logger = logger;
            this.initialMessage = Unpooled.Buffer(256);
            byte[] messageBytes = Encoding.UTF8.GetBytes("Hello from UWP!");
            this.initialMessage.WriteBytes(messageBytes);
        }

        public override void ChannelActive(IChannelHandlerContext context) => context.WriteAndFlushAsync(this.initialMessage);

        public override async void ChannelRead(IChannelHandlerContext context, object message)
        {
            try
            {
                var byteBuffer = message as IByteBuffer;
                if (byteBuffer != null)
                {
                    var str = byteBuffer.ToString(Encoding.UTF8);
                    System.Diagnostics.Debug.WriteLine("Received from server: " + str);
                    this.logger(str);
                }
                await context.WriteAsync(message);
                // Throttle the client:
                await Task.Delay(100);
            }
            catch(Exception ex)
            {
                var str = string.Format("Error reading from channel: {0}", ex.Message);
                this.logger(str);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            context.CloseAsync();
        }
    }

}
