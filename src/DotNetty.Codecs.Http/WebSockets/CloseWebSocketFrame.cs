// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public class CloseWebSocketFrame : WebSocketFrame
    {
        public CloseWebSocketFrame() 
            : base(Unpooled.Buffer(0))
        {
        }

        public CloseWebSocketFrame(int statusCode, ICharSequence reasonText)
            : this(true, 0, statusCode, reasonText)
        {
        }

        public CloseWebSocketFrame(bool finalFragment, int rsv)
            : this(finalFragment, rsv, Unpooled.Buffer(0))
        {
        }

        public CloseWebSocketFrame(bool finalFragment, int rsv, int statusCode, ICharSequence reasonText)
            : base(finalFragment, rsv, NewBinaryData(statusCode, reasonText))
        {
        }

        static IByteBuffer NewBinaryData(int statusCode, ICharSequence reasonText)
        {
            if (reasonText == null)
            {
                reasonText = StringCharSequence.Empty;
            }

            IByteBuffer binaryData = Unpooled.Buffer(2 + reasonText.Count);
            binaryData.WriteShort(statusCode);
            if (reasonText.Count > 0)
            {
                binaryData.WriteCharSequence(reasonText, Encoding.UTF8);
            }

            binaryData.SetReaderIndex(0);
            return binaryData;
        }

        public CloseWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, binaryData)
        {
        }

        ///<summary>
        ///    Returns the closing status code as per http://tools.ietf.org/html/rfc6455#section-7.4 RFC 6455. 
        ///    If a getStatus code is set, -1 is returned.
        /// </summary>
        public int StatusCode()
        {
            IByteBuffer binaryData = this.Content;
            if (binaryData == null || binaryData.Capacity == 0)
            {
                return -1;
            }

            binaryData.SetReaderIndex(0);
            int statusCode = binaryData.ReadShort();
            binaryData.SetReaderIndex(0);

            return statusCode;
        }

        ///<summary>
        ///     Returns the reason text as per http://tools.ietf.org/html/rfc6455#section-7.4 RFC 6455
        ///     If a reason text is not supplied, an empty string is returned.
        /// </summary>
        public ICharSequence ReasonText()
        {
            IByteBuffer binaryData = this.Content;
            if (binaryData == null || binaryData.Capacity <= 2)
            {
                return StringCharSequence.Empty;
            }

            binaryData.SetReaderIndex(2);
            string reasonText = binaryData.ToString(Encoding.UTF8);
            binaryData.SetReaderIndex(0);

            return new StringCharSequence(reasonText);
        }

        public override IByteBufferHolder Replace(IByteBuffer content) => new CloseWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
