// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    class WrappedCompositeByteBuffer : CompositeByteBuffer
    {
        readonly CompositeByteBuffer wrapped;

        internal WrappedCompositeByteBuffer(CompositeByteBuffer wrapped) : base(wrapped.Allocator)
        {
            this.wrapped = wrapped;
            this.SetMaxCapacity(this.wrapped.MaxCapacity);
        }

        public override bool Release() => this.wrapped.Release();

        public override bool Release(int decrement) => this.wrapped.Release(decrement);

        public sealed override int ReaderIndex => this.wrapped.ReaderIndex;

        public sealed override int WriterIndex => this.wrapped.WriterIndex;

        public sealed override bool IsReadable() => this.wrapped.IsReadable();

        public sealed override bool IsReadable(int numBytes) => this.wrapped.IsReadable(numBytes);

        public sealed override bool IsWritable() => this.wrapped.IsWritable();

        public sealed override int ReadableBytes => this.wrapped.ReadableBytes;

        public sealed override int WritableBytes => this.wrapped.WritableBytes;

        public sealed override int MaxWritableBytes => this.wrapped.MaxWritableBytes;

        public override int EnsureWritable(int minWritableBytes, bool force) => this.wrapped.EnsureWritable(minWritableBytes, force);

        public override short GetShort(int index) => this.wrapped.GetShort(index);

        public override short GetShortLE(int index) => this.wrapped.GetShortLE(index);

        public override int GetUnsignedMedium(int index) => this.wrapped.GetUnsignedMedium(index);

        public override int GetUnsignedMediumLE(int index) => this.wrapped.GetUnsignedMediumLE(index);

        public override int GetInt(int index) => this.wrapped.GetInt(index);

        public override int GetIntLE(int index) => this.wrapped.GetIntLE(index);

        public override long GetLong(int index) => this.wrapped.GetLong(index);

        public override long GetLongLE(int index) => this.wrapped.GetLongLE(index);

        public override char GetChar(int index) => this.wrapped.GetChar(index);

        public override IByteBuffer SetShortLE(int index, int value) => this.wrapped.SetShortLE(index, value);

        public override IByteBuffer SetMediumLE(int index, int value) => this.wrapped.SetMediumLE(index, value);

        public override IByteBuffer SetIntLE(int index, int value) => this.wrapped.SetIntLE(index, value);

        public override IByteBuffer SetLongLE(int index, long value) => this.wrapped.SetLongLE(index, value);

        public override byte ReadByte() => this.wrapped.ReadByte();

        public override short ReadShort() => this.wrapped.ReadShort();

        public override short ReadShortLE() => this.wrapped.ReadShortLE();

        public override int ReadUnsignedMedium() => this.wrapped.ReadUnsignedMedium();

        public override int ReadUnsignedMediumLE() => this.wrapped.ReadUnsignedMediumLE();

        public override int ReadInt() => this.wrapped.ReadInt();

        public override int ReadIntLE() => this.wrapped.ReadIntLE();

        public override long ReadLong() => this.wrapped.ReadLong();

        public override long ReadLongLE() => this.wrapped.ReadLongLE();

        public override IByteBuffer ReadBytes(int length) => this.wrapped.ReadBytes(length);

        public override IByteBuffer Slice() => this.wrapped.Slice();

        public override IByteBuffer Slice(int index, int length) => this.wrapped.Slice(index, length);

        public override string ToString(Encoding encoding) => this.wrapped.ToString(encoding);

        public override string ToString(int index, int length, Encoding encoding) => this.wrapped.ToString(index, length, encoding);

        public override int IndexOf(int fromIndex, int toIndex, byte value) => this.wrapped.IndexOf(fromIndex, toIndex, value);

        public override int BytesBefore(int index, int length, byte value) => this.wrapped.BytesBefore(index, length, value);

        public override int ForEachByte(IByteProcessor processor) => this.wrapped.ForEachByte(processor);

        public override int ForEachByte(int index, int length, IByteProcessor processor) => this.wrapped.ForEachByte(index, length, processor);

        public override int ForEachByteDesc(IByteProcessor processor) => this.wrapped.ForEachByteDesc(processor);

        public override int ForEachByteDesc(int index, int length, IByteProcessor processor) => this.wrapped.ForEachByteDesc(index, length, processor);

        public override int GetHashCode() => this.wrapped.GetHashCode();

        public override bool Equals(IByteBuffer buf) => this.wrapped.Equals(buf);

        public override int CompareTo(IByteBuffer that) => this.wrapped.CompareTo(that);

        public override int ReferenceCount => this.wrapped.ReferenceCount;

        public override IByteBuffer Duplicate() => this.wrapped.Duplicate();

        public override IByteBuffer ReadSlice(int length) => this.wrapped.ReadSlice(length);

        public override IByteBuffer WriteShortLE(int value) => this.wrapped.WriteShortLE(value);

        public override IByteBuffer WriteMediumLE(int value) => this.wrapped.WriteMediumLE(value);

        public override IByteBuffer WriteIntLE(int value) => this.wrapped.WriteIntLE(value);

        public override IByteBuffer WriteLongLE(long value) => this.wrapped.WriteLongLE(value);

        public override Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken) => this.wrapped.WriteBytesAsync(stream, length, cancellationToken);

        public override CompositeByteBuffer AddComponent(IByteBuffer buffer)
        {
            this.wrapped.AddComponent(buffer);
            return this;
        }

        public override CompositeByteBuffer AddComponents(params IByteBuffer[] buffers)
        {
            this.wrapped.AddComponents(buffers);
            return this;
        }

        public override CompositeByteBuffer AddComponents(IEnumerable<IByteBuffer> buffers)
        {
            this.wrapped.AddComponents(buffers);
            return this;
        }

        public override CompositeByteBuffer AddComponent(int cIndex, IByteBuffer buffer)
        {
            this.wrapped.AddComponent(cIndex, buffer);
            return this;
        }

        public override CompositeByteBuffer AddComponents(int cIndex, params IByteBuffer[] buffers)
        {
            this.wrapped.AddComponents(cIndex, buffers);
            return this;
        }

        public override CompositeByteBuffer AddComponents(int cIndex, IEnumerable<IByteBuffer> buffers)
        {
            this.wrapped.AddComponents(cIndex, buffers);
            return this;
        }

        public override CompositeByteBuffer AddComponent(bool increaseWriterIndex, IByteBuffer buffer)
        {
            this.wrapped.AddComponent(increaseWriterIndex, buffer);
            return this;
        }

        public override CompositeByteBuffer AddComponents(bool increaseWriterIndex, params IByteBuffer[] buffers)
        {
            this.wrapped.AddComponents(increaseWriterIndex, buffers);
            return this;
        }

        public override CompositeByteBuffer AddComponents(bool increaseWriterIndex, IEnumerable<IByteBuffer> buffers)
        {
            this.wrapped.AddComponents(increaseWriterIndex, buffers);
            return this;
        }

        public override CompositeByteBuffer AddComponent(bool increaseWriterIndex, int cIndex, IByteBuffer buffer)
        {
            this.wrapped.AddComponent(increaseWriterIndex, cIndex, buffer);
            return this;
        }

        public override CompositeByteBuffer RemoveComponent(int cIndex)
        {
            this.wrapped.RemoveComponent(cIndex);
            return this;
        }

        public override CompositeByteBuffer RemoveComponents(int cIndex, int numComponents)
        {
            this.wrapped.RemoveComponents(cIndex, numComponents);
            return this;
        }

        public override IEnumerator<IByteBuffer> GetEnumerator() => this.wrapped.GetEnumerator();

        public override IList<IByteBuffer> Decompose(int offset, int length) => this.wrapped.Decompose(offset, length);

        public sealed override bool HasArray => this.wrapped.HasArray;

        public sealed override byte[] Array => this.wrapped.Array;

        public sealed override int ArrayOffset => this.wrapped.ArrayOffset;

        public sealed override int Capacity => this.wrapped.Capacity;

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.wrapped.AdjustCapacity(newCapacity);
            return this;
        }

        public sealed override IByteBufferAllocator Allocator => this.wrapped.Allocator;

        public sealed override int NumComponents => this.wrapped.NumComponents;

        public sealed override int MaxNumComponents => this.wrapped.MaxNumComponents;

        public sealed override int ToComponentIndex(int offset) => this.wrapped.ToComponentIndex(offset);

        public sealed override int ToByteIndex(int cIndex) => this.wrapped.ToByteIndex(cIndex);

        public override byte GetByte(int index) => this.wrapped.GetByte(index);

        protected internal sealed override byte _GetByte(int index) => this.wrapped._GetByte(index);

        protected internal sealed override short _GetShort(int index) => this.wrapped._GetShort(index);

        protected internal sealed override short _GetShortLE(int index) => this.wrapped._GetShortLE(index);

        protected internal sealed override int _GetUnsignedMedium(int index) => this.wrapped._GetUnsignedMedium(index);

        protected internal sealed override int _GetUnsignedMediumLE(int index) => this.wrapped._GetUnsignedMediumLE(index);

        protected internal sealed override int _GetInt(int index) => this.wrapped._GetInt(index);

        protected internal sealed override int _GetIntLE(int index) => this.wrapped._GetIntLE(index);

        protected internal sealed override long _GetLong(int index) => this.wrapped._GetLong(index);

        protected internal sealed override long _GetLongLE(int index) => this.wrapped._GetLongLE(index);

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.wrapped.GetBytes(index, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.wrapped.GetBytes(index, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length) => this.wrapped.GetBytes(index, destination, length);

        public override IByteBuffer SetByte(int index, int value)
        {
            this.wrapped.SetByte(index, value);
            return this;
        }

        protected internal sealed override void _SetByte(int index, int value) => this.wrapped._SetByte(index, value);

        public override IByteBuffer SetShort(int index, int value)
        {
            this.wrapped.SetShort(index, value);
            return this;
        }

        protected internal sealed override void _SetShort(int index, int value) => this.wrapped._SetShort(index, value);

        protected internal sealed override void _SetShortLE(int index, int value) => this.wrapped._SetShortLE(index, value);

        public override IByteBuffer SetMedium(int index, int value)
        {
            this.wrapped.SetMedium(index, value);
            return this;
        }

        protected internal sealed override void _SetMedium(int index, int value) => this.wrapped._SetMedium(index, value);

        protected internal sealed override void _SetMediumLE(int index, int value) => this.wrapped._SetMediumLE(index, value);

        public override IByteBuffer SetInt(int index, int value)
        {
            this.wrapped.SetInt(index, value);
            return this;
        }

        protected internal sealed override void _SetInt(int index, int value) => this.wrapped._SetInt(index, value);

        protected internal sealed override void _SetIntLE(int index, int value) => this.wrapped._SetIntLE(index, value);

        public override IByteBuffer SetLong(int index, long value)
        {
            this.wrapped.SetLong(index, value);
            return this;
        }

        protected internal sealed override void _SetLong(int index, long value) => this.wrapped._SetLong(index, value);

        protected internal sealed override void _SetLongLE(int index, long value) => this.wrapped._SetLongLE(index, value);

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.wrapped.SetBytes(index, src, srcIndex, length);
            return this;
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.wrapped.SetBytes(index, src, srcIndex, length);
            return this;
        }

        public override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken) => this.wrapped.SetBytesAsync(index, src, length, cancellationToken);

        public override IByteBuffer Copy() => this.wrapped.Copy();

        public override IByteBuffer Copy(int index, int length) => this.wrapped.Copy(index, length);

        public sealed override IByteBuffer this[int cIndex] => this.wrapped[cIndex];

        public sealed override IByteBuffer ComponentAtOffset(int offset) => this.wrapped.ComponentAtOffset(offset);

        public sealed override IByteBuffer InternalComponent(int cIndex) => this.wrapped.InternalComponent(cIndex);

        public sealed override IByteBuffer InternalComponentAtOffset(int offset) => this.wrapped.InternalComponentAtOffset(offset);

        public override int IoBufferCount => this.wrapped.IoBufferCount;

        public override ArraySegment<byte> GetIoBuffer(int index, int length) => this.wrapped.GetIoBuffer(index, length);

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length) => this.wrapped.GetIoBuffers(index, length);

        public override CompositeByteBuffer Consolidate()
        {
            this.wrapped.Consolidate();
            return this;
        }

        public override CompositeByteBuffer Consolidate(int cIndex, int numComponents)
        {
            this.wrapped.Consolidate(cIndex, numComponents);
            return this;
        }

        public override CompositeByteBuffer DiscardReadComponents()
        {
            this.wrapped.DiscardReadComponents();
            return this;
        }

        public override IByteBuffer DiscardReadBytes()
        {
            this.wrapped.DiscardReadBytes();
            return this;
        }

        public sealed override string ToString() => this.wrapped.ToString();

        public sealed override IByteBuffer SetReaderIndex(int readerIndex)
        {
            this.wrapped.SetReaderIndex(readerIndex);
            return this;
        }

        public sealed override IByteBuffer SetWriterIndex(int writerIndex)
        {
            this.wrapped.SetWriterIndex(writerIndex);
            return this;
        }

        public sealed override IByteBuffer SetIndex(int readerIndex, int writerIndex)
        {
            this.wrapped.SetIndex(readerIndex, writerIndex);
            return this;
        }

        public sealed override IByteBuffer Clear()
        {
            this.wrapped.Clear();
            return this;
        }

        public sealed override IByteBuffer MarkReaderIndex()
        {
            this.wrapped.MarkReaderIndex();
            return this;
        }

        public sealed override IByteBuffer ResetReaderIndex()
        {
            this.wrapped.ResetReaderIndex();
            return this;
        }

        public sealed override IByteBuffer MarkWriterIndex()
        {
            this.wrapped.MarkWriterIndex();
            return this;
        }

        public sealed override IByteBuffer ResetWriterIndex()
        {
            this.wrapped.ResetWriterIndex();
            return this;
        }

        public override IByteBuffer EnsureWritable(int minWritableBytes)
        {
            this.wrapped.EnsureWritable(minWritableBytes);
            return this;
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst)
        {
            this.wrapped.GetBytes(index, dst);
            return this;
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int length)
        {
            this.wrapped.GetBytes(index, dst, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, byte[] dst)
        {
            this.wrapped.GetBytes(index, dst);
            return this;
        }

        public override IByteBuffer SetBoolean(int index, bool value)
        {
            this.wrapped.SetBoolean(index, value);
            return this;
        }

        public override IByteBuffer SetChar(int index, char value)
        {
            this.wrapped.SetChar(index, value);
            return this;
        }

        public override IByteBuffer SetFloat(int index, float value)
        {
            this.wrapped.SetFloat(index, value);
            return this;
        }

        public override IByteBuffer SetDouble(int index, double value)
        {
            this.wrapped.SetDouble(index, value);
            return this;
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src)
        {
            this.wrapped.SetBytes(index, src);
            return this;
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            this.wrapped.SetBytes(index, src, length);
            return this;
        }

        public override IByteBuffer SetBytes(int index, byte[] src)
        {
            this.wrapped.SetBytes(index, src);
            return this;
        }

        public override IByteBuffer SetZero(int index, int length)
        {
            this.wrapped.SetZero(index, length);
            return this;
        }

        public override IByteBuffer ReadBytes(IByteBuffer dst)
        {
            this.wrapped.ReadBytes(dst);
            return this;
        }

        public override IByteBuffer ReadBytes(IByteBuffer dst, int length)
        {
            this.wrapped.ReadBytes(dst, length);
            return this;
        }

        public override IByteBuffer ReadBytes(IByteBuffer dst, int dstIndex, int length)
        {
            this.wrapped.ReadBytes(dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer ReadBytes(byte[] dst)
        {
            this.wrapped.ReadBytes(dst);
            return this;
        }

        public override IByteBuffer ReadBytes(byte[] dst, int dstIndex, int length)
        {
            this.wrapped.ReadBytes(dst, dstIndex, length);
            return this;
        }

        public override ICharSequence GetCharSequence(int index, int length, Encoding encoding) => this.wrapped.GetCharSequence(index, length, encoding);

        public override ICharSequence ReadCharSequence(int length, Encoding encoding) => this.wrapped.ReadCharSequence(length, encoding);

        public override int SetCharSequence(int index, ICharSequence sequence, Encoding encoding) => this.wrapped.SetCharSequence(index, sequence, encoding);

        public override string GetString(int index, int length, Encoding encoding) => this.wrapped.GetString(index, length, encoding);

        public override string ReadString(int length, Encoding encoding) => this.wrapped.ReadString(length, encoding);

        public override int SetString(int index, string value, Encoding encoding) => this.wrapped.SetString(index, value, encoding);

        public override IByteBuffer ReadBytes(Stream destination, int length) => this.wrapped.ReadBytes(destination, length);

        public override int WriteCharSequence(ICharSequence sequence, Encoding encoding) => this.wrapped.WriteCharSequence(sequence, encoding);

        public override int WriteString(string value, Encoding encoding) => this.wrapped.WriteString(value, encoding);

        public override IByteBuffer SkipBytes(int length)
        {
            this.wrapped.SkipBytes(length);
            return this;
        }

        public override IByteBuffer WriteBoolean(bool value)
        {
            this.wrapped.WriteBoolean(value);
            return this;
        }

        public override IByteBuffer WriteByte(int value)
        {
            this.wrapped.WriteByte(value);
            return this;
        }

        public override IByteBuffer WriteShort(int value)
        {
            this.wrapped.WriteShort(value);
            return this;
        }

        public override IByteBuffer WriteMedium(int value)
        {
            this.wrapped.WriteMedium(value);
            return this;
        }

        public override IByteBuffer WriteInt(int value)
        {
            this.wrapped.WriteInt(value);
            return this;
        }

        public override IByteBuffer WriteLong(long value)
        {
            this.wrapped.WriteLong(value);
            return this;
        }

        public override IByteBuffer WriteChar(char value)
        {
            this.wrapped.WriteChar(value);
            return this;
        }

        public override IByteBuffer WriteFloat(float value)
        {
            this.wrapped.WriteFloat(value);
            return this;
        }

        public override IByteBuffer WriteDouble(double value)
        {
            this.wrapped.WriteDouble(value);
            return this;
        }

        public override IByteBuffer WriteBytes(IByteBuffer src)
        {
            this.wrapped.WriteBytes(src);
            return this;
        }

        public override IByteBuffer WriteBytes(IByteBuffer src, int length)
        {
            this.wrapped.WriteBytes(src, length);
            return this;
        }

        public override IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length)
        {
            this.wrapped.WriteBytes(src, srcIndex, length);
            return this;
        }

        public override IByteBuffer WriteBytes(byte[] src)
        {
            this.wrapped.WriteBytes(src);
            return this;
        }

        public override IByteBuffer WriteBytes(byte[] src, int srcIndex, int length)
        {
            this.wrapped.WriteBytes(src, srcIndex, length);
            return this;
        }

        public override IByteBuffer WriteZero(int length)
        {
            this.wrapped.WriteZero(length);
            return this;
        }

        public override IReferenceCounted Retain(int increment)
        {
            this.wrapped.Retain(increment);
            return this;
        }

        public override IReferenceCounted Retain()
        {
            this.wrapped.Retain();
            return this;
        }

        public override IReferenceCounted Touch()
        {
            this.wrapped.Touch();
            return this;
        }

        public override IReferenceCounted Touch(object hint)
        {
            this.wrapped.Touch(hint);
            return this;
        }

        public override IByteBuffer DiscardSomeReadBytes()
        {
            this.wrapped.DiscardSomeReadBytes();
            return this;
        }

        protected internal sealed override void Deallocate() => this.wrapped.Deallocate();

        public sealed override IByteBuffer Unwrap() => this.wrapped;
    }
}