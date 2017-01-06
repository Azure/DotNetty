// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
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

    public enum DotNetPlatform
    {
        DotNetStandard13,
        UWP
    }

    public static class PlatformSupportLevel
    {
        public static DotNetPlatform Value { get; set; } = DotNetPlatform.DotNetStandard13; // DotNetStandard is the default, UWP must set explicitly
    }

    static class PlatformResolver
    {
        static string GetPlatformSpecificAssemblyName()
        {
            if (PlatformSupportLevel.Value == DotNetPlatform.DotNetStandard13)
            {
                return "DotNetty.Common";   // The calling assembly
            }
            return "DotNetty.Platform.UWP"; // The satellite assembly containing UWP support
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
