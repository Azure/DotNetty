// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;

    [Serializable]
    sealed class DefaultChannelId : IChannelId
    {
        const int MachineIdLen = 8;
        const int ProcessIdLen = 4;
        // Maximal value for 64bit systems is 2^22.  See man 5 proc.
        // See https://github.com/netty/netty/issues/2706
        const int MaxProcessId = 4194304;
        const int SequenceLen = 4;
        const int TimestampLen = 8;
        const int RandomLen = 4;
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<DefaultChannelId>();
        static readonly Regex MachineIdPattern = new Regex("^(?:[0-9a-fA-F][:-]?){6,8}$");
        static readonly byte[] MachineId;
        static readonly int ProcessId;
        static int nextSequence;
        static int seed = (int)(Stopwatch.GetTimestamp() & 0xFFFFFFFF); //used to safly cast long to int, because the timestamp returned is long and it doesn't fit into an int
        static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed))); //used to simulate java ThreadLocalRandom
        readonly byte[] data = new byte[MachineIdLen + ProcessIdLen + SequenceLen + TimestampLen + RandomLen];
        int hashCode;

        [NonSerialized]
        string longValue;

        [NonSerialized]
        string shortValue;

        static DefaultChannelId()
        {
            int processId = -1;
            string customProcessId = SystemPropertyUtil.Get("io.netty.processId");
            if (customProcessId != null)
            {
                if (!int.TryParse(customProcessId, out processId))
                {
                    processId = -1;
                }
            }
            if (processId < 0 || processId > MaxProcessId)
            {
                processId = -1;
                Logger.Warn("-Dio.netty.processId: {0} (malformed)", customProcessId);
            }
            else if (Logger.DebugEnabled)
            {
                Logger.Debug("-Dio.netty.processId: {0} (user-set)", processId);
            }
            if (processId < 0)
            {
                processId = DefaultProcessId();
            }
            ProcessId = processId;
            byte[] machineId = null;
            string customMachineId = SystemPropertyUtil.Get("io.netty.machineId");
            if (customMachineId != null)
            {
                if (MachineIdPattern.Match(customMachineId).Success)
                {
                    machineId = ParseMachineId(customMachineId);
                }
            }

            if (machineId == null)
            {
                machineId = DefaultMachineId();
            }
            MachineId = machineId;
        }

        public string AsShortText()
        {
            string asShortText = this.shortValue;
            if (asShortText == null)
            {
                this.shortValue = asShortText = ByteBufferUtil.HexDump(this.data, MachineIdLen + ProcessIdLen + SequenceLen + TimestampLen, RandomLen);
            }

            return asShortText;
        }

        public string AsLongText()
        {
            string asLongText = this.longValue;
            if (asLongText == null)
            {
                this.longValue = asLongText = this.NewLongValue();
            }
            return asLongText;
        }

        public int CompareTo(IChannelId other)
        {
            return 0;
        }

        static byte[] ParseMachineId(string value)
        {
            // Strip separators.
            value = value.Replace("[:-]", "");
            var machineId = new byte[MachineIdLen];
            for (int i = 0; i < value.Length; i += 2)
            {
                machineId[i] = (byte)int.Parse(value.Substring(i, i + 2), NumberStyles.AllowHexSpecifier);
            }
            return machineId;
        }

        static int DefaultProcessId()
        {
            int pId = Process.GetCurrentProcess().Id;

            if (pId <= 0)
            {
                pId = ThreadLocalRandom.Value.Next(MaxProcessId + 1);
            }
            return pId;
        }

        public static DefaultChannelId NewInstance()
        {
            var id = new DefaultChannelId();
            id.Init();
            return id;
        }

        static byte[] DefaultMachineId()
        {
            // Find the best MAC address available.
            byte[] notFound = { byte.MaxValue };
            byte[] bestMacAddr = notFound;
            IPAddress bestIpAddr = IPAddress.Loopback;
            var ifaces = new SortedDictionary<NetworkInterface, IPAddress>();
            try
            {
                foreach (NetworkInterface iface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    UnicastIPAddressInformationCollection addrs = iface.GetIPProperties().UnicastAddresses;
                    UnicastIPAddressInformation addr = addrs.FirstOrDefault(a => !IPAddress.IsLoopback(a.Address));
                    if (addr != null)
                    {
                        ifaces.Add(iface, addr.Address);
                    }
                }
            }
            catch (SocketException e)
            {
                Logger.Warn("Failed to retrieve the list of available network interfaces", e);
            }
            catch
            {
                // ignored
            }

            foreach (KeyValuePair<NetworkInterface, IPAddress> entry in ifaces)
            {
                NetworkInterface iface = entry.Key;
                IPAddress addr = entry.Value;
                //todo check if the iface is virtual(there is no equivvalent method in .Net like in java)
                byte[] macAddr = iface.GetPhysicalAddress().GetAddressBytes();
                bool replace = false;
                int res = CompareAddresses(bestMacAddr, macAddr);
                if (res < 0)
                {
                    replace = true;
                }
                else if (res == 0)
                {
                    res = CompareAddresses(bestIpAddr, addr);
                    if (res < 0)
                    {
                        replace = true;
                    }
                    else if (res == 0)
                    {
                        if (bestMacAddr.Length < macAddr.Length)
                        {
                            replace = true;
                        }
                    }
                }

                if (replace)
                {
                    bestMacAddr = macAddr;
                    bestIpAddr = addr;
                }
            }

            if (bestMacAddr == notFound)
            {
                bestMacAddr = new byte[MachineIdLen];
                ThreadLocalRandom.Value.NextBytes(bestMacAddr);
            }

            switch (bestMacAddr.Length)
            {
                case 6: // EUI-48 - convert to EUI-64
                    var newAddr = new byte[MachineIdLen];
                    Array.Copy(bestMacAddr, 0, newAddr, 0, 3);
                    newAddr[3] = 0xFF;
                    newAddr[4] = 0xFE;
                    Array.Copy(bestMacAddr, 3, newAddr, 5, 3);
                    bestMacAddr = newAddr;
                    break;
                default: // Unknown
                    bestMacAddr = bestMacAddr.Take(MachineIdLen).ToArray();
                    break;
            }
            return bestMacAddr;
        }

        static int CompareAddresses(byte[] current, byte[] candidate)
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

        static int CompareAddresses(IPAddress current, IPAddress candidate)
        {
            return ScoreAddress(current) - ScoreAddress(candidate);
        }

        static int ScoreAddress(IPAddress addr)
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

        string NewLongValue()
        {
            var buf = new StringBuilder(2 * this.data.Length + 5);
            int i = 0;
            i = this.AppendHexDumpField(buf, i, MachineIdLen);
            i = this.AppendHexDumpField(buf, i, ProcessIdLen);
            i = this.AppendHexDumpField(buf, i, SequenceLen);
            i = this.AppendHexDumpField(buf, i, TimestampLen);
            i = this.AppendHexDumpField(buf, i, RandomLen);
            Debug.Assert(i == this.data.Length);
            return buf.ToString().Substring(0, buf.Length - 1);
        }

        int AppendHexDumpField(StringBuilder buf, int i, int length)
        {
            buf.Append(ByteBufferUtil.HexDump(this.data, i, length));
            buf.Append('-');
            i += length;
            return i;
        }

        void Init()
        {
            int i = 0;
            // machineId
            Array.Copy(MachineId, 0, this.data, i, MachineIdLen);
            i += MachineIdLen;

            // processId
            i = this.WriteInt(i, ProcessId);

            // sequence
            i = this.WriteInt(i, Interlocked.Increment(ref nextSequence));

            // timestamp (kind of)
            long ticks = Stopwatch.GetTimestamp();
            long nanos = (ticks / Stopwatch.Frequency) * 1000000000;
            long millis = (ticks / Stopwatch.Frequency) * 1000;
            i = this.WriteLong(i, ByteBufferUtil.SwapLong(nanos) ^ millis);

            // random
            int random = ThreadLocalRandom.Value.Next();
            this.hashCode = random;
            i = this.WriteInt(i, random);

            Debug.Assert(i == this.data.Length);
        }

        int WriteInt(int i, int value)
        {
            uint val = (uint)value;
            this.data[i++] = (byte)(val >> 24);
            this.data[i++] = (byte)(val >> 16);
            this.data[i++] = (byte)(val >> 8);
            this.data[i++] = (byte)value;
            return i;
        }

        int WriteLong(int i, long value)
        {
            ulong val = (ulong)value;
            this.data[i++] = (byte)(val >> 56);
            this.data[i++] = (byte)(val >> 48);
            this.data[i++] = (byte)(val >> 40);
            this.data[i++] = (byte)(val >> 32);
            this.data[i++] = (byte)(val >> 24);
            this.data[i++] = (byte)(val >> 16);
            this.data[i++] = (byte)(val >> 8);
            this.data[i++] = (byte)value;
            return i;
        }

        public override int GetHashCode()
        {
            return this.hashCode;
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

            return Equals(this.data, ((DefaultChannelId)obj).data);
        }

        public override string ToString()
        {
            return this.AsShortText();
        }
    }
}