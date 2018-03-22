// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace DotNetty.Transport.Libuv.Native
{
    using System;

    public sealed class OperationException : Exception
    {
        public OperationException(int errorCode, string errorName, string description) 
        {
            this.Code = errorCode;
            this.Name = errorName;
            this.Description = description;
                
            if (!Enum.TryParse(errorName, out ErrorCode value))
            {
                value = ErrorCode.UNKNOWN;
            }

            this.ErrorCode = value;
        }

        public int Code { get; }

        public string Name { get; }

        public string Description { get; }

        public ErrorCode ErrorCode { get; }

        public override string Message => $"{this.Name} ({this.ErrorCode}) : {this.Description}";
    }

    enum uv_err_code
    {
        UV_OK = 0,
        UV_E2BIG,
        UV_EACCES,
        UV_EADDRINUSE,
        UV_EADDRNOTAVAIL,
        UV_EAFNOSUPPORT,
        UV_EAGAIN,
        UV_EAI_ADDRFAMILY,
        UV_EAI_AGAIN,
        UV_EAI_BADFLAGS,
        UV_EAI_BADHINTS,
        UV_EAI_CANCELED,
        UV_EAI_FAIL,
        UV_EAI_FAMILY,
        UV_EAI_MEMORY,
        UV_EAI_NODATA,
        UV_EAI_NONAME,
        UV_EAI_OVERFLOW,
        UV_EAI_PROTOCOL,
        UV_EAI_SERVICE,
        UV_EAI_SOCKTYPE,
        UV_EALREADY,
        UV_EBADF,
        UV_EBUSY,
        UV_ECANCELED,
        UV_ECHARSET,
        UV_ECONNABORTED,
        UV_ECONNREFUSED,
        UV_ECONNRESET,
        UV_EDESTADDRREQ,
        UV_EEXIST,
        UV_EFAULT,
        UV_EFBIG,
        UV_EHOSTUNREACH,
        UV_EINTR,
        UV_EINVAL,
        UV_EIO,
        UV_EISCONN,
        UV_EISDIR,
        UV_ELOOP,
        UV_EMFILE,
        UV_EMSGSIZE,
        UV_ENAMETOOLONG,
        UV_ENETDOWN,
        UV_ENETUNREACH,
        UV_ENFILE,
        UV_ENOBUFS,
        UV_ENODEV,
        UV_ENOENT,
        UV_ENOMEM,
        UV_ENONET,
        UV_ENOPROTOOPT,
        UV_ENOSPC,
        UV_ENOSYS,
        UV_ENOTCONN,
        UV_ENOTDIR,
        UV_ENOTEMPTY,
        UV_ENOTSOCK,
        UV_ENOTSUP,
        UV_EPERM,
        UV_EPIPE,
        UV_EPROTO,
        UV_EPROTONOSUPPORT,
        UV_EPROTOTYPE,
        UV_ERANGE,
        UV_EROFS,
        UV_ESHUTDOWN,
        UV_ESPIPE,
        UV_ESRCH,
        UV_ETIMEDOUT,
        UV_ETXTBSY,
        UV_EXDEV,
        UV_UNKNOWN,
        UV_EOF = -4095,
        UV_ENXIO,
        UV_EMLINK,
    }

    public enum ErrorCode
    {
        E2BIG,
        EACCES,
        EADDRINUSE,
        EADDRNOTAVAIL,
        EAFNOSUPPORT,
        EAGAIN,
        EAI_ADDRFAMILY,
        EAI_AGAIN,
        EAI_BADFLAGS,
        EAI_BADHINTS,
        EAI_CANCELED,
        EAI_FAIL,
        EAI_FAMILY,
        EAI_MEMORY,
        EAI_NODATA,
        EAI_NONAME,
        EAI_OVERFLOW,
        EAI_PROTOCOL,
        EAI_SERVICE,
        EAI_SOCKTYPE,
        EALREADY,
        EBADF,
        EBUSY,
        ECANCELED,
        ECHARSET,
        ECONNABORTED,
        ECONNREFUSED,
        ECONNRESET,
        EDESTADDRREQ,
        EEXIST,
        EFAULT,
        EFBIG,
        EHOSTUNREACH,
        EINTR,
        EINVAL,
        EIO,
        EISCONN,
        EISDIR,
        ELOOP,
        EMFILE,
        EMSGSIZE,
        ENAMETOOLONG,
        ENETDOWN,
        ENETUNREACH,
        ENFILE,
        ENOBUFS,
        ENODEV,
        ENOENT,
        ENOMEM,
        ENONET,
        ENOPROTOOPT,
        ENOSPC,
        ENOSYS,
        ENOTCONN,
        ENOTDIR,
        ENOTEMPTY,
        ENOTSOCK,
        ENOTSUP,
        EPERM,
        EPIPE,
        EPROTO,
        EPROTONOSUPPORT,
        EPROTOTYPE,
        ERANGE,
        EROFS,
        ESHUTDOWN,
        ESPIPE,
        ESRCH,
        ETIMEDOUT,
        ETXTBSY,
        EXDEV,
        UNKNOWN,
        EOF,
        ENXIO,
        EMLINK,
    }
}
