// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    enum MultiPartStatus
    {
        Notstarted,
        Preamble,
        HeaderDelimiter,
        Disposition,
        Field,
        Fileupload,
        MixedPreamble,
        MixedDelimiter,
        MixedDisposition,
        MixedFileUpload,
        MixedCloseDelimiter,
        CloseDelimiter,
        PreEpilogue,
        Epilogue
    }
}
