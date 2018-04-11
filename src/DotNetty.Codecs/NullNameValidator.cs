// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;

    public sealed class NullNameValidator<T> : INameValidator<T>
    {
        public void ValidateName(T name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
        }
    }
}
