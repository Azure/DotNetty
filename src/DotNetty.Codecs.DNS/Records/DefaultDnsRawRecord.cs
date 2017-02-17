using System;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Common;
using System.Reflection;

namespace DotNetty.Codecs.DNS.Records
{
    public class DefaultDnsRawRecord : AbstractDnsRecord, IDnsRawRecord
    {
        public IByteBuffer Content { get; }

        public int ReferenceCount { get; }

        public DefaultDnsRawRecord(string name, DnsRecordType type, long timeToLive,
            IByteBuffer content) : this(name, type, DnsRecordClass.IN, timeToLive, content)
        {
        }

        public DefaultDnsRawRecord(string name, DnsRecordType type, DnsRecordClass dnsClass,
            long timeToLive, IByteBuffer content) : base(name, type, timeToLive, dnsClass)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public IByteBufferHolder Copy()
        {
            return Replace(Content.Copy());
        }

        public IByteBufferHolder Duplicate()
        {
            return Replace(Content.Duplicate());
        }

        public bool Release()
        {
            return Content.Release();
        }

        public bool Release(int decrement)
        {
            return Content.Release(decrement);
        }

        public IReferenceCounted Retain()
        {
            Content.Retain();
            return this;
        }

        public IReferenceCounted Retain(int increment)
        {
            Content.Retain(increment);
            return this;
        }

        public IReferenceCounted Touch()
        {
            Content.Touch();
            return this;
        }

        public IReferenceCounted Touch(object hint)
        {
            Content.Touch(hint);
            return this;
        }

        private IDnsRawRecord Replace(IByteBuffer content)
        {
            return new DefaultDnsRawRecord(Name, Type, DnsClass, TimeToLive, content);
        }

        public override string ToString()
        {
            var builder = new StringBuilder(64);
            builder.Append(GetType().GetTypeInfo().Name).Append('(');

            if (Type != DnsRecordType.OPT)
            {
                builder.Append(string.IsNullOrWhiteSpace(Name) ? "<root>" : Name)
                    .Append(' ')
                    .Append(TimeToLive)
                    .Append(' ')
                    .AppendRecordClass(DnsClass)
                    .Append(' ')
                    .Append(Type.Name);
            }
            else
            {
                builder.Append("OPT flags:")
                    .Append(TimeToLive)
                    .Append(" udp:")
                    .Append(DnsClass);
            }

            builder.Append(' ')
                .Append(Content.ReadableBytes)
                .Append("B)");

            return builder.ToString();
        }
    }
}
