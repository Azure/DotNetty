using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Codecs.CoapTcp.util
{
    interface IBytesBuildHelper<T>
    {
        BytesBuilder build(IEnumerable<T> objs, BytesBuilder builder);
    }
}
