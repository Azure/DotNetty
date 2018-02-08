// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    public static class PromiseExtensions
    {
        public static IPromise Unvoid(this IPromise promise) => promise.IsVoid ? new TaskCompletionSource() : promise;
    }
}