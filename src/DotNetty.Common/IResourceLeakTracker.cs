// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    public interface IResourceLeakTracker
    {
        /// <summary>
        ///     Records the caller's current stack trace so that the <see cref="ResourceLeakDetector" /> can tell where the
        ///     leaked
        ///     resource was accessed lastly. This method is a shortcut to <see cref="Record(object)" /> with <c>null</c> as an
        ///     argument.
        /// </summary>
        void Record();

        /// <summary>
        ///     Records the caller's current stack trace and the specified additional arbitrary information
        ///     so that the <see cref="ResourceLeakDetector" /> can tell where the leaked resource was accessed lastly.
        /// </summary>
        /// <param name="hint"></param>
        void Record(object hint);

        /// <summary>
        ///     Close the leak so that <see cref="ResourceLeakDetector" /> does not warn about leaked resources.
        /// </summary>
        /// <returns><c>true</c> if called first time, <c>false</c> if called already</returns>
        bool Close(object trackedObject);
    }
}