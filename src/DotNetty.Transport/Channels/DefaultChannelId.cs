using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;


namespace DotNetty.Transport.Channels
{
    [Serializable]
    public class DefaultChannelId : IChannelId
    {
        //private static final InternalLogger logger = InternalLoggerFactory.getInstance(DefaultChannelId.class);
        private static readonly Regex MachineIdPattern = new Regex("^(?:[0-9a-fA-F][:-]?){6,8}$");
        private const int MachineIdLen = 8;
        private static readonly byte[] MachineId;
        private const int ProcessIdLen = 4;
        // Maximal value for 64bit systems is 2^22.  See man 5 proc.
        // See https://github.com/netty/netty/issues/2706
        private const int MaxProcessId = 4194304;
        private static readonly int ProcessId;
        private const int SequenceLen = 4;
        private const int TimestampLen = 8;
        private const int RandomLen = 4;

        private static int nextSequence = 0;

        private static int seed = (int)(Stopwatch.GetTimestamp() & 0xFFFFFFFF);//used to safly cast long to int, because the timestamp returned is long and it doesn't fit into an int
        private static readonly ThreadLocal<Random> threadLocalRandom = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));//used to simulate java ThreadLocalRandom

        private readonly byte[] data = new byte[MachineIdLen + ProcessIdLen + SequenceLen + TimestampLen + RandomLen];
        private int hashCode;

        [NonSerialized]
        private string shortValue;
        [NonSerialized]
        private string longValue;

        static DefaultChannelId()
        {
            int processId = -1;
            string customProcessId = SystemPropertyUtil.Get("io.netty.processId");
            if (customProcessId != null)
                int.TryParse(customProcessId, out processId);
            if (processId < 0 || processId > MaxProcessId)
                processId = -1;
            if (processId < 0)
                processId = DefaultProcessId();
            ProcessId = processId;
            byte[] machineId = null;
            string customMachineId = SystemPropertyUtil.Get("io.netty.machineId");
            if(customMachineId!=null)
            {
                if(MachineIdPattern.Match(customMachineId).Success)
                {
                    machineId = ParseMachineId(customMachineId);
                }
            }

            if(machineId==null)
            {
                machineId = DefaultMachineId();
            }
            MachineId = machineId;
        }

        private static byte[] ParseMachineId(string value)
        {
            // Strip separators.
            value = value.Replace("[:-]", "");
            byte[] machineId = new byte[MachineIdLen];
            for (int i = 0; i < value.Length; i += 2)
            {
                machineId[i] = (byte)int.Parse(value.Substring(i, i + 2),NumberStyles.AllowHexSpecifier);
            }
            return machineId;
        }

        private static int DefaultProcessId()
        {
            int pId = -1;
            try
            {
                pId = Process.GetCurrentProcess().Id;
            }
            catch { }

            if(pId<0)
            {
                pId = threadLocalRandom.Value.Next(MaxProcessId + 1);
            }
            return pId;

        }

        public static DefaultChannelId NewInstance()
        {
            DefaultChannelId id = new DefaultChannelId();
            id.Init();
            return id;
        }

        private static byte[] DefaultMachineId()
        {
            // Find the best MAC address available.
            byte[] notFound = unchecked(new byte[] { (byte)-1 });
            byte[] bestMacAddr = notFound;
            IPAddress bestIpAddr = IPAddress.Loopback;
            var ifaces = new SortedDictionary<NetworkInterface, IPAddress>();
            try
            {
                foreach(NetworkInterface iface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var addrs = iface.GetIPProperties().UnicastAddresses;
                    var addr = addrs.Where(a=>!IPAddress.IsLoopback(a.Address)).FirstOrDefault();
                    if (addr!=null)
                    {
                        ifaces.Add(iface, addr.Address);
                    }
                }
            }
            //catch (SocketException e)
            //{
            //    logger.warn("Failed to retrieve the list of available network interfaces", e);
            //}
            catch { }

            foreach(var entry in ifaces)
            {
                NetworkInterface iface = entry.Key;
                IPAddress addr = entry.Value;
                //todo check if the iface is virtual(there is no equivvalent method in .Net like in java)
                byte[] macAddr = iface.GetPhysicalAddress().GetAddressBytes();
                bool replace = false;
                int res = CompareAddresses(bestMacAddr, macAddr);
                if(res<0)
                {
                    replace = true;
                }
                else if(res==0)
                {
                    res = CompareAddresses(bestIpAddr, addr);
                    if(res<0)
                    {
                        replace = true;
                    }
                    else if(res==0)
                    {
                        if (bestMacAddr.Length < macAddr.Length)
                            replace = true;
                    }
                }

                if(replace)
                {
                    bestMacAddr = macAddr;
                    bestIpAddr = addr;
                }
            }

            if(bestMacAddr==notFound)
            {
                bestMacAddr = new byte[MachineIdLen];
                threadLocalRandom.Value.NextBytes(bestMacAddr);
            }

            switch(bestMacAddr.Length)
            {
                case 6: // EUI-48 - convert to EUI-64
                    byte[] newAddr = new byte[MachineIdLen];
                    Array.Copy(bestMacAddr, 0, newAddr, 0, 3);
                    newAddr[3] = (byte)0xFF;
                    newAddr[4] = (byte)0xFE;
                    Array.Copy(bestMacAddr, 3, newAddr, 5, 3);
                    bestMacAddr = newAddr;
                    break;
                default: // Unknown
                    bestMacAddr = bestMacAddr.Take(MachineIdLen).ToArray();
                    break;
            }
            return bestMacAddr;

        }

        private static int CompareAddresses(byte[] current, byte[] candidate)
        {
            if (candidate == null)
            {
                return 1;
            }

            // Must be EUI-48 or longer.
            if (candidate.Length < 6)
            {
                return 1;
            }

            // Must not be filled with only 0 and 1.
            bool onlyZeroAndOne = true;
            foreach (byte b in candidate)
            {
                if (b != 0 && b != 1)
                {
                    onlyZeroAndOne = false;
                    break;
                }
            }

            if (onlyZeroAndOne)
            {
                return 1;
            }

            // Must not be a multicast address
            if ((candidate[0] & 1) != 0)
            {
                return 1;
            }

            // Prefer globally unique address.
            if ((current[0] & 2) == 0)
            {
                if ((candidate[0] & 2) == 0)
                {
                    // Both current and candidate are globally unique addresses.
                    return 0;
                }
                else
                {
                    // Only current is globally unique.
                    return 1;
                }
            }
            else
            {
                if ((candidate[0] & 2) == 0)
                {
                    // Only candidate is globally unique.
                    return -1;
                }
                else
                {
                    // Both current and candidate are non-unique.
                    return 0;
                }
            }
        }

        private static int CompareAddresses(IPAddress current, IPAddress candidate)
        {
            return ScoreAddress(current) - ScoreAddress(candidate);
        }

        private static int ScoreAddress(IPAddress addr)
        {
            if (IPAddress.IsLoopback(addr))
            {
                return 0;
            }
            if (addr.IsIPv6Multicast)
            {
                return 1;
            }
            if (addr.IsIPv6LinkLocal)
            {
                return 2;
            }
            if (addr.IsIPv6SiteLocal)
            {
                return 3;
            }

            return 4;
        }


        public string AsShortText
        {
            get {

                string shortValue = this.shortValue;
                if(shortValue==null)
                {
                    this.shortValue = shortValue = ByteBufferUtil.HexDump(data, MachineIdLen + ProcessIdLen + SequenceLen + TimestampLen, RandomLen);
                }
                return shortValue;
            }
        }

        public string AsLongText
        {
            get 
            {
                string longValue = this.longValue;
                if(longValue==null)
                {
                    this.longValue = longValue = NewLongValue();
                }
                return longValue;
            }
        }

        public int CompareTo(IChannelId other)
        {
            return 0;
        }

        private string NewLongValue()
        {
            StringBuilder buf = new StringBuilder(2 * data.Length + 5);
            int i = 0;
            i = AppendHexDumpField(buf, i, MachineIdLen);
            i = AppendHexDumpField(buf, i, ProcessIdLen);
            i = AppendHexDumpField(buf, i, SequenceLen);
            i = AppendHexDumpField(buf, i, TimestampLen);
            i = AppendHexDumpField(buf, i, RandomLen);
            Debug.Assert(i == data.Length);
            return buf.ToString().Substring(0, buf.Length - 1);
        }

        private int AppendHexDumpField(StringBuilder buf, int i, int length)
        {
            buf.Append(ByteBufferUtil.HexDump(data, i, length));
            buf.Append('-');
            i += length;
            return i;
        }

        private void Init()
        {
            int i = 0;
            // machineId
            Array.Copy(MachineId, 0, data, i, MachineIdLen);
            i += MachineIdLen;

            // processId
            i = WriteInt(i, ProcessId);

            // sequence
            i = WriteInt(i, Interlocked.Increment(ref nextSequence));

            // timestamp (kind of)
            long ticks = Stopwatch.GetTimestamp();
            long nanos = (ticks / Stopwatch.Frequency) * 1000000000;
            long millis = (ticks / Stopwatch.Frequency) * 1000;
            i = WriteLong(i, ByteBufferUtil.SwapLong(nanos) ^ millis);

            // random
            int random = threadLocalRandom.Value.Next();
            hashCode = random;
            i = WriteInt(i, random);

            Debug.Assert(i == data.Length);

        }

        private int WriteInt(int i, int value)
        {
            data[i++] = (byte)((int)((uint)value >> 24));
            data[i++] = (byte)((int)((uint)value >> 16));
            data[i++] = (byte)((int)((uint)value >> 8));
            data[i++] = (byte)value;
            return i;
        }

        private int WriteLong(int i,long value)
        {
            data[i++] = (byte)((long)((ulong)value >> 56));
            data[i++] = (byte)((long)((ulong)value >> 48));
            data[i++] = (byte)((long)((ulong)value >> 40));
            data[i++] = (byte)((long)((ulong)value >> 32));
            data[i++] = (byte)((long)((ulong)value >> 24));
            data[i++] = (byte)((long)((ulong)value >> 16));
            data[i++] = (byte)((long)((ulong)value >> 8));
            data[i++] = (byte)value;
            return i;
        }

        public override int GetHashCode()
        {
            return hashCode;
        }


        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (!(obj is DefaultChannelId))
            {
                return false;
            }

            return Array.Equals(data, ((DefaultChannelId)obj).data);
        }

        public override string ToString()
        {
            return  AsShortText;
        }
    }
}
