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
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWPEcho.Client
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        Runner runner = new Runner();

        public MainPage()
        {
            this.InitializeComponent();
        }

        int counter = 0;
        public void AddElement(object element)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    listView.Items.Add(string.Format("[{0}] {1}", counter++, element));

                    var selectedIndex = listView.Items.Count - 1;
                    if (selectedIndex < 0)
                        return;

                    listView.SelectedIndex = selectedIndex;
                    listView.UpdateLayout();

                    listView.ScrollIntoView(listView.SelectedItem);
                });
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            runner.StartAsync(AddElement);
        }

        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            runner.ShutdownAsync();
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

        MultithreadEventLoopGroup eventLoopGroup;

        public async Task StartAsync(Action<object> logger)
        {
            DotNetty.Common.PlatformSupportLevel.Value = DotNetty.Common.DotNetPlatform.UWP;

            this.eventLoopGroup = new MultithreadEventLoopGroup();

            string pfxDir = "dotnetty.com.pfx";
            X509Certificate2 cert = new X509Certificate2(pfxDir, "password");
            string targetHost = cert.GetNameInfo(X509NameType.DnsName, false);

            Func<IChannel> channelFactory = () => new StreamSocketChannel(
                                new HostName(ClientSettings.Host.ToString()),
                                ClientSettings.Port.ToString(),
                                new HostName(targetHost));

            var bootstrap = new Bootstrap();

            bootstrap
                .Group(eventLoopGroup)
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
                    pipeline.AddLast("echo", new EchoClientHandler(logger));
                }));

            IChannel clientChannel = await bootstrap.ConnectAsync();
        }

        public Task ShutdownAsync()
        {
            return eventLoopGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
        }
    }
}
