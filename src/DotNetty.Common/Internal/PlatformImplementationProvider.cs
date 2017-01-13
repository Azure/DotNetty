// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System;
    using System.Reflection;

    public static class PlatformImplementationProvider
    {
        static IPlatform platform;
        static object syncRoot = new Object();

        public static IPlatform Platform
        {
            get
            {
                lock (syncRoot)
                {
                    if (platform == null)
                    {
                        platform = new DotNetty.Common.Internal.PlatformImplementation();
                    }
                    return platform;
                }
            }

            set
            {
                lock (syncRoot)
                {
                    platform = value;
                }
            }
        }
    }
}