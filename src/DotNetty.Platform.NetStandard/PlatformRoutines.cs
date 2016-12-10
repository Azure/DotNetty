using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

// .NET Standard assembly
namespace DotNetty.Platform
{
    public class PlatformRoutines
    {
        public static int GetCurrentProcessId() => Process.GetCurrentProcess().Id;
    }
}
