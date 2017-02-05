namespace DotNetty.Rpc.Service
{
    using System.Reflection;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class ModuleRegistrations
    {
        private static readonly AssemblyManager AssemblyManager = new AssemblyManager();

        public static IModule[] FindModules()
        {
            var modulelList = new List<IModule>();

            Assembly[] assemblies = AssemblyManager.GetAssemblies().Where(IsAssembleIncludingType).ToArray();

            if (!assemblies.Any())
                throw new Exception("modules");

            Type moduleBaseType = typeof(AbstractModule);
            var moduleTypes = new List<Type>();

            foreach (Assembly assembly in assemblies)
            {
                Module[] modules = assembly.GetLoadedModules();
                foreach (Module module in modules)
                {
                    IEnumerable<Type> types =
                        module.GetTypes()
                            .Where(type => type.IsClass && !type.IsAbstract && !type.IsGenericTypeDefinition);
                    foreach (Type type in types)
                    {
                        if (moduleBaseType.IsAssignableFrom(type))
                            moduleTypes.Add(type);
                        else
                        {
                            continue;
                        }
                    }
                }
            }

            if (!moduleTypes.Any()) return modulelList.ToArray();

            foreach (Type moduleType in moduleTypes)
            {
                var module = Activator.CreateInstance(moduleType) as IModule;

                modulelList.Add(module);
            }

            return modulelList.ToArray();
        }

        private static bool IsAssembleIncludingType(Assembly assembly)
        {
            try
            {
                bool isAssignableFrom = assembly.GetTypes().Any(t => typeof(EventHandlerImpl).IsAssignableFrom(t));

                return isAssignableFrom;
            }
            catch (Exception ex)
            {
                return false;
            }

        }
    }
}
