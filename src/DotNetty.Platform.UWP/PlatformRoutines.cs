using System;
using System.Collections.Generic;
using System.Text;
using Windows.System.Diagnostics;

// UWP assembly
namespace DotNetty.Platform
{
    public class PlatformRoutines
    {
        public static int GetCurrentProcessId() => (int)ProcessDiagnosticInfo.GetForCurrentProcess().ProcessId;
    }
}
