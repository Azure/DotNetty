// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    class MediationStream : Stream
    {
        readonly Func<ArraySegment<byte>, Task<int>> readDataFunc;
        readonly Func<ArraySegment<byte>, Task> writeDataFunc;

        public MediationStream(Func<ArraySegment<byte>, Task<int>> readDataFunc, Func<ArraySegment<byte>, Task> writeDataFunc)
        {
            this.readDataFunc = readDataFunc;
            this.writeDataFunc = writeDataFunc;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.readDataFunc(new ArraySegment<byte>(buffer, offset, count));

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.writeDataFunc(new ArraySegment<byte>(buffer, offset, count));

#if !NETCOREAPP1_1
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var tcs = new TaskCompletionSource<int>(state);
            this.ReadAsync(buffer, offset, count, CancellationToken.None).ContinueWith(
                t =>
                {
                    tcs.TrySetResult(t.Result);
                    callback?.Invoke(tcs.Task);
                }, 
                TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }

        public override int EndRead(IAsyncResult asyncResult) => ((Task<int>)asyncResult).Result;

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var tcs = new TaskCompletionSource<int>(state);
            this.WriteAsync(buffer, offset, count, CancellationToken.None).ContinueWith(
                t =>
                {
                    tcs.TrySetResult(0);
                    callback?.Invoke(tcs.Task);
                },
                TaskContinuationOptions.ExecuteSynchronously);
            return tcs.Task;
        }

        public override void EndWrite(IAsyncResult asyncResult) => ((Task<int>)asyncResult).Wait();
#endif

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }
    }
}