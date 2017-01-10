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
using Windows.Networking.Sockets;
using Windows.Security.Cryptography.Certificates;

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
#pragma warning disable 4014
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
#pragma warning restore 4014
        }

        private void CheckStatus(Task operation)
        {
            operation.ContinueWith(result =>
            {
#pragma warning disable 4014
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        statusTextBlock.Text = string.Format("Error: '{0}'", result.Exception.Message);
                    });
#pragma warning restore 4014
            },
            TaskContinuationOptions.OnlyOnFaulted);
        }

        void startButton_Click(object sender, RoutedEventArgs e)
        {
            CheckStatus(runner.StartAsync(AddElement));
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            CheckStatus(runner.ShutdownAsync());
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

            var streamSocket = new StreamSocket();
            await streamSocket.ConnectAsync(new HostName(ClientSettings.Host.ToString()), ClientSettings.Port.ToString(), SocketProtectionLevel.PlainSocket);
            streamSocket.Control.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);
            await streamSocket.UpgradeToSslAsync(SocketProtectionLevel.Tls12, new HostName(targetHost));

            var streamSocketChannel = new StreamSocketChannel(streamSocket);

            streamSocketChannel.Pipeline.AddLast(new LoggingHandler());
            streamSocketChannel.Pipeline.AddLast("framing-enc", new LengthFieldPrepender(2));
            streamSocketChannel.Pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));
            streamSocketChannel.Pipeline.AddLast("echo", new EchoClientHandler(logger));

            await eventLoopGroup.GetNext().RegisterAsync(streamSocketChannel);
        }

        public Task ShutdownAsync()
        {
            if (eventLoopGroup != null)
            {
                return eventLoopGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
            return Task.CompletedTask;
        }
    }
}
