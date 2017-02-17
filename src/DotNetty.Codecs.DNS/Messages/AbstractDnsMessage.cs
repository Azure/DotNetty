using DotNetty.Codecs.DNS.Records;
using DotNetty.Common;
using DotNetty.Common.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DotNetty.Codecs.DNS.Messages
{
    public class AbstractDnsMessage : AbstractReferenceCounted, IDnsMessage
    {
        private static readonly ResourceLeakDetector leakDetector = ResourceLeakDetector.Create<IDnsMessage>();
        private readonly IResourceLeak leak;
        private const DnsSection SECTION_QUESTION = DnsSection.QUESTION;
        private const int SECTION_COUNT = 4;
        private object questions;
        private object answers;
        private object authorities;
        private object additionals;

        public int Id { get; set; }
        public DnsOpCode OpCode { get; set; }
        public bool IsRecursionDesired { get; set; }
        public int Z { get; set; }

        protected AbstractDnsMessage(int id) : this(id, DnsOpCode.QUERY) { }

        protected AbstractDnsMessage(int id, DnsOpCode opcode)
        {
            Id = id;
            OpCode = opcode;
            leak = leakDetector.Open(this);
        }

        public int Count()
        {
            int count = 0;
            for (int i = 0; i < SECTION_COUNT; i++)
            {
                count += Count((DnsSection)i);
            }
            return count;
        }

        public int Count(DnsSection section)
        {
            object records = SectionAt(section);
            if (records == null)
                return 0;

            if (records is IDnsRecord)
                return 1;

            List<IDnsRecord> recordList = (List<IDnsRecord>)records;
            return recordList.Count;
        }

        private object SectionAt(DnsSection section)
        {
            switch (section)
            {
                case DnsSection.QUESTION:
                    return questions;
                case DnsSection.ANSWER:
                    return answers;
                case DnsSection.AUTHORITY:
                    return authorities;
                case DnsSection.ADDITIONAL:
                    return additionals;
                default:
                    return null;
            }
        }

        public void AddRecord(DnsSection section, IDnsRecord record)
        {
            CheckQuestion(section, record);

            object records = SectionAt(section);
            if (records == null)
            {
                SetSection(section, record);
                return;
            }

            List<IDnsRecord> recordList;
            if (records is IDnsRecord)
            {
                recordList = new List<IDnsRecord>(2);
                recordList.Add((IDnsRecord)records);
                recordList.Add(record);
                SetSection(section, recordList);
                return;
            }

            recordList = (List<IDnsRecord>)records;
            recordList.Add(record);
        }

        public void AddRecord(DnsSection section, int index, IDnsRecord record)
        {
            CheckQuestion(section, record);

            object records = SectionAt(section);
            if (records == null)
            {
                if (index != 0)
                    throw new IndexOutOfRangeException($"index: {index} (expected: 0)");

                SetSection(section, record);
                return;
            }

            List<IDnsRecord> recordList;
            if (records is IDnsRecord)
            {
                if (index == 0)
                {
                    recordList = new List<IDnsRecord>();
                    recordList.Add(record);
                    recordList.Add((IDnsRecord)records);
                }
                else if (index == 1)
                {
                    recordList = new List<IDnsRecord>();
                    recordList.Add((IDnsRecord)records);
                    recordList.Add(record);
                }
                else
                {
                    throw new IndexOutOfRangeException($"index: {index} (expected: 0 or 1)");
                }
                SetSection(section, recordList);
                return;
            }

            recordList = (List<IDnsRecord>)records;
            recordList[index] = record;
        }

        public void Clear(DnsSection section)
        {
            object recordOrList = SectionAt(section);
            SetSection(section, null);

            if (recordOrList is IReferenceCounted)
            {
                ((IReferenceCounted)recordOrList).Release();
            }
            else if (recordOrList is IList)
            {
                List<IDnsRecord> list = (List<IDnsRecord>)recordOrList;
                if (list.Count == 0)
                {
                    foreach (var r in list)
                    {
                        ReferenceCountUtil.Release(r);
                    }
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < SECTION_COUNT; i++)
            {
                Clear((DnsSection)i);
            }
        }

        public TRecord GetRecord<TRecord>(DnsSection section) where TRecord : IDnsRecord
        {
            object records = SectionAt(section);
            if (records == null)
                return default(TRecord);

            if (records is IDnsRecord)
                return (TRecord)records;

            List<IDnsRecord> recordList = (List<IDnsRecord>)records;
            if (recordList.Count == 0)
                return default(TRecord);

            return (TRecord)recordList[0];
        }

        public TRecord GetRecord<TRecord>(DnsSection section, int index) where TRecord : IDnsRecord
        {
            object records = SectionAt(section);
            if (records == null)
                throw new IndexOutOfRangeException($"index: {index} (expected: none)");

            if (records is IDnsRecord)
            {
                if (index == 0)
                    return (TRecord)records;

                throw new IndexOutOfRangeException($"index: {index} (expected: 0)");
            }

            List<IDnsRecord> recordList = (List<IDnsRecord>)records;
            return (TRecord)recordList[index];
        }

        public void RemoveRecord(DnsSection section, int index)
        {
            object records = SectionAt(section);
            if (records == null)
                throw new IndexOutOfRangeException($"index: {index} (expected: none)");

            if (records is IDnsRecord)
            {
                if (index != 0)
                    throw new IndexOutOfRangeException($"index: {index} (expected: 0)");

                SetSection(section, null);
            }

            List<IDnsRecord> recordList = (List<IDnsRecord>)records;
            recordList.RemoveAt(index);
        }

        public void SetRecord(DnsSection section, IDnsRecord record)
        {
            Clear(section);
            SetSection(section, record);
        }

        public void SetRecord(DnsSection section, int index, IDnsRecord record)
        {
            CheckQuestion(section, record);

            object records = SectionAt(section);
            if (records == null)
                throw new IndexOutOfRangeException($"index: {index} (expected: none)");

            if (records is IDnsRecord)
            {
                if (index == 0)
                {
                    SetSection(section, record);
                }
                else
                {
                    throw new IndexOutOfRangeException($"index: {index} (expected: 0)");
                }
            }

            List<IDnsRecord> recordList = (List<IDnsRecord>)records;
            recordList[index] = record;
        }

        private void SetSection(DnsSection section, object value)
        {
            switch (section)
            {
                case DnsSection.QUESTION:
                    questions = value;
                    break;
                case DnsSection.ANSWER:
                    answers = value;
                    break;
                case DnsSection.AUTHORITY:
                    authorities = value;
                    break;
                case DnsSection.ADDITIONAL:
                    additionals = value;
                    break;
            }
        }

        private static void CheckQuestion(DnsSection section, IDnsRecord record)
        {
            if (section == SECTION_QUESTION &&
                record != null &&
                !(record is IDnsQuestion))
                throw new ArgumentException($"record: {record} (expected: DnsQuestion)");
        }

        public override IReferenceCounted Touch(object hint)
        {
            if (leak != null)
                leak.Record(hint);

            return this;
        }

        protected override void Deallocate()
        {
            Clear();
            if (leak != null)
                leak.Close(); 
        }

        public override bool Equals(object obj)
        {
            if (this == obj) return true;

            if (!(obj is IDnsMessage)) return false;

            IDnsMessage that = (IDnsMessage)obj;
            if (Id != that.Id)
                return false;

            if (this is IDnsQuestion)
            {
                if (!(that is IDnsQuestion))
                    return false;
            }
            else if (that is IDnsQuestion)
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return Id * 31 + (this is IDnsQuestion ? 0 : 1);
        }
    }
}
