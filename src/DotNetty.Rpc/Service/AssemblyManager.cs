namespace DotNetty.Rpc.Service
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;

    internal class AssemblyManager 
    {
        protected virtual AppDomain App => AppDomain.CurrentDomain;

        private string AssemblySkipLoadingPattern { get; set; } = "^System|^mscorlib|^Microsoft|^AjaxControlToolkit|^Antlr3|^Autofac|^AutoMapper|^Castle|^ComponentArt|^CppCodeProvider|^DotNetOpenAuth|^EntityFramework|^EPPlus|^FluentValidation|^ImageResizer|^itextsharp|^log4net|^MaxMind|^MbUnit|^MiniProfiler|^Mono.Math|^MvcContrib|^Newtonsoft|^NHibernate|^nunit|^Org.Mentalis|^PerlRegex|^QuickGraph|^Recaptcha|^Remotion|^RestSharp|^Rhino|^Telerik|^Iesi|^TestDriven|^TestFu|^UserAgentStringLibrary|^VJSharpCodeProvider|^WebActivator|^WebDev|^WebGrease";

        private string AssemblyRestrictToLoadingPattern { get; set; } = ".*";

        private bool Loaded { get; set; }

        private static readonly object Locker = new object();

        public IEnumerable<Assembly> GetAssemblies()
        {
            if (this.Loaded)
                return this.App.GetAssemblies();
            lock (Locker)
            {
                this.LoadMatchingAssemblies();
                this.Loaded = true;
                return this.App.GetAssemblies();
            }

        }

        protected virtual bool Matches(string assemblyFullName)
        {
            return !this.Matches(assemblyFullName, this.AssemblySkipLoadingPattern)
                   && this.Matches(assemblyFullName, this.AssemblyRestrictToLoadingPattern);
        }

        protected virtual bool Matches(string assemblyFullName, string pattern)
        {
            return Regex.IsMatch(assemblyFullName, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        protected virtual void LoadMatchingAssemblies()
        {
            string directoryPath = this.App.BaseDirectory;

            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            string binDirectory = Path.Combine(directoryPath, "bin");
            if (Directory.Exists(binDirectory))
            {
                directoryPath = binDirectory;
            }
            
            List<string> loadedAssemblyNames = this.App.GetAssemblies().Select(a => a.FullName).ToList();

            foreach (string dllPath in Directory.GetFiles(directoryPath, "*.dll"))
            {
                try
                {
                    AssemblyName an = AssemblyName.GetAssemblyName(dllPath);
                    if (this.Matches(an.FullName) && !loadedAssemblyNames.Contains(an.FullName))
                    {
                        this.App.Load(an);
                    }
                }
                catch (BadImageFormatException ex)
                {
                    Trace.TraceError(ex.ToString());
                }
            }
        }

    }
}
