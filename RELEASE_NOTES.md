#### 0.2.3 February 10
- Critical fix to handling of async operations when initiated from outside the event loop (#66).
- Fix to enable setting socket-related options through SetOption on Bootstrap (#68).
- build changes to allow signing assemblies

#### 0.2.2 January 30
- `ResourceLeakDetector` fix (#64)
- Assigned GUID on default internal logger `EventSource`
- `IByteBuffer.ToString(..)` for efficient string decoding directly from Byte Buffer

#### 0.2.1 December 08 2015
- fixes to EmptyByteBuffer
- ported LoggingHandler

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