// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Utilities
{
    public interface IReadFinishedSignal
    {
        bool Finished { get; }

        void Signal();
    }
}