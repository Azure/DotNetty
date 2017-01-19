// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace UWPEcho.Client
{
    using System;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Windows.ApplicationModel.Core;
    using Windows.Networking;
    using Windows.Networking.Sockets;
    using Windows.Security.Cryptography.Certificates;
    using Windows.UI.Core;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using DotNetty.Codecs;
    using DotNetty.Common.Internal;
    using DotNetty.Handlers.Logging;
    using DotNetty.Transport.Channels;
    using DotNettyTestApp;

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        readonly Runner runner = new Runner();

        public MainPage()
        {
            this.InitializeComponent();
        }

        int counter;

        public void AddElement(object element)
        {
#pragma warning disable 4014
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    this.listView.Items.Add(string.Format("[{0}] {1}", this.counter++, element));

                    int selectedIndex = this.listView.Items.Count - 1;
                    if (selectedIndex < 0)
                        return;

                    this.listView.SelectedIndex = selectedIndex;
                    this.listView.UpdateLayout();

                    this.listView.ScrollIntoView(this.listView.SelectedItem);
                });
#pragma warning restore 4014
        }

        void CheckStatus(Task operation)
        {
            operation.ContinueWith(result =>
                {
#pragma warning disable 4014
                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () => { this.statusTextBlock.Text = string.Format("Error: '{0}'", result.Exception.Message); });
#pragma warning restore 4014
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        void startButton_Click(object sender, RoutedEventArgs e)
        {
            this.CheckStatus(this.runner.StartAsync(this.AddElement));
        }

        void stopButton_Click(object sender, RoutedEventArgs e)
        {
            this.CheckStatus(this.runner.ShutdownAsync());
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
            PlatformProvider.Platform = new UWPPlatform();

            this.eventLoopGroup = new MultithreadEventLoopGroup();

            string pfxDir = "dotnetty.com.pfx";
            var cert = new X509Certificate2(pfxDir, "password");
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

            await this.eventLoopGroup.GetNext().RegisterAsync(streamSocketChannel);
        }

        public Task ShutdownAsync()
        {
            if (this.eventLoopGroup != null)
            {
                return this.eventLoopGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
            return Task.CompletedTask;
        }
    }
}