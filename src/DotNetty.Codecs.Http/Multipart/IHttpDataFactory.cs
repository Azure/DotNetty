// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System.Text;

    /// <summary>
    /// Interface to enable creation of IPostHttpData objects
    /// </summary>
    public interface IHttpDataFactory
    {
        void SetMaxLimit(long max);

        IAttribute CreateAttribute(IHttpRequest request, string name);

        IAttribute CreateAttribute(IHttpRequest request, string name, long definedSize);

        IAttribute CreateAttribute(IHttpRequest request, string name, string value);

        IFileUpload CreateFileUpload(IHttpRequest request, string name, string filename, 
            string contentType, string contentTransferEncoding, Encoding charset, long size);

        void RemoveHttpDataFromClean(IHttpRequest request, IInterfaceHttpData data);

        void CleanRequestHttpData(IHttpRequest request);

        void CleanAllHttpData();
    }
}
