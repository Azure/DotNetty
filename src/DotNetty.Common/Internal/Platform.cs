// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System;
    using DotNetty.Common.Internal;

    public static class Platform
    {
        public static int GetCurrentProcessId() => PlatformImplementationProvider.Platform.GetCurrentProcessId();

        public static byte[] GetDefaultDeviceId() => PlatformImplementationProvider.Platform.GetDefaultDeviceId();
    }
}