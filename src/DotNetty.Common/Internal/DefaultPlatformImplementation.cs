// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System.Diagnostics;

    class DefaultPlatform : IPlatform
    {
        int IPlatform.GetCurrentProcessId() => Process.GetCurrentProcess().Id;

        byte[] IPlatform.GetDefaultDeviceId() => MacAddressUtil.GetBestAvailableMac();
    }
}