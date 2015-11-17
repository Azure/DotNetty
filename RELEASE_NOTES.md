#### 0.2.0 November 17 2015
- Proper Event Executor model port
- EmbeddedChannel
- Better test coverage for executor model and basic channel functionality
- Channel groups support
- Channel ID
- Complete `LengthFieldBasedFrameDecoder` and `LengthFieldPrepender`
- Resource leak detection support (basic is on by default for pooled byte buffers)
- Proper internal logging 
- Reacher byte buffer API
- Proper utilities set for byte buffers, strings, system properties
- Performance improvements in SingleThreadEventExecutor 

#### 0.1.3 September 21 2015
- Fixed `TcpSocketChannel` closure on graceful socket closure 
- Better alignment of IChannel implementations to netty's expected behavior for `Open`, `Active`, `LocalAddress`, `RemoteAddress`
- Proper port of `Default/IChannelPipeline` and `AbstractChannelHandlerContext` to enable channel handlers to run on different invoker.

#### 0.1.2 August 09 2015
First public release