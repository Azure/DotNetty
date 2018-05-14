// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System.Collections.Generic;

    public interface IHeaders<TKey, TValue> : IEnumerable<HeaderEntry<TKey, TValue>> 
        where TKey : class
    {
        bool TryGet(TKey name, out TValue value);

        TValue Get(TKey name, TValue defaultValue);

        bool TryGetAndRemove(TKey name, out TValue value);

        TValue GetAndRemove(TKey name, TValue defaultValue);

        IList<TValue> GetAll(TKey name);

        IList<TValue> GetAllAndRemove(TKey name);

        bool TryGetBoolean(TKey name, out bool value);

        bool GetBoolean(TKey name, bool defaultValue);

        bool TryGetByte(TKey name, out byte value);

        byte GetByte(TKey name, byte defaultValue);

        bool TryGetChar(TKey name, out char value);

        char GetChar(TKey name, char defaultValue);

        bool TryGetShort(TKey name, out short value);

        short GetShort(TKey name, short defaultValue);

        bool TryGetInt(TKey name, out int value);

        int GetInt(TKey name, int defaultValue);

        bool TryGetLong(TKey name, out long value);

        long GetLong(TKey name, long defaultValue);

        bool TryGetFloat(TKey name, out float value);

        float GetFloat(TKey name, float defaultValue);

        bool TryGetDouble(TKey name, out double value);

        double GetDouble(TKey name, double defaultValue);

        bool TryGetTimeMillis(TKey name, out long value);

        long GetTimeMillis(TKey name, long defaultValue);

        bool TryGetBooleanAndRemove(TKey name, out bool value);

        bool GetBooleanAndRemove(TKey name, bool defaultValue);

        bool TryGetByteAndRemove(TKey name, out byte value);

        byte GetByteAndRemove(TKey name, byte defaultValue);

        bool TryGetCharAndRemove(TKey name, out char value);

        char GetCharAndRemove(TKey name, char defaultValue);

        bool TryGetShortAndRemove(TKey name, out short value);

        short GetShortAndRemove(TKey name, short defaultValue);

        bool TryGetIntAndRemove(TKey name, out int value);

        int GetIntAndRemove(TKey name, int defaultValue);

        bool TryGetLongAndRemove(TKey name, out long value);

        long GetLongAndRemove(TKey name, long defaultValue);

        bool TryGetFloatAndRemove(TKey name, out float value);

        float GetFloatAndRemove(TKey name, float defaultValue);

        bool TryGetDoubleAndRemove(TKey name, out double value);

        double GetDoubleAndRemove(TKey name, double defaultValue);

        bool TryGetTimeMillisAndRemove(TKey name, out long value);

        long GetTimeMillisAndRemove(TKey name, long defaultValue);

        bool Contains(TKey name);

        bool Contains(TKey name, TValue value);

        bool ContainsObject(TKey name, object value);

        bool ContainsBoolean(TKey name, bool value);

        bool ContainsByte(TKey name, byte value);

        bool ContainsChar(TKey name, char value);

        bool ContainsShort(TKey name, short value);

        bool ContainsInt(TKey name, int value);

        bool ContainsLong(TKey name, long value);

        bool ContainsFloat(TKey name, float value);

        bool ContainsDouble(TKey name, double value);

        bool ContainsTimeMillis(TKey name, long value);

        int Size { get; }

        bool IsEmpty { get; }

        ISet<TKey> Names();

        IHeaders<TKey, TValue> Add(TKey name, TValue value);

        IHeaders<TKey, TValue> Add(TKey name, IEnumerable<TValue> values);

        IHeaders<TKey, TValue> AddObject(TKey name, object value);

        IHeaders<TKey, TValue> AddObject(TKey name, IEnumerable<object> values);

        IHeaders<TKey, TValue> AddBoolean(TKey name, bool value);

        IHeaders<TKey, TValue> AddByte(TKey name, byte value);

        IHeaders<TKey, TValue> AddChar(TKey name, char value);

        IHeaders<TKey, TValue> AddShort(TKey name, short value);

        IHeaders<TKey, TValue> AddInt(TKey name, int value);

        IHeaders<TKey, TValue> AddLong(TKey name, long value);

        IHeaders<TKey, TValue> AddFloat(TKey name, float value);

        IHeaders<TKey, TValue> AddDouble(TKey name, double value);

        IHeaders<TKey, TValue> AddTimeMillis(TKey name, long value);

        IHeaders<TKey, TValue> Add(IHeaders<TKey, TValue> headers);

        IHeaders<TKey, TValue> Set(TKey name, TValue value);

        IHeaders<TKey, TValue> Set(TKey name, IEnumerable<TValue> values);

        IHeaders<TKey, TValue> SetObject(TKey name, object value);

        IHeaders<TKey, TValue> SetObject(TKey name, IEnumerable<object> values);

        IHeaders<TKey, TValue> SetBoolean(TKey name, bool value);

        IHeaders<TKey, TValue> SetByte(TKey name, byte value);

        IHeaders<TKey, TValue> SetChar(TKey name, char value);

        IHeaders<TKey, TValue> SetShort(TKey name, short value);

        IHeaders<TKey, TValue> SetInt(TKey name, int value);

        IHeaders<TKey, TValue> SetLong(TKey name, long value);

        IHeaders<TKey, TValue> SetFloat(TKey name, float value);

        IHeaders<TKey, TValue> SetDouble(TKey name, double value);

        IHeaders<TKey, TValue> SetTimeMillis(TKey name, long value);

        IHeaders<TKey, TValue> Set(IHeaders<TKey, TValue> headers);

        IHeaders<TKey, TValue> SetAll(IHeaders<TKey, TValue> headers);

        bool Remove(TKey name);

        IHeaders<TKey, TValue> Clear();
    }
}
