// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;

    [AttributeUsage(AttributeTargets.Method)]
    public class PipelinePropagationAttribute : Attribute
    {
        public PipelinePropagationAttribute(PropagationDirections direction)
        {
            this.Direction = direction;
        }

        public PropagationDirections Direction { get; set; }
    }
}