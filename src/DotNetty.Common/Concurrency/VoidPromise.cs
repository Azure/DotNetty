// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    public sealed class VoidPromise : IPromise
    {
        public static readonly IPromise Instance = new VoidPromise();

        static readonly Exception Error = new InvalidOperationException("No operations are allowed on void promise");

        VoidPromise()
        {
            this.Task = TaskEx.FromException(Error);
        }

        public Task Task { get; }

        public bool IsVoid => true;

        public bool TryComplete() => false;

        public void Complete() => throw Error;

        public bool TrySetException(Exception ex) => false;

        public bool TrySetException(IEnumerable<Exception> ex) => false;

        public void SetException(Exception ex) => throw Error;

        public bool TrySetCanceled() => false;

        public void SetCanceled() => throw Error;

        public bool SetUncancellable() => false;

        public override string ToString() => "VoidPromise";
    }
}