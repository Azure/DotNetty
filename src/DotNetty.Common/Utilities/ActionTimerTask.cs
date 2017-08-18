// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    public class ActionTimerTask : ITimerTask
    {
        readonly Action<ITimeout> action;

        public ActionTimerTask(Action<ITimeout> action) => this.action = action;

        public void Run(ITimeout timeout) => this.action(timeout);
    }
}