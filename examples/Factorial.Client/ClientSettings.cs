// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Factorial.Client
{
    using Examples.Common;

    public class ClientSettings : Examples.Common.ClientSettings
    {
        public static int Count => int.Parse(ExampleHelper.Configuration["count"]);
    }
}