// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace UWPEcho.Client
{
    using Windows.Storage.Streams;
    using Windows.System.Diagnostics;
    using Windows.System.Profile;
    using DotNetty.Common.Internal;

    class UWPPlatform : IPlatform
    {
        int IPlatform.GetCurrentProcessId() => (int)ProcessDiagnosticInfo.GetForCurrentProcess().ProcessId;

        byte[] IPlatform.GetDefaultDeviceId()
        {
            var signature = new byte[8];
            int index = 0;
            HardwareToken hardwareToken = HardwareIdentification.GetPackageSpecificToken(null);
            using (DataReader dataReader = DataReader.FromBuffer(hardwareToken.Id))
            {
                int offset = 0;
                while (offset < hardwareToken.Id.Length && index < 7)
                {
                    var hardwareEntry = new byte[4];
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