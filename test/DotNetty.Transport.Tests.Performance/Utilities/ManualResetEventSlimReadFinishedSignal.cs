// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Utilities
{
    using System.Threading;

    public class ManualResetEventSlimReadFinishedSignal : IReadFinishedSignal
    {
        readonly ManualResetEventSlim manualResetEventSlim;

        public ManualResetEventSlimReadFinishedSignal(ManualResetEventSlim manualResetEventSlim)
        {
            this.manualResetEventSlim = manualResetEventSlim;
        }

        public void Signal() => this.manualResetEventSlim.Set();

        public bool Finished => this.manualResetEventSlim.IsSet;
    }
}