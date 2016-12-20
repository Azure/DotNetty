using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using DotNetty.Codecs;
using DotNetty.Handlers.Logging;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNettyTestApp;
using Windows.Networking;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWPEcho.Client
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            Runner.RunClientAsync();
        }
    }

    class Runner
    {
        public class ClientSettings
        {
            public static bool IsSsl
            {
                get
                {
                    string ssl = "true";
                    return !string.IsNullOrEmpty(ssl) && bool.Parse(ssl);
                }
            }

            public static IPAddress Host => IPAddress.Parse("127.0.0.1");

            public static int Port => int.Parse("8007");

            public static int Size => int.Parse("256");
        }

        public static async Task RunClientAsync()
        {
            var group = new MultithreadEventLoopGroup();

            string pfxDir = "dotnetty.com.pfx";
            X509Certificate2 cert = new X509Certificate2(pfxDir, "password");
            string targetHost = cert.GetNameInfo(X509NameType.DnsName, false);

            try
            {
                Func<IChannel> channelFactory = () =>
                {
                    var channel = new StreamSocketChannel(
                                    new HostName(ClientSettings.Host.ToString()),
                                    ClientSettings.Port.ToString(),
                                    new HostName(targetHost));
                    return channel;
                };

                var bootstrap = new Bootstrap();

                bootstrap
                    .Group(group)
                    .RemoteAddress(ClientSettings.Host, ClientSettings.Port)
                    .ChannelFactory(channelFactory)
                    .Option(ChannelOption.TcpNodelay, true)
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        pipeline.AddLast("streamsocket", new StreamSocketDecoder());
                        pipeline.AddLast(new LoggingHandler());
                        pipeline.AddLast("framing-enc", new LengthFieldPrepender(2));
                        pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));
                        pipeline.AddLast("echo", new EchoClientHandler());
                    }));

                IChannel clientChannel = await bootstrap.ConnectAsync();
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }
    }
}
