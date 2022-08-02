// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET5_0_OR_GREATER
namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using System.Threading.Tasks;

    partial class TlsHandler
    {
        sealed class MediationStreamNet : MediationStreamBase
        {
            readonly CompositeSource source = new();
            TaskCompletionSource<int> readCompletionSource;
            Memory<byte> sslOwnedMemory;
            int readByteCount;

            public MediationStreamNet(TlsHandler owner)
                : base(owner)
            {
            }

            public override bool SourceIsReadable => this.source.IsReadable;
            public override int SourceReadableBytes => this.source.GetTotalReadableBytes();

            public override void SetSource(byte[] source, int offset) => this.source.AddSource(source, offset);
            public override void ResetSource() => this.source.ResetSources();
            
            public override void ExpandSource(int count)
            {
                this.source.Expand(count);

                Memory<byte> sslMemory = this.sslOwnedMemory;
                if (sslMemory.IsEmpty)
                {
                    return;
                }

                this.sslOwnedMemory = default;

                this.readByteCount = this.ReadFromInput(sslMemory);
                // hack: this tricks SslStream's continuation to run synchronously instead of dispatching to TP. Remove once Begin/EndRead are available. 
                new Task(
                        ms =>
                        {
                            var self = (MediationStreamNet)ms;
                            TaskCompletionSource<int> p = self.readCompletionSource;
                            self.readCompletionSource = null;
                            p.TrySetResult(self.readByteCount);
                        },
                        this)
                    .RunSynchronously(TaskScheduler.Default);
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                => this.owner.capturedContext.Executor.InEventLoop
                    ? this.InLoopReadAsync(buffer, cancellationToken)
                    : new ValueTask<int>(this.OutOfLoopReadAsync(buffer, cancellationToken));

            ValueTask<int> InLoopReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            {
                if (this.SourceIsReadable)
                {
                    // we have the bytes available upfront - write out synchronously
                    int read = this.ReadFromInput(buffer);
                    return new ValueTask<int>(read);
                }

                Contract.Assert(this.sslOwnedMemory.IsEmpty);
                // take note of buffer - we will pass bytes there once available
                this.sslOwnedMemory = buffer;
                this.readCompletionSource = new TaskCompletionSource<int>();
                return new ValueTask<int>(this.readCompletionSource.Task);
            }
            
            Task<int> OutOfLoopReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            {
                return this.owner.capturedContext.Executor.SubmitAsync(
                    () =>
                    {
                        if (this.SourceIsReadable)
                        {
                            // we have the bytes available upfront - write out synchronously
                            int read = this.ReadFromInput(buffer);
                            return Task.FromResult(read);
                        }

                        Contract.Assert(this.sslOwnedMemory.IsEmpty);
                        // take note of buffer - we will pass bytes there once available
                        this.sslOwnedMemory = buffer;
                        this.readCompletionSource = new TaskCompletionSource<int>();
                        return this.readCompletionSource.Task;
                    },
                    cancellationToken).Unwrap();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (this.owner.capturedContext.Executor.InEventLoop)
                {
                    this.owner.FinishWrap(buffer, offset, count);
                }
                else
                {
                    this.owner.capturedContext.Executor.Execute(() => this.owner.FinishWrap(buffer, offset, count));
                }
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (this.owner.capturedContext.Executor.InEventLoop)
                {
                    return this.owner.FinishWrapNonAppDataAsync(buffer, offset, count);
                }

                return this.owner.capturedContext.Executor.SubmitAsync(
                    () => this.owner.FinishWrapNonAppDataAsync(buffer, offset, count),
                    cancellationToken
                ).Unwrap();
            }

            public override void Flush()
            {
                // NOOP: called on SslStream.Close
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    TaskCompletionSource<int> p = this.readCompletionSource;
                    if (p != null)
                    {
                        this.readCompletionSource = null;
                        p.TrySetResult(0);
                    }
                }
            }
            
            int ReadFromInput(Memory<byte> destination) => this.source.Read(destination);

            #region Source

            sealed class Source
            {
                byte[] input;
                int startOffset;
                int offset;
                int length;
                bool retained;

                public Source(byte[] input, int offset)
                {
                    this.input = input;
                    this.startOffset = offset;
                    this.offset = 0;
                    this.length = 0;
                }

                public int ReadableBytes => this.length - this.offset;

                public bool IsReadable => this.ReadableBytes > 0;

                public void Expand(int count)
                {
                    Contract.Assert(!this.retained); // Retained source is not expected to be Expanded
                    this.length += count;
                    Contract.Assert(this.length <= this.input.Length);
                }

                public int Read(Memory<byte> destination)
                {
                    int len = Math.Min(this.ReadableBytes, destination.Length);
                    new ReadOnlySpan<byte>(this.input, this.startOffset + this.offset, len).CopyTo(destination.Span);
                    this.offset += len;
                    return len;
                }

                // This is to avoid input bytes to be discarded by ref counting mechanism
                public void Retain()
                {
                    int readableBytes = this.ReadableBytes;
                    if (this.retained || readableBytes <= 0)
                    {
                        return;
                    }
                    
                    // todo: is there a way to not discard those bytes till they are read??? If not, then use context.Allocator???
                    
                    // Copy only readable bytes to a new buffer
                    byte[] copy = new byte[readableBytes];
                    Buffer.BlockCopy(this.input, this.startOffset + this.offset, copy, 0, readableBytes);
                    this.input = copy;
                    
                    // Set both offsets to 0 and length to readableBytes (so that this.ReadableBytes stays the same)
                    this.startOffset = 0;
                    this.offset = 0;
                    this.length = readableBytes;
                    
                    this.retained = true;
                }
            }

            sealed class CompositeSource
            {
                // Why not List?
                // 1. It's unlikely this list to grow more than 10 nodes. In fact in most cases it'll have one element only
                // 2. Cleanup removes from head, so it's cheaper compared to List which shifts elements in this case. 
                readonly LinkedList<Source> sources = new LinkedList<Source>();

                public bool IsReadable
                {
                    get
                    {
                        // The composite source is readable if any readable sources, so
                        // it's enough to check on last one as we always AddLast
                        LinkedListNode<Source> last = this.sources.Last;
                        return last != null && last.Value.IsReadable;
                    }
                }

                public void AddSource(byte[] input, int offset)
                {
                    // Always add to the tail
                    this.sources.AddLast(new Source(input, offset));
                }

                public void Expand(int count)
                {
                    Contract.Assert(this.sources.Last != null); // AddSource is always called before
                    
                    // Always expand the last added source
                    this.sources.Last.Value.Expand(count);
                }

                public int GetTotalReadableBytes()
                {
                    int count = 0;
                    
                    LinkedListNode<Source> node = this.sources.First;
                    while (node != null)
                    {
                        count += node.Value.ReadableBytes;
                        node = node.Next;
                    }

                    return count;
                }

                // Read from all readable sources to the destination starting from head (oldest)
                public int Read(Memory<byte> destination)
                {
                    int totalRead = 0;

                    LinkedListNode<Source> node = this.sources.First;
                    while (node != null && totalRead < destination.Length)
                    {
                        Source source = node.Value;
                        int read = source.Read(destination.Slice(totalRead, destination.Length - totalRead));
                        totalRead += read;

                        if (!source.IsReadable)
                        {
                            node = node.Next;
                            // Do not remove the node here as it can be expanded. Instead,
                            // remove in the CleanUp method below
                        }
                    }

                    return totalRead;
                }

                // Remove all not readable sources and retain readable. Start from first as it's the oldest
                public void ResetSources()
                {
                    LinkedListNode<Source> node = this.sources.First;
                    while (node != null)
                    {
                        if (!node.Value.IsReadable)
                        {
                            this.sources.RemoveFirst();
                            node = this.sources.First;
                        }
                        else
                        {
                            node.Value.Retain();
                            node = node.Next;
                        }
                    }
                }
            }

            #endregion
        }
    }
}
#endif