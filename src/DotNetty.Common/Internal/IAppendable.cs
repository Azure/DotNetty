// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using DotNetty.Common.Utilities;

    public interface IAppendable
    {
        IAppendable Append(char c);

        IAppendable Append(ICharSequence sequence);

        IAppendable Append(ICharSequence sequence, int start, int end);
    }
}
