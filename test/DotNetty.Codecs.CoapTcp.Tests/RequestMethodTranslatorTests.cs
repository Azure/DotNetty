using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Codecs.CoapTcp.Tests
{
    [TestClass]
    public class RequestMethodTranslatorTests
    {
        [TestMethod]
        public void TranslateCodeTest()
        {
            byte[] codes = { 0xE1, 0xE2, 0xE3, 0xE4, 0xE5 };
            RequestMethodTranslator.Translate(code)
        }
    }
}
