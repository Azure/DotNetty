// Credit http://stackoverflow.com/questions/2196767/c-implementing-networkstream-peek/7281113#7281113
namespace DotNetty.Handlers.Tls
{
    using System;
    using System.IO;

    /// <summary>
    /// PeekableStream wraps a Stream and can be used to peek ahead in the underlying stream,
    /// without consuming the bytes. In other words, doing Peek() will allow you to look ahead in the stream,
    /// but it won't affect the result of subsequent Read() calls.
    /// 
    /// This is sometimes necessary, e.g. for peeking at the magic number of a stream of bytes and decide which
    /// stream processor to hand over the stream.
    /// </summary>
    public class PeekableStream : Stream
    {
        readonly Stream underlyingStream;
        readonly byte[] lookAheadBuffer;

        int lookAheadIndex;

        public PeekableStream(Stream underlyingStream, int maxPeekBytes)
        {
            this.underlyingStream = underlyingStream;
            this.lookAheadBuffer = new byte[maxPeekBytes];
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                this.underlyingStream.Dispose();

            base.Dispose(disposing);
        }

        /// <summary>
        /// Peeks at a maximum of count bytes, or less if the stream ends before that number of bytes can be read.
        /// 
        /// Calls to this method do not influence subsequent calls to Read() and Peek().
        /// 
        /// Please note that this method will always peek count bytes unless the end of the stream is reached before that - in contrast to the Read()
        /// method, which might read less than count bytes, even though the end of the stream has not been reached.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and
        /// (offset + number-of-peeked-bytes - 1) replaced by the bytes peeked from the current source.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data peeked from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be peeked from the current stream.</param>
        /// <returns>The total number of bytes peeked into the buffer. If it is less than the number of bytes requested then the end of the stream has been reached.</returns>
        public virtual int Peek(byte[] buffer, int offset, int count)
        {
            if (count > this.lookAheadBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count), "must be smaller than peekable size, which is " + this.lookAheadBuffer.Length);

            while (this.lookAheadIndex < count)
            {
                int bytesRead = this.underlyingStream.Read(this.lookAheadBuffer, this.lookAheadIndex, count - this.lookAheadIndex);

                if (bytesRead == 0) // end of stream reached
                    break;

                this.lookAheadIndex += bytesRead;
            }

            int peeked = Math.Min(count, this.lookAheadIndex);
            Array.Copy(this.lookAheadBuffer, 0, buffer, offset, peeked);
            return peeked;
        }

        public override bool CanRead { get { return true; } }

        public override long Position
        {
            get
            {
                return this.underlyingStream.Position - this.lookAheadIndex;
            }
            set
            {
                this.underlyingStream.Position = value;
                this.lookAheadIndex = 0; // this needs to be done AFTER the call to underlyingStream.Position, as that might throw NotSupportedException, 
                                    // in which case we don't want to change the lookAhead status
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesTakenFromLookAheadBuffer = 0;
            if (count > 0 && this.lookAheadIndex > 0)
            {
                bytesTakenFromLookAheadBuffer = Math.Min(count, this.lookAheadIndex);
                Array.Copy(this.lookAheadBuffer, 0, buffer, offset, bytesTakenFromLookAheadBuffer);
                count -= bytesTakenFromLookAheadBuffer;
                offset += bytesTakenFromLookAheadBuffer;
                this.lookAheadIndex -= bytesTakenFromLookAheadBuffer;
                if (this.lookAheadIndex > 0) // move remaining bytes in lookAheadBuffer to front
                                        // copying into same array should be fine, according to http://msdn.microsoft.com/en-us/library/z50k9bft(v=VS.90).aspx :
                                        // "If sourceArray and destinationArray overlap, this method behaves as if the original values of sourceArray were preserved
                                        // in a temporary location before destinationArray is overwritten."
                    Array.Copy(this.lookAheadBuffer, this.lookAheadBuffer.Length - bytesTakenFromLookAheadBuffer + 1, this.lookAheadBuffer, 0, this.lookAheadIndex);
            }

            return count > 0
                ? bytesTakenFromLookAheadBuffer + this.underlyingStream.Read(buffer, offset, count)
                : bytesTakenFromLookAheadBuffer;
        }

        public override int ReadByte()
        {
            if (this.lookAheadIndex > 0)
            {
                this.lookAheadIndex--;
                byte firstByte = this.lookAheadBuffer[0];
                if (this.lookAheadIndex > 0) // move remaining bytes in lookAheadBuffer to front
                    Array.Copy(this.lookAheadBuffer, 1, this.lookAheadBuffer, 0, this.lookAheadIndex);
                return firstByte;
            }
            else
            {
                return this.underlyingStream.ReadByte();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long ret = this.underlyingStream.Seek(offset, origin);
            this.lookAheadIndex = 0; // this needs to be done AFTER the call to underlyingStream.Seek(), as that might throw NotSupportedException,
                                // in which case we don't want to change the lookAhead status
            return ret;
        }

        // from here on, only simple delegations to underlyingStream

        public override bool CanSeek { get { return this.underlyingStream.CanSeek; } }
        public override bool CanWrite { get { return this.underlyingStream.CanWrite; } }
        public override bool CanTimeout { get { return this.underlyingStream.CanTimeout; } }
        public override int ReadTimeout { get { return this.underlyingStream.ReadTimeout; } set { this.underlyingStream.ReadTimeout = value; } }
        public override int WriteTimeout { get { return this.underlyingStream.WriteTimeout; } set { this.underlyingStream.WriteTimeout = value; } }
        public override void Flush() {
            this.underlyingStream.Flush(); }
        public override long Length { get { return this.underlyingStream.Length; } }
        public override void SetLength(long value) {
            this.underlyingStream.SetLength(value); }
        public override void Write(byte[] buffer, int offset, int count) {
            this.underlyingStream.Write(buffer, offset, count); }
        public override void WriteByte(byte value) {
            this.underlyingStream.WriteByte(value); }
    }
}