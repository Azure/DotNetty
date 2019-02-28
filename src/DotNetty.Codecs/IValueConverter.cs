// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    public interface IValueConverter<T>
    {
        T ConvertObject(object value);

        T ConvertBoolean(bool value);

        bool ConvertToBoolean(T value);

        T ConvertByte(byte value);

        byte ConvertToByte(T value);

        T ConvertChar(char value);

        char ConvertToChar(T value);

        T ConvertShort(short value);

        short ConvertToShort(T value);

        T ConvertInt(int value);

        int ConvertToInt(T value);

        T ConvertLong(long value);

        long ConvertToLong(T value);

        T ConvertTimeMillis(long value);

        long ConvertToTimeMillis(T value);

        T ConvertFloat(float value);

        float ConvertToFloat(T value);

        T ConvertDouble(double value);

        double ConvertToDouble(T value);
    }
}
