using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Common.Tests
{
    using DotNetty.Common.Utilities;
    using Moq;
    using Xunit;

    public class ResourceLeakDetectorTest
    {
        [Fact]
        public void TestLeak()
        {
            var refCnt = new Mock<IReferenceCounted>(MockBehavior.Strict);
            refCnt.Setup(x => x.Touch()).Verifiable();
            ReferenceCountUtil.

        }
    }
}
