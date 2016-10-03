// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    class PortionedMemoryStream : Stream
    {
        readonly byte[] data;
        int position;
        readonly IEnumerable<ArraySegment<byte>> dataFeedSource;
        IEnumerator<ArraySegment<byte>> currentDataFeed;

        public PortionedMemoryStream(byte[] data, IEnumerable<int> readCounts)
        {
            this.data = data;
            this.dataFeedSource = this.DataFeedFromCountSequence(readCounts);
        }

        IEnumerable<ArraySegment<byte>> DataFeedFromCountSequence(IEnumerable<int> readCounts)
        {
            while (true)
            {
                // ReSharper disable once PossibleMultipleEnumeration
                foreach (int readCount in readCounts)
                {
                    int nextCount = readCount;
                    int maxCount = this.data.Length - this.position - 1;
                    if (nextCount >= maxCount)
                    {
                        if (maxCount > 0)
                        {
                            yield return new ArraySegment<byte>(this.data, this.position, maxCount);
                        }
                        yield break;
                    }
                    else
                    {
                        yield return new ArraySegment<byte>(this.data, this.position, readCount);
                    }
                }
            }
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.currentDataFeed == null)
            {
                this.currentDataFeed = this.dataFeedSource.GetEnumerator();
            }
            if (!this.currentDataFeed.MoveNext())
            {
                return 0;
            }
            else
            {
                ArraySegment<byte> current = this.currentDataFeed.Current;
                int finalCount = Math.Min(count, current.Count);
                Array.Copy(current.Array, current.Offset, buffer, offset, finalCount);
                this.position += finalCount;
                return finalCount;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => this.data.Length;

        public override long Position
        {
            get { return this.position; }
            set
            {
                this.position = (int)value;
                if (this.currentDataFeed != null)
                {
                    this.currentDataFeed.Dispose();
                    this.currentDataFeed = null;
                }
            }
        }
    }
}