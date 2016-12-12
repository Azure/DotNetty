using DotNetty.Common.Platform;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.System.Diagnostics;

// UWP assembly
namespace DotNetty.Platform
{
    public class PlatformImplementation : DotNetty.Common.Platform.IPlatform
    {
        public int GetCurrentProcessId()
        {
            return (int)ProcessDiagnosticInfo.GetForCurrentProcess().ProcessId;
        }
    }
}
