// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common;
    using DotNetty.Tests.Common;
    using Xunit;

    public class LeakDetectionTest
    {
        [Fact(Skip = "logging or GC is acting funny in xUnit console runner.")]
        public void UnderReleaseBufferLeak()
        {
            ResourceLeakDetector.DetectionLevel preservedLevel = ResourceLeakDetector.Level;
            try
            {
                ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Paranoid;
                bool observedLeak = false;
                LogTestHelper.Intercept logInterceptor = (name, level, id, message, exception) =>
                {
                    if (message.Contains("LEAK"))
                    {
                        observedLeak = true;
                    }
                };
                using (LogTestHelper.SetInterceptionLogger(logInterceptor))
                {
                    this.CreateAndForgetBuffer();

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                Assert.True(observedLeak);
            }
            finally
            {
                ResourceLeakDetector.Level = preservedLevel;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void CreateAndForgetBuffer()
        {
            IByteBuffer forgotten = PooledByteBufferAllocator.Default.Buffer(10);
        }

        [Fact]
        public void ResampleNoLeak()
        {
            ResourceLeakDetector.DetectionLevel preservedLevel = ResourceLeakDetector.Level;
            try
            {
                ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Paranoid;
                IByteBuffer buffer = PooledByteBufferAllocator.Default.Buffer(10);
                buffer.Release();
                buffer = PooledByteBufferAllocator.Default.Buffer(10);
                buffer.Release();
            }
            finally
            {
                ResourceLeakDetector.Level = preservedLevel;
            }
        }
    }
}