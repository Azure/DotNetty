// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Platform
{
    using System;
    using System.Reflection;

    public class PlatformImplementationNotFound : Exception
    {
        public PlatformImplementationNotFound(string assemblyName) : 
            base($"The assembly {assemblyName} containing platform-specific implementation cannot be found. Make sure the assembly is located in the application's current directory/Appx")
        {
        }
    }

    public static class PlatformResolver
    {
        private static string GetPlatformSpecificAssemblyName()
        {
            try
            {
                Assembly.Load(new AssemblyName("System.Threading.Thread"));
            }
            catch (System.IO.FileNotFoundException)
            {
                // This platform does not have System.Threading.Thread, it must be UWP
                return "DotNetty.Platform.UWP";
            }
            return "DotNetty.Platform.NETStandard";
        }

        public static IPlatform GetPlatform()
        {
            // Load the assembly that contains the implementation of this platform
            var assemblyName = GetPlatformSpecificAssemblyName();
            Assembly assembly;
            try
            {
                assembly = Assembly.Load(new AssemblyName(assemblyName));
            }
            catch
            {
                throw new PlatformImplementationNotFound(assemblyName);
            }

            // Get the type from the assembly that implements the platform
            Type type = assembly.GetType("DotNetty.Platform.PlatformImplementation");
            object instance = Activator.CreateInstance(type);
            return (IPlatform)instance;
        }
    }
}
