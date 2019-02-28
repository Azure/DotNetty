// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    public class DefaultHttpDataFactory : IHttpDataFactory
    {
        // Proposed default MINSIZE as 16 KB.
        public static readonly long MinSize = 0x4000;

        // Proposed default MAXSIZE = -1 as UNLIMITED
        public static readonly long MaxSize = -1;

        readonly bool useDisk;
        readonly bool checkSize;
        readonly long minSize;
        long maxSize = MaxSize;
        readonly Encoding charset = HttpConstants.DefaultEncoding;

        // Keep all HttpDatas until cleanAllHttpData() is called.
        readonly ConcurrentDictionary<IHttpRequest, List<IHttpData>> requestFileDeleteMap = 
            new ConcurrentDictionary<IHttpRequest, List<IHttpData>>(IdentityComparer.Default);

        // HttpData will be in memory if less than default size (16KB).
        // The type will be Mixed.
        public DefaultHttpDataFactory()
        {
            this.useDisk = false;
            this.checkSize = true;
            this.minSize = MinSize;
        }

        public DefaultHttpDataFactory(Encoding charset) : this()
        {
            this.charset = charset;
        }

        // HttpData will be always on Disk if useDisk is True, else always in Memory if False
        public DefaultHttpDataFactory(bool useDisk)
        {
            this.useDisk = useDisk;
            this.checkSize = false;
        }

        public DefaultHttpDataFactory(bool useDisk, Encoding charset) : this(useDisk)
        {
            this.charset = charset;
        }

        public DefaultHttpDataFactory(long minSize)
        {
            this.useDisk = false;
            this.checkSize = true;
            this.minSize = minSize;
        }

        public DefaultHttpDataFactory(long minSize, Encoding charset) : this(minSize)
        {
            this.charset = charset;
        }

        public void SetMaxLimit(long max) => this.maxSize = max;

        List<IHttpData> GetList(IHttpRequest request)
        {
            List<IHttpData> list = this.requestFileDeleteMap.GetOrAdd(request, _ => new List<IHttpData>());
            return list;
        }

        public IAttribute CreateAttribute(IHttpRequest request, string name)
        {
            if (this.useDisk)
            {
                var diskAttribute = new DiskAttribute(name, this.charset);
                diskAttribute.MaxSize = this.maxSize;
                List<IHttpData> list = this.GetList(request);
                list.Add(diskAttribute);
                return diskAttribute;
            }
            if (this.checkSize)
            {
                var mixedAttribute = new MixedAttribute(name, this.minSize, this.charset);
                mixedAttribute.MaxSize = this.maxSize;
                List<IHttpData> list = this.GetList(request);
                list.Add(mixedAttribute);
                return mixedAttribute;
            }
            var attribute = new MemoryAttribute(name);
            attribute.MaxSize = this.maxSize;
            return attribute;
        }

        public IAttribute CreateAttribute(IHttpRequest request, string name, long definedSize)
        {
            if (this.useDisk)
            {
                var diskAttribute = new DiskAttribute(name, definedSize, this.charset);
                diskAttribute.MaxSize = this.maxSize;
                List<IHttpData> list = this.GetList(request);
                list.Add(diskAttribute);
                return diskAttribute;
            }
            if (this.checkSize)
            {
                var mixedAttribute = new MixedAttribute(name, definedSize, this.minSize, this.charset);
                mixedAttribute.MaxSize = this.maxSize;
                List<IHttpData> list = this.GetList(request);
                list.Add(mixedAttribute);
                return mixedAttribute;
            }
            var attribute = new MemoryAttribute(name, definedSize);
            attribute.MaxSize = this.maxSize;
            return attribute;
        }

        static void CheckHttpDataSize(IHttpData data)
        {
            try
            {
                data.CheckSize(data.Length);
            }
            catch (IOException)
            {
                throw new ArgumentException("Attribute bigger than maxSize allowed");
            }
        }

        public IAttribute CreateAttribute(IHttpRequest request, string name, string value)
        {
            if (this.useDisk)
            {
                IAttribute attribute;
                try
                {
                    attribute = new DiskAttribute(name, value, this.charset);
                    attribute.MaxSize = this.maxSize;
                }
                catch (IOException)
                {
                    // revert to Mixed mode
                    attribute = new MixedAttribute(name, value, this.minSize, this.charset);
                    attribute.MaxSize = this.maxSize;
                }
                CheckHttpDataSize(attribute);
                List<IHttpData> list = this.GetList(request);
                list.Add(attribute);
                return attribute;
            }
            if (this.checkSize)
            {
                var mixedAttribute = new MixedAttribute(name, value, this.minSize, this.charset);
                mixedAttribute.MaxSize = this.maxSize;
                CheckHttpDataSize(mixedAttribute);
                List<IHttpData> list = this.GetList(request);
                list.Add(mixedAttribute);
                return mixedAttribute;
            }
            try
            {
                var attribute = new MemoryAttribute(name, value, this.charset);
                attribute.MaxSize = this.maxSize;
                CheckHttpDataSize(attribute);
                return attribute;
            }
            catch (IOException e)
            {
                throw new ArgumentException($"({request}, {name}, {value})", e);
            }
        }

        public IFileUpload CreateFileUpload(IHttpRequest request, string name, string fileName, 
            string contentType, string contentTransferEncoding, Encoding encoding, 
            long size)
        {
            if (this.useDisk)
            {
                var fileUpload = new DiskFileUpload(name, fileName, contentType, 
                    contentTransferEncoding, encoding, size);
                fileUpload.MaxSize = this.maxSize;
                CheckHttpDataSize(fileUpload);
                List<IHttpData> list = this.GetList(request);
                list.Add(fileUpload);
                return fileUpload;
            }
            if (this.checkSize)
            {
                var fileUpload = new MixedFileUpload(name, fileName, contentType, 
                    contentTransferEncoding, encoding, size, this.minSize);
                fileUpload.MaxSize = this.maxSize;
                CheckHttpDataSize(fileUpload);
                List<IHttpData> list = this.GetList(request);
                list.Add(fileUpload);
                return fileUpload;
            }
            var memoryFileUpload = new MemoryFileUpload(name, fileName, contentType, 
                contentTransferEncoding, encoding, size);
            memoryFileUpload.MaxSize = this.maxSize;
            CheckHttpDataSize(memoryFileUpload);
            return memoryFileUpload;
        }

        public void RemoveHttpDataFromClean(IHttpRequest request, IInterfaceHttpData data)
        {
            if (!(data is IHttpData httpData))
            {
                return;
            }

            // Do not use getList because it adds empty list to requestFileDeleteMap
            // if request is not found
            if (!this.requestFileDeleteMap.TryGetValue(request, out List<IHttpData> list))
            {
                return;
            }

            // Can't simply call list.remove(data), because different data items may be equal.
            // Need to check identity.
            int index = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], httpData))
                {
                    index = i;
                    break;
                }
            }
            if (index != -1)
            {
                list.RemoveAt(index);
            }
            if (list.Count == 0)
            {
                this.requestFileDeleteMap.TryRemove(request, out _);
            }
        }

        public void CleanRequestHttpData(IHttpRequest request)
        {
            if (this.requestFileDeleteMap.TryRemove(request, out List<IHttpData> list))
            {
                foreach (IHttpData data in list)
                {
                    data.Release();
                }
            }
        }

        public void CleanAllHttpData()
        {
            while (!this.requestFileDeleteMap.IsEmpty)
            {
                IHttpRequest[] keys = this.requestFileDeleteMap.Keys.ToArray();
                foreach (IHttpRequest key in keys)
                {
                    if (this.requestFileDeleteMap.TryRemove(key, out List<IHttpData> list))
                    {
                        foreach (IHttpData data in list)
                        {
                            data.Release();
                        }
                    }
                }
            }
        }

        // Similar to IdentityHashMap in Java
        sealed class IdentityComparer : IEqualityComparer<IHttpRequest>
        {
            internal static readonly IdentityComparer Default = new IdentityComparer();

            public bool Equals(IHttpRequest x, IHttpRequest y) => ReferenceEquals(x, y);

            public int GetHashCode(IHttpRequest obj) => obj.GetHashCode();
        }
    }
}
