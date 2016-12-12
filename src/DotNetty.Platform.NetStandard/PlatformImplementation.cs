using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

// .NET Standard assembly
namespace DotNetty.Platform
{
    public class PlatformImplementation : DotNetty.Common.Platform.IPlatform
    {
        public int GetCurrentProcessId()
        {
            return Process.GetCurrentProcess().Id;
        }
    }
}
