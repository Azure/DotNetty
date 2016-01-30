// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Diagnostics.Tracing;
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

        [Fact]
        public void UnderReleaseBufferLeak()
        {
            var eventListener = new ObservableEventListener();
            Mock<IObserver<EventEntry>> logListener = this.mockRepo.Create<IObserver<EventEntry>>();
            var eventTextFormatter = new EventTextFormatter();
            Func<EventEntry, bool> leakPredicate = y => y.TryFormatAsString(eventTextFormatter).Contains("LEAK");
            logListener.Setup(x => x.OnNext(It.Is<EventEntry>(y => leakPredicate(y)))).Verifiable();
            logListener.Setup(x => x.OnNext(It.Is<EventEntry>(y => !leakPredicate(y))));
            eventListener.Subscribe(logListener.Object);
            eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Verbose);

            var bufPool = new PooledByteBufferAllocator(100, 1000);
            IByteBuffer buffer = bufPool.Buffer(10);

            buffer = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();

            this.mockRepo.Verify();
        }

        [Fact]
        public void ResampleNoLeak()
        {
            ResourceLeakDetector.DetectionLevel preservedLevel = ResourceLeakDetector.Level;
            try
            {
                ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Paranoid;
                var bufPool = new PooledByteBufferAllocator(100, 1000);
                IByteBuffer buffer = bufPool.Buffer(10);
                buffer.Release();
                buffer = bufPool.Buffer(10);
                buffer.Release();
            }
            finally
            {
                ResourceLeakDetector.Level = preservedLevel;
            }
        }
    }
}