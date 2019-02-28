// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;

    static class PlatformApi
    {
        const int AF_INET6_LINUX = 10;
        const int AF_INET6_OSX = 30;

        internal static uint GetAddressFamily(AddressFamily addressFamily)
        {
            // AF_INET 2
            if (addressFamily == AddressFamily.InterNetwork || RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return (uint)addressFamily;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return AF_INET6_LINUX;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return AF_INET6_OSX;
            }

            throw new InvalidOperationException($"Address family : {addressFamily} platform : {RuntimeInformation.OSDescription} not supported");
        }

        internal static bool GetReuseAddress(TcpHandle tcpHandle)
        {
            IntPtr socketHandle = GetSocketHandle(tcpHandle);

            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? WindowsApi.GetReuseAddress(socketHandle) 
                : UnixApi.GetReuseAddress(socketHandle);
        }

        internal static void SetReuseAddress(TcpHandle tcpHandle, int value)
        {
            IntPtr socketHandle = GetSocketHandle(tcpHandle);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsApi.SetReuseAddress(socketHandle, value);
            }
            else
            {
                UnixApi.SetReuseAddress(socketHandle, value);
            }
        }

        internal static bool GetReusePort(TcpHandle tcpHandle)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetReuseAddress(tcpHandle);
            }

            IntPtr socketHandle = GetSocketHandle(tcpHandle);
            return UnixApi.GetReusePort(socketHandle);
        }

        internal static void SetReusePort(TcpHandle tcpHandle, int value)
        {
            IntPtr socketHandle = GetSocketHandle(tcpHandle);
            // Ignore SO_REUSEPORT on Windows because it is controlled
            // by SO_REUSEADDR
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            UnixApi.SetReusePort(socketHandle, value);
        }

        static IntPtr GetSocketHandle(TcpHandle handle)
        {
            IntPtr socket = IntPtr.Zero;
            NativeMethods.uv_fileno(handle.Handle, ref socket);
            return socket;
        }
    }
}
