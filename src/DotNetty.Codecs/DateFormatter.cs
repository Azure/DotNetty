// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public sealed class DateFormatter
    {
        static readonly BitArray Delimiters = GetDelimiters();

        static BitArray GetDelimiters()
        {
            var bitArray = new BitArray(128, false);
            bitArray[0x09] = true;
            for (int c = 0x20; c <= 0x2F; c++)
            {
                bitArray[c] = true;
            }

            for (int c = 0x3B; c <= 0x40; c++)
            {
                bitArray[c] = true;
            }

            for (int c = 0x5B; c <= 0x60; c++)
            {
                bitArray[c] = true;
            }

            for (int c = 0x7B; c <= 0x7E; c++)
            {
                bitArray[c] = true;
            }

            return bitArray;
        }

        // The order is the same as dateTime.DayOfWeek
        static readonly string[] DayOfWeekToShortName =
            { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        static readonly string[] CalendarMonthToShortName =
            { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        static readonly ThreadLocalCache Cache = new ThreadLocalCache();

        public static DateTime? ParseHttpDate(string txt) => ParseHttpDate(AsciiString.Cached(txt));

        public static DateTime? ParseHttpDate(ICharSequence txt) => ParseHttpDate(txt, 0, txt.Count);

        public static DateTime? ParseHttpDate(string txt, int start, int end) => ParseHttpDate(AsciiString.Cached(txt), start, end);

        public static DateTime? ParseHttpDate(ICharSequence txt, int start, int end)
        {
            Contract.Requires(txt != null);

            int length = end - start;
            if (length == 0)
            {
                return null;
            }
            else if (length < 0)
            {
                throw new ArgumentException("Can't have end < start");
            }
            else if (length > 64)
            {
                throw new ArgumentException("Can't parse more than 64 chars, looks like a user error or a malformed header");
            }

            return Formatter().Parse0(txt, start, end);
        }

        public static string Format(DateTime dateTime) => Formatter().Format0(dateTime);

        public static StringBuilder Append(DateTime dateTime, StringBuilder sb) => Append0(dateTime, sb);

        static DateFormatter Formatter()
        {
            DateFormatter formatter = Cache.Value;
            formatter.Reset();
            return formatter;
        }

        // delimiter = %x09 / %x20-2F / %x3B-40 / %x5B-60 / %x7B-7E
        static bool IsDelim(char c) => Delimiters[c];

        static bool IsDigit(char c) => c >= 48 && c <= 57;

        static int GetNumericalValue(char c) => c - 48;

        readonly StringBuilder sb = new StringBuilder(29); // Sun, 27 Nov 2016 19:37:15 GMT
        bool timeFound;
        int hours;
        int minutes;
        int seconds;
        bool dayOfMonthFound;
        int dayOfMonth;
        bool monthFound;
        int month;
        bool yearFound;
        int year;

        DateFormatter()
        {
            this.Reset();
        }

        public void Reset()
        {
            this.timeFound = false;
            this.hours = -1;
            this.minutes = -1;
            this.seconds = -1;
            this.dayOfMonthFound = false;
            this.dayOfMonth = -1;
            this.monthFound = false;
            this.month = -1;
            this.yearFound = false;
            this.year = -1;
            this.sb.Length = 0;
        }

        bool TryParseTime(ICharSequence txt, int tokenStart, int tokenEnd)
        {
            int len = tokenEnd - tokenStart;

            // h:m:s to hh:mm:ss
            if (len < 5 || len > 8)
            {
                return false;
            }

            int localHours = -1;
            int localMinutes = -1;
            int localSeconds = -1;
            int currentPartNumber = 0;
            int currentPartValue = 0;
            int numDigits = 0;

            for (int i = tokenStart; i < tokenEnd; i++)
            {
                char c = txt[i];
                if (IsDigit(c))
                {
                    currentPartValue = currentPartValue * 10 + GetNumericalValue(c);
                    if (++numDigits > 2)
                    {
                        return false; // too many digits in this part
                    }
                }
                else if (c == ':')
                {
                    if (numDigits == 0)
                    {
                        // no digits between separators
                        return false;
                    }
                    switch (currentPartNumber)
                    {
                        case 0:
                            // flushing hours
                            localHours = currentPartValue;
                            break;
                        case 1:
                            // flushing minutes
                            localMinutes = currentPartValue;
                            break;
                        default:
                            // invalid, too many :
                            return false;
                    }
                    currentPartValue = 0;
                    currentPartNumber++;
                    numDigits = 0;
                }
                else
                {
                    // invalid char
                    return false;
                }
            }

            if (numDigits > 0)
            {
                // pending seconds
                localSeconds = currentPartValue;
            }

            if (localHours >= 0 && localMinutes >= 0 && localSeconds >= 0)
            {
                this.hours = localHours;
                this.minutes = localMinutes;
                this.seconds = localSeconds;
                return true;
            }

            return false;
        }

        bool TryParseDayOfMonth(ICharSequence txt, int tokenStart, int tokenEnd)
        {
            int len = tokenEnd - tokenStart;

            if (len == 1)
            {
                char c0 = txt[tokenStart];
                if (IsDigit(c0))
                {
                    this.dayOfMonth = GetNumericalValue(c0);
                    return true;
                }

            }
            else if (len == 2)
            {
                char c0 = txt[tokenStart];
                char c1 = txt[tokenStart + 1];
                if (IsDigit(c0) && IsDigit(c1))
                {
                    this.dayOfMonth = GetNumericalValue(c0) * 10 + GetNumericalValue(c1);
                    return true;
                }
            }

            return false;
        }

        static bool MatchMonth(ICharSequence month, ICharSequence txt, int tokenStart) =>
            AsciiString.RegionMatchesAscii(month, true, 0, txt, tokenStart, 3);

        bool TryParseMonth(ICharSequence txt, int tokenStart, int tokenEnd)
        {
            int len = tokenEnd - tokenStart;

            if (len != 3)
            {
                return false;
            }

            if (MatchMonth(Jan, txt, tokenStart))
            {
                this.month = 1;
            }
            else if (MatchMonth(Feb, txt, tokenStart))
            {
                this.month = 2;
            }
            else if (MatchMonth(Mar, txt, tokenStart))
            {
                this.month = 3;
            }
            else if (MatchMonth(Apr, txt, tokenStart))
            {
                this.month = 4;
            }
            else if (MatchMonth(May, txt, tokenStart))
            {
                this.month = 5;
            }
            else if (MatchMonth(Jun, txt, tokenStart))
            {
                this.month = 6;
            }
            else if (MatchMonth(Jul, txt, tokenStart))
            {
                this.month = 7;
            }
            else if (MatchMonth(Aug, txt, tokenStart))
            {
                this.month = 8;
            }
            else if (MatchMonth(Sep, txt, tokenStart))
            {
                this.month = 9;
            }
            else if (MatchMonth(Oct, txt, tokenStart))
            {
                this.month = 10;
            }
            else if (MatchMonth(Nov, txt, tokenStart))
            {
                this.month = 11;
            }
            else if (MatchMonth(Dec, txt, tokenStart))
            {
                this.month = 12;
            }
            else
            {
                return false;
            }

            return true;
        }

        static readonly AsciiString Jan = AsciiString.Cached("Jan");
        static readonly AsciiString Feb = AsciiString.Cached("Feb");
        static readonly AsciiString Mar = AsciiString.Cached("Mar");
        static readonly AsciiString Apr = AsciiString.Cached("Apr");
        static readonly AsciiString May = AsciiString.Cached("May");
        static readonly AsciiString Jun = AsciiString.Cached("Jun");
        static readonly AsciiString Jul = AsciiString.Cached("Jul");
        static readonly AsciiString Aug = AsciiString.Cached("Aug");
        static readonly AsciiString Sep = AsciiString.Cached("Sep");
        static readonly AsciiString Oct = AsciiString.Cached("Oct");
        static readonly AsciiString Nov = AsciiString.Cached("Nov");
        static readonly AsciiString Dec = AsciiString.Cached("Dec");

        bool TryParseYear(ICharSequence txt, int tokenStart, int tokenEnd)
        {
            int len = tokenEnd - tokenStart;

            if (len == 2)
            {
                char c0 = txt[tokenStart];
                char c1 = txt[tokenStart + 1];
                if (IsDigit(c0) && IsDigit(c1))
                {
                    this.year = GetNumericalValue(c0) * 10 + GetNumericalValue(c1);
                    return true;
                }

            }
            else if (len == 4)
            {
                char c0 = txt[tokenStart];
                char c1 = txt[tokenStart + 1];
                char c2 = txt[tokenStart + 2];
                char c3 = txt[tokenStart + 3];
                if (IsDigit(c0) && IsDigit(c1) && IsDigit(c2) && IsDigit(c3))
                {
                    this.year = GetNumericalValue(c0) * 1000
                        + GetNumericalValue(c1) * 100
                        + GetNumericalValue(c2) * 10
                        + GetNumericalValue(c3);

                    return true;
                }
            }

            return false;
        }

        bool ParseToken(ICharSequence txt, int tokenStart, int tokenEnd)
        {
            // return true if all parts are found
            if (!this.timeFound)
            {
                this.timeFound = this.TryParseTime(txt, tokenStart, tokenEnd);
                if (this.timeFound)
                {
                    return this.dayOfMonthFound && this.monthFound && this.yearFound;
                }
            }

            if (!this.dayOfMonthFound)
            {
                this.dayOfMonthFound = this.TryParseDayOfMonth(txt, tokenStart, tokenEnd);
                if (this.dayOfMonthFound)
                {
                    return this.timeFound && this.monthFound && this.yearFound;
                }
            }

            if (!this.monthFound)
            {
                this.monthFound = this.TryParseMonth(txt, tokenStart, tokenEnd);
                if (this.monthFound)
                {
                    return this.timeFound && this.dayOfMonthFound && this.yearFound;
                }
            }

            if (!this.yearFound)
            {
                this.yearFound = this.TryParseYear(txt, tokenStart, tokenEnd);
            }

            return this.timeFound && this.dayOfMonthFound && this.monthFound && this.yearFound;
        }

        DateTime? Parse0(ICharSequence txt, int start, int end)
        {
            bool allPartsFound = this.Parse1(txt, start, end);
            return allPartsFound && this.NormalizeAndValidate() ? this.ComputeDate() : default(DateTime?);
        }

        bool Parse1(ICharSequence txt, int start, int end)
        {
            // return true if all parts are found
            int tokenStart = -1;

            for (int i = start; i < end; i++)
            {
                char c = txt[i];

                if (IsDelim(c))
                {
                    if (tokenStart != -1)
                    {
                        // terminate token
                        if (this.ParseToken(txt, tokenStart, i))
                        {
                            return true;
                        }
                        tokenStart = -1;
                    }
                }
                else if (tokenStart == -1)
                {
                    // start new token
                    tokenStart = i;
                }
            }

            // terminate trailing token
            return tokenStart != -1 && this.ParseToken(txt, tokenStart, txt.Count);
        }

        bool NormalizeAndValidate()
        {
            if (this.dayOfMonth < 1
                || this.dayOfMonth > 31
                || this.hours > 23
                || this.minutes > 59
                || this.seconds > 59)
            {
                return false;
            }

            if (this.year >= 70 && this.year <= 99)
            {
                this.year += 1900;
            }
            else if (this.year >= 0 && this.year < 70)
            {
                this.year += 2000;
            }
            else if (this.year < 1601)
            {
                // invalid value
                return false;
            }
            return true;
        }

        DateTime ComputeDate() => new DateTime(this.year, this.month, this.dayOfMonth, this.hours, this.minutes, this.seconds, DateTimeKind.Utc);

        string Format0(DateTime dateTime) => Append0(dateTime, this.sb).ToString();

        static StringBuilder Append0(DateTime dateTime, StringBuilder buffer)
        {
            buffer.Append(DayOfWeekToShortName[(int)dateTime.DayOfWeek]).Append(", ");
            buffer.Append(dateTime.Day).Append(' ');
            buffer.Append(CalendarMonthToShortName[dateTime.Month - 1]).Append(' ');
            buffer.Append(dateTime.Year).Append(' ');

            AppendZeroLeftPadded(dateTime.Hour, buffer).Append(':');
            AppendZeroLeftPadded(dateTime.Minute, buffer).Append(':');
            return AppendZeroLeftPadded(dateTime.Second, buffer).Append(" GMT");
        }

        static StringBuilder AppendZeroLeftPadded(int value, StringBuilder sb)
        {
            if (value < 10)
            {
                sb.Append('0');
            }
            return sb.Append(value);
        }

        sealed class ThreadLocalCache : FastThreadLocal<DateFormatter>
        {
            protected override DateFormatter GetInitialValue() => new DateFormatter();
        }
    }
}
