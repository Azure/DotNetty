#### 0.6.0 October 9, 2018
- Clearly marks Unsafe Buffer management routines as `unsafe`
- Changes defaults for Unpooled and Pooled buffer allocators to safe versions
- Fixes write buffer handling (#423)

#### 0.5.0 August 14, 2018
- Web Socket support
- Aligned execution service model
- Fix for synchronous socket connection establishment on .NET Core 2.1
- TlsHandler fixes
- Fix to scheduled task cancellation
- XML Doc updates

#### 0.4.8 April 24, 2018
- Unsafe direct buffers
- HTTP 1.1 codec
- FlowControlHandler
- Channel pool
- Better Buffer-String integration
- Better shutdown handling for sockets
- Realigned Redis codec
- Fixes to LenghtFieldPrepender, LengthFieldBasedDecoder
- Fixes to libuv-based transport
- Fixes to buffer management on flush for .NET Core
- Fixes to ResourceLeakDetector

#### 0.4.6 August 2 2017
- Small fixes (#259, #260, #264, #266)
- Properly handling handshake with AutoRead = false when Read is not issued by upstream handlers in pipeline (#263)
- Proper exception handling in TcpServerSocketChannel to retry accept instead of closing (#272)

#### 0.4.5 May 15 2017
- Support for Medium and Unsigned Medium types (#244)
- Support for Float (Single) type and Zeroing API (#209)
- Hashed Wheel Timer (#242)
- Fix for unintended concurrent flush (#218), silent failures during TLS handshake (#225)

#### 0.4.4 March 31 2017
- Added SNI support
- Fixed assembly metadata

#### 0.4.3 March 21 2017
- Extended support for .NET 4.5
- Fix to PooledByteBufferAllocator to promptly release freed chunks for GC
- Ability to limit overall PooledByteBufferAllocator capacity
- Updated dependencies

#### 0.4.2 February 9 2017
- Better alignment with .NET Standard and portability (esp UWP support)
- New tooling

#### 0.4.1 January 26 2017
- Introduced Platform allowing for alternative implementations of platform-specific concepts.
- STEE and others use Task-based "thread" abstraction.

#### 0.4.0 November 25 2016
- .NET Standard 1.3 support.
- Libraries are strong-named by default.
- Redis codec.
- Protocol Buffers 2 and 3 codecs.
- Socket Datagram Channel.
- Base64 encoder and decoder.
- STEE uses ConcurrentQueue by default (queue impl is pluggable now).

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