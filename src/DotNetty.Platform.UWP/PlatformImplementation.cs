using DotNetty.Common;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.System.Diagnostics;
using Windows.System.Profile;
using Windows.Storage.Streams;

// UWP assembly
namespace DotNetty.Platform
{
    class PlatformImplementation : IPlatform
    {
        int IPlatform.GetCurrentProcessId()
        {
            return (int)ProcessDiagnosticInfo.GetForCurrentProcess().ProcessId;
        }

        byte[] IPlatform.GetDefaultDeviceID()
        {
            byte[] signature = new byte[8];
            int index = 0;
            var hardwareToken = HardwareIdentification.GetPackageSpecificToken(null);
            using (var dataReader = DataReader.FromBuffer(hardwareToken.Id))
            {
                int offset = 0;
                while (offset < hardwareToken.Id.Length && index < 7)
                {
                    byte[] hardwareEntry = new byte[4];
                    dataReader.ReadBytes(hardwareEntry);
                    byte componentID = hardwareEntry[0];
                    byte componentIDReserved = hardwareEntry[1];

                    if (componentIDReserved == 0)
                    {
                        switch (componentID)
                        {
                            // Per guidance in http://msdn.microsoft.com/en-us/library/windows/apps/jj553431
                            case 1: // CPU
                            case 2: // Memory
                            case 4: // Network Adapter
                            case 9: // Bios
                                signature[index++] = hardwareEntry[2];
                                signature[index++] = hardwareEntry[3];
                                break;
                            default:
                                break;
                        }
                    }
                    offset += 4;
                }
            }
            return signature;
        }
    }
}
