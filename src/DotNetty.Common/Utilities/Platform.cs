// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System;

    public static class Platform
    {
        private readonly static Lazy<IPlatform> Instance = new Lazy<IPlatform>(() => PlatformResolver.GetPlatform());

        public static int GetCurrentProcessId() => Instance.Value.GetCurrentProcessId();

        public static byte[] GetDefaultDeviceID() => Instance.Value.GetDefaultDeviceID();
    }
}