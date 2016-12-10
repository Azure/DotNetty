using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DotNetty.Platform
{
    /// <summary>
    /// An exception thrown from the DotNetty.Platform reference assembly when it is called
    /// instead of a platform-specific assembly at runtime.
    /// </summary>
    internal class NotImplementedByReferenceAssemblyException : NotImplementedException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotImplementedByReferenceAssemblyException"/> class.
        /// </summary>
        internal NotImplementedByReferenceAssemblyException()
            : base("This is a reference assembly and does not contain implementation. Be sure to install the DotNetty.Platform package into your application so the platform implementation assembly will be used at runtime.")
        {
        }
    }
}
