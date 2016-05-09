// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Runtime.CompilerServices;
    using DotNetty.Common;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
    using Moq;
    using Xunit;

    public class LeakDetectionTest
    {
        readonly MockRepository mockRepo = new MockRepository(MockBehavior.Strict);

        [Fact(Skip = "logging or GC is acting funny in xUnit console runner.")]
        public void UnderReleaseBufferLeak()
        {
            ResourceLeakDetector.DetectionLevel preservedLevel = ResourceLeakDetector.Level;
            try
            {
                ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Paranoid;
                var eventListener = new ObservableEventListener();
                Mock<IObserver<EventEntry>> logListener = this.mockRepo.Create<IObserver<EventEntry>>();
                var eventTextFormatter = new EventTextFormatter();
                Func<EventEntry, bool> leakPredicate = y => y.TryFormatAsString(eventTextFormatter).Contains("LEAK");
                logListener.Setup(x => x.OnNext(It.Is<EventEntry>(y => leakPredicate(y)))).Verifiable();
                logListener.Setup(x => x.OnNext(It.Is<EventEntry>(y => !leakPredicate(y))));
                eventListener.Subscribe(logListener.Object);
                eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Verbose);

                this.CreateAndForgetBuffer();

                GC.Collect();
                GC.WaitForPendingFinalizers();

                this.mockRepo.Verify();
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