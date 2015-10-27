using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Codecs.CoapTcp
{
    public enum RequestMethod
    {
        GET=1,
        POST=2,
        PUT=3,
        DELETE=4,
        OTHER=-1,
    }
}
