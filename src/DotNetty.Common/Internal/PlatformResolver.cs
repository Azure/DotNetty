// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System;
    using System.Reflection;

    public class PlatformImplementationNotFound : Exception
    {
        public PlatformImplementationNotFound(string assemblyName)
            : base($"The assembly {assemblyName} containing platform-specific implementation cannot be found. Make sure the assembly is located in the application's current directory/Appx")
        {
        }
    }

    public static class PlatformImplementationDetails
    {
        public static string AssemblyName { get; set; } = "DotNetty.Common"; // This assembly
        public static string TypeName { get; set; }     = "DotNetty.Common.Internal.PlatformImplementation";
    }

    static class PlatformResolver
    {
        public static IPlatform GetPlatform()
        {
            // Load the assembly that contains the implementation of this platform
            string assemblyName = PlatformImplementationDetails.AssemblyName;
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
            Type type = assembly.GetType(PlatformImplementationDetails.TypeName);
            object instance = Activator.CreateInstance(type);
            return (IPlatform)instance;
        }
    }
}