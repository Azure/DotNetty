#### 0.3.3 November 04 2016
- .NET Standard 1.3 support.
- Libraries are strong-named by default.
- Redis codec.

#### 0.3.2 June 22 2016
- Better API alignment with final version of netty 4.1 (#125).
- Exposed API for flexible TlsHandler initialization (#132, #134).

#### 0.3.1 June 01 2016
- Port of IdleStateHandler, ReadTimeoutHandler, WriteTimeoutHandler (#98).
- Fixes and optimization in TlsHandler (#116).
- Port of AdaptiveRecvByteBufAllocator enabling flexible sizing of read buffer (#117).
- Support for adding Attributes on Channel (#114).
- Proper xml-doc configuration (#120).

#### 0.3.0 May 13 2016
- BREAKING CHANGE: default byte buffer is now PooledByteBufferAllocator (unless overriden through environment variable).
- Port of PooledByteBuffer (support for flexible buffer sizes).
- Enables sending of multiple buffers in a single socket call.
- Refreshed DefaultChannelPipeline, AbstractChannelHandlerContext.
- Port of JsonObjectDecoder, DelimeterBasedFrameDecoder.
- Fixes to async sending in TcpSocketChannel.
- IoBufferCount, GetIoBuffer(s) introduced in IByteBuffer.

#### 0.2.6 April 27 2016
- TlsHandler negotiates TLS 1.0+ on server side (#89).
- STEE properly supports graceful shutdown (#7).
- UnpooledHeapByteBuffer.GetBytes honors received index and length (#88).
- Port of MessageToMessageDecoder, LineBasedFrameDecoder, StringDecoder, StringEncoder, ByteProcessor and ForEachByte family of methods on Byte Buffers (#86).

#### 0.2.5 April 14 2016
- Fixes regression in STEE where while evaluation of idling timeout did not account for immediately pending scheduled tasks (#83).

#### 0.2.4 April 07 2016
- Proper handling of pooled buffer growth beyond max capacity of buffer in pool (fixing #71).
- Improved pooling of buffers when a buffer was released in other thread (#73).
- Introduction of IEventExecutor.Schedule and proper cancellation of scheduled tasks (#80).
- Better handling of wake-ups for scheduled tasks (#81).
- Default internal logging initialization is deferred to allow override it completely (#80 extra).
- Honoring `IByteBuffer.ArrayOffset` in `IByteBuffer.ToString(Encoding)` (#80 extra).

#### 0.2.3 February 10 2016
- Critical fix to handling of async operations when initiated from outside the event loop (#66).
- Fix to enable setting socket-related options through SetOption on Bootstrap (#68).
- build changes to allow signing assemblies

#### 0.2.2 January 30 2016
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