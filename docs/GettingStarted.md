# Getting Started

DotNetty is a cross-platform asynchronous network application framework for the .NET platform. It is based on the Netty project, which is a high-performance networking library for Java. DotNetty provides a set of reusable components for building various network protocols and applications.

## How to use DotNetty:

### 1. Install DotNetty NuGet Package

You can add DotNetty to your project using NuGet Package Manager Console:

[Refer here for more Examples](https://github.com/Azure/DotNetty/tree/dev/examples)

```bash
Install-Package DotNetty
```

### 2. Create a Server

```csharp
using System;
using System.Net;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

class Program
{
    static void Main(string[] args)
    {
        var bossGroup = new MultithreadEventLoopGroup();
        var workerGroup = new MultithreadEventLoopGroup();

        try
        {
            var bootstrap = new ServerBootstrap()
                .Group(bossGroup, workerGroup)
                .Channel<TcpServerSocketChannel>()
                .Option(ChannelOption.SoBacklog, 100)
                .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                {
                    IChannelPipeline pipeline = channel.Pipeline;
                    pipeline.AddLast(new EchoServerHandler());
                }));

            var serverChannel = bootstrap.BindAsync(new IPEndPoint(IPAddress.Any, 8888)).Result;
            Console.WriteLine("Server started on port 8888. Press Enter to exit.");
            Console.ReadLine();

            // Close the server channel
            serverChannel.CloseAsync().Wait();
        }
        finally
        {
            bossGroup.ShutdownGracefullyAsync().Wait();
            workerGroup.ShutdownGracefullyAsync().Wait();
        }
    }
}
```

### 3. Create a Server Handler

```csharp
using System;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;

public class EchoServerHandler : ChannelHandlerAdapter
{
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        var buffer = (IByteBuffer)message;
        Console.WriteLine($"Received: {buffer.ToString(Encoding.UTF8)}");

        // Echo the message back to the client
        context.WriteAsync(message);
    }

    public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
        Console.WriteLine($"Exception: {exception}");
        context.CloseAsync();
    }
}
```

### 4. Create a Client

```csharp
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Common.Concurrency;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

class Program
{
    static async Task Main(string[] args)
    {
        var group = new MultithreadEventLoopGroup();

        try
        {
            var bootstrap = new Bootstrap()
                .Group(group)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(new ActionChannelInitializer<IChannel>(channel =>
                {
                    IChannelPipeline pipeline = channel.Pipeline;
                    pipeline.AddLast(new EchoClientHandler());
                }));

            var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888);
            var channel = await bootstrap.ConnectAsync(endPoint);

            // Send a message to the server
            await channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes("Hello, DotNetty")));

            Console.WriteLine("Message sent to server. Press Enter to exit.");
            Console.ReadLine();
        }
        finally
        {
            await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
        }
    }
}
```

### 5. Create a Client Handler

```csharp
using System;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;

public class EchoClientHandler : ChannelHandlerAdapter
{
    public override void ChannelActive(IChannelHandlerContext context)
    {
        Console.WriteLine("Client connected to server.");
    }

    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
        var buffer = (IByteBuffer)message;
        Console.WriteLine($"Received from server: {buffer.ToString(Encoding.UTF8)}");
    }

    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
        Console.WriteLine($"Exception: {exception}");
        context.CloseAsync();
    }
}
```

This is a basic example of a TCP server and client using DotNetty. You can extend and modify this code based on your specific requirements and protocols. Ensure you handle exceptions properly and release resources when necessary.

## Some of the basic components of DotNetty:

    Bootstrap:
        The Bootstrap class is used to set up and configure a DotNetty application, either for a server or a client. It includes methods for configuring the EventLoopGroup, Channel type, and other settings.

    EventLoopGroup:
        EventLoopGroup is a group of EventLoops. An EventLoop is responsible for handling I/O operations such as reading and writing data. There are typically two EventLoopGroups in DotNetty: one for the server (boss group) and one for processing client requests (worker group).

    Channel:
        The Channel interface represents a communication channel in DotNetty. It abstracts the underlying transport, such as sockets, and provides a unified API for reading and writing data.

    ChannelPipeline:
        ChannelPipeline is a sequence of handlers associated with a Channel. It defines the processing pipeline for inbound and outbound data. Each handler in the pipeline processes data as it passes through.

    ChannelHandler:
        ChannelHandler is an interface that defines methods to handle various events in the lifecycle of a Channel, such as channelActive, channelRead, and exceptionCaught. Developers can implement custom handlers to extend or modify the behavior of the network application.

    ByteBuf:
        ByteBuf is DotNetty's abstraction for working with binary data. It provides a flexible and efficient way to read and write data. DotNetty uses ByteBuf instances for handling data in the network stack.

    Codec:
        Codecs in DotNetty are responsible for encoding and decoding messages. DotNetty includes various codecs for common protocols like HTTP, SSL/TLS, and more. You can also create custom codecs for your specific application.

    BootstrapConfig:
        BootstrapConfig is used to configure the settings of a Bootstrap instance. It allows you to set options like channel type, EventLoopGroup, and other parameters.

    ChannelOption:
        ChannelOption is a key-value pair that represents an option for a Channel. It is often used to configure low-level transport settings, such as socket options.

    ChannelFuture:
        ChannelFuture represents the result of an asynchronous operation on a Channel. It allows you to register listeners for events like completion or failure.

These are some of the fundamental components in DotNetty. Depending on your application's requirements, you may also encounter other specialized components and abstractions provided by DotNetty for handling tasks like SSL/TLS, UDP communication, and more.
