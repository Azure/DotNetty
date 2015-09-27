#### 0.1.3 September 21 2015
- Fixed `TcpSocketChannel` closure on graceful socket closure 
- Better alignment of IChannel implementations to netty's expected behavior for `Open`, `Active`, `LocalAddress`, `RemoteAddress`
- Proper port of `Default/IChannelPipeline` and `AbstractChannelHandlerContext` to enable channel handlers to run on different invoker.

#### 0.1.2 August 09 2015
First public release