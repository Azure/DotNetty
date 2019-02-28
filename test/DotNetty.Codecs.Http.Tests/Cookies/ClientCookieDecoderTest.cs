// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Cookies
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs.Http.Cookies;
    using Xunit;

    public sealed class ClientCookieDecoderTest
    {
        [Fact]
        public void DecodingSingleCookieV0()
        {
            string cookieString = "myCookie=myValue;expires="
                    + DateFormatter.Format(DateTime.UtcNow.AddMilliseconds(50000))
                    + ";path=/apathsomewhere;domain=.adomainsomewhere;secure;";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(cookieString);
            Assert.NotNull(cookie);

            Assert.Equal("myValue", cookie.Value);
            Assert.Equal(".adomainsomewhere", cookie.Domain);
            Assert.True(cookie.MaxAge >= 40 && cookie.MaxAge <= 60, "maxAge should be about 50ms when parsing cookie " + cookieString);
            Assert.Equal("/apathsomewhere", cookie.Path);
            Assert.True(cookie.IsSecure);
        }

        [Fact]
        public void DecodingSingleCookieV0ExtraParamsIgnored()
        {
            const string CookieString = "myCookie=myValue;max-age=50;path=/apathsomewhere;" +
                "domain=.adomainsomewhere;secure;comment=this is a comment;version=0;" +
                "commentURL=http://aurl.com;port=\"80,8080\";discard;";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(CookieString);
            Assert.NotNull(cookie);

            Assert.Equal("myValue", cookie.Value);
            Assert.Equal(".adomainsomewhere", cookie.Domain);
            Assert.Equal(50, cookie.MaxAge);
            Assert.Equal("/apathsomewhere", cookie.Path);
            Assert.True(cookie.IsSecure);
        }

        [Fact]
        public void DecodingSingleCookieV1()
        {
            const string CookieString = "myCookie=myValue;max-age=50;path=/apathsomewhere;domain=.adomainsomewhere"
                + ";secure;comment=this is a comment;version=1;";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(CookieString);
            Assert.NotNull(cookie);

            Assert.Equal("myValue", cookie.Value);
            Assert.Equal(".adomainsomewhere", cookie.Domain);
            Assert.Equal(50, cookie.MaxAge);
            Assert.Equal("/apathsomewhere", cookie.Path);
            Assert.True(cookie.IsSecure);
        }

        [Fact]
        public void DecodingSingleCookieV1ExtraParamsIgnored()
        {
            const string CookieString = "myCookie=myValue;max-age=50;path=/apathsomewhere;"
                + "domain=.adomainsomewhere;secure;comment=this is a comment;version=1;"
                + "commentURL=http://aurl.com;port='80,8080';discard;";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(CookieString);
            Assert.NotNull(cookie);

            Assert.Equal("myValue", cookie.Value);
            Assert.Equal(".adomainsomewhere", cookie.Domain);
            Assert.Equal(50, cookie.MaxAge);
            Assert.Equal("/apathsomewhere", cookie.Path);
            Assert.True(cookie.IsSecure);
        }

        [Fact]
        public void DecodingSingleCookieV2()
        {
            const string CookieString = "myCookie=myValue;max-age=50;path=/apathsomewhere;"
                    + "domain=.adomainsomewhere;secure;comment=this is a comment;version=2;"
                    + "commentURL=http://aurl.com;port=\"80,8080\";discard;";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(CookieString);
            Assert.NotNull(cookie);

            Assert.Equal("myValue", cookie.Value);
            Assert.Equal(".adomainsomewhere", cookie.Domain);
            Assert.Equal(50, cookie.MaxAge);
            Assert.Equal("/apathsomewhere", cookie.Path);
            Assert.True(cookie.IsSecure);
        }

        [Fact]
        public void DecodingComplexCookie()
        {
            const string CookieString = "myCookie=myValue;max-age=50;path=/apathsomewhere;"
                    + "domain=.adomainsomewhere;secure;comment=this is a comment;version=2;"
                    + "commentURL=\"http://aurl.com\";port='80,8080';discard;";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(CookieString);
            Assert.NotNull(cookie);

            Assert.Equal("myValue", cookie.Value);
            Assert.Equal(".adomainsomewhere", cookie.Domain);
            Assert.Equal(50, cookie.MaxAge);
            Assert.Equal("/apathsomewhere", cookie.Path);
            Assert.True(cookie.IsSecure);
        }

        [Fact]
        public void DecodingQuotedCookie()
        {
            var sources = new List<string>
            {
                "a=\"\",",
                "b=\"1\","
            };

            var cookies = new List<ICookie>();
            foreach (string source in sources)
            {
                cookies.Add(ClientCookieDecoder.StrictDecoder.Decode(source));
            }
            Assert.Equal(2, cookies.Count);

            ICookie c = cookies[0];
            Assert.Equal("a", c.Name);
            Assert.Equal("", c.Value);

            c = cookies[1];
            Assert.Equal("b", c.Name);
            Assert.Equal("1", c.Value);
        }

        [Fact]
        public void DecodingGoogleAnalyticsCookie()
        {
            const string Source = "ARPT=LWUKQPSWRTUN04CKKJI; "
                + "kw-2E343B92-B097-442c-BFA5-BE371E0325A2=unfinished furniture; "
                + "__utma=48461872.1094088325.1258140131.1258140131.1258140131.1; "
                + "__utmb=48461872.13.10.1258140131; __utmc=48461872; "
                + "__utmz=48461872.1258140131.1.1.utmcsr=overstock.com|utmccn=(referral)|"
                + "utmcmd=referral|utmcct=/Home-Garden/Furniture/Clearance,/clearance,/32/dept.html";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(Source);
            Assert.NotNull(cookie);

            Assert.Equal("ARPT", cookie.Name);
            Assert.Equal("LWUKQPSWRTUN04CKKJI", cookie.Value);
        }

        [Fact]
        public void DecodingLongDates()
        {
            var cookieDate = new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            long expectedMaxAge = (cookieDate.Ticks - DateTime.UtcNow.Ticks) / TimeSpan.TicksPerSecond;

            const string Source = "Format=EU; expires=Fri, 31-Dec-9999 23:59:59 GMT; path=/";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(Source);
            Assert.NotNull(cookie);

            Assert.True(Math.Abs(expectedMaxAge - cookie.MaxAge) < 2);
        }

        [Fact]
        public void DecodingValueWithCommaFails()
        {
            const string Source = "UserCookie=timeZoneName=(GMT+04:00) Moscow, St. Petersburg, Volgograd&promocode=&region=BE;"
                + " expires=Sat, 01-Dec-2012 10:53:31 GMT; path=/";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(Source);
            Assert.Null(cookie);
        }

        [Fact]
        public void DecodingWeirdNames1()
        {
            const string Source = "path=; expires=Mon, 01-Jan-1990 00:00:00 GMT; path=/; domain=.www.google.com";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(Source);
            Assert.NotNull(cookie);

            Assert.Equal("path", cookie.Name);
            Assert.Equal("", cookie.Value);
            Assert.Equal("/", cookie.Path);
        }

        [Fact]
        public void DecodingWeirdNames2()
        {
            const string Source = "HTTPOnly=";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(Source);
            Assert.NotNull(cookie);

            Assert.Equal("HTTPOnly", cookie.Name);
            Assert.Equal("", cookie.Value);
        }

        [Fact]
        public void DecodingValuesWithCommasAndEqualsFails()
        {
            const string Source = "A=v=1&lg=en-US,it-IT,it&intl=it&np=1;T=z=E";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(Source);
            Assert.Null(cookie);
        }

        [Fact]
        public void DecodingLongValue()
        {
            const string LongValue =
                    "b___$Q__$ha__<NC=MN(F__%#4__<NC=MN(F__2_d____#=IvZB__2_F____'=KqtH__2-9____" +
                    "'=IvZM__3f:____$=HbQW__3g'____%=J^wI__3g-____%=J^wI__3g1____$=HbQW__3g2____" +
                    "$=HbQW__3g5____%=J^wI__3g9____$=HbQW__3gT____$=HbQW__3gX____#=J^wI__3gY____" +
                    "#=J^wI__3gh____$=HbQW__3gj____$=HbQW__3gr____$=HbQW__3gx____#=J^wI__3h_____" +
                    "$=HbQW__3h$____#=J^wI__3h'____$=HbQW__3h_____$=HbQW__3h0____%=J^wI__3h1____" +
                    "#=J^wI__3h2____$=HbQW__3h4____$=HbQW__3h7____$=HbQW__3h8____%=J^wI__3h:____" +
                    "#=J^wI__3h@____%=J^wI__3hB____$=HbQW__3hC____$=HbQW__3hL____$=HbQW__3hQ____" +
                    "$=HbQW__3hS____%=J^wI__3hU____$=HbQW__3h[____$=HbQW__3h^____$=HbQW__3hd____" +
                    "%=J^wI__3he____%=J^wI__3hf____%=J^wI__3hg____$=HbQW__3hh____%=J^wI__3hi____" +
                    "%=J^wI__3hv____$=HbQW__3i/____#=J^wI__3i2____#=J^wI__3i3____%=J^wI__3i4____" +
                    "$=HbQW__3i7____$=HbQW__3i8____$=HbQW__3i9____%=J^wI__3i=____#=J^wI__3i>____" +
                    "%=J^wI__3iD____$=HbQW__3iF____#=J^wI__3iH____%=J^wI__3iM____%=J^wI__3iS____" +
                    "#=J^wI__3iU____%=J^wI__3iZ____#=J^wI__3i]____%=J^wI__3ig____%=J^wI__3ij____" +
                    "%=J^wI__3ik____#=J^wI__3il____$=HbQW__3in____%=J^wI__3ip____$=HbQW__3iq____" +
                    "$=HbQW__3it____%=J^wI__3ix____#=J^wI__3j_____$=HbQW__3j%____$=HbQW__3j'____" +
                    "%=J^wI__3j(____%=J^wI__9mJ____'=KqtH__=SE__<NC=MN(F__?VS__<NC=MN(F__Zw`____" +
                    "%=KqtH__j+C__<NC=MN(F__j+M__<NC=MN(F__j+a__<NC=MN(F__j_.__<NC=MN(F__n>M____" +
                    "'=KqtH__s1X____$=MMyc__s1_____#=MN#O__ypn____'=KqtH__ypr____'=KqtH_#%h_____" +
                    "%=KqtH_#%o_____'=KqtH_#)H6__<NC=MN(F_#*%'____%=KqtH_#+k(____'=KqtH_#-E_____" +
                    "'=KqtH_#1)w____'=KqtH_#1)y____'=KqtH_#1*M____#=KqtH_#1*p____'=KqtH_#14Q__<N" +
                    "C=MN(F_#14S__<NC=MN(F_#16I__<NC=MN(F_#16N__<NC=MN(F_#16X__<NC=MN(F_#16k__<N" +
                    "C=MN(F_#17@__<NC=MN(F_#17A__<NC=MN(F_#1Cq____'=KqtH_#7)_____#=KqtH_#7)b____" +
                    "#=KqtH_#7Ww____'=KqtH_#?cQ____'=KqtH_#His____'=KqtH_#Jrh____'=KqtH_#O@M__<N" +
                    "C=MN(F_#O@O__<NC=MN(F_#OC6__<NC=MN(F_#Os.____#=KqtH_#YOW____#=H/Li_#Zat____" +
                    "'=KqtH_#ZbI____%=KqtH_#Zbc____'=KqtH_#Zbs____%=KqtH_#Zby____'=KqtH_#Zce____" +
                    "'=KqtH_#Zdc____%=KqtH_#Zea____'=KqtH_#ZhI____#=KqtH_#ZiD____'=KqtH_#Zis____" +
                    "'=KqtH_#Zj0____#=KqtH_#Zj1____'=KqtH_#Zj[____'=KqtH_#Zj]____'=KqtH_#Zj^____" +
                    "'=KqtH_#Zjb____'=KqtH_#Zk_____'=KqtH_#Zk6____#=KqtH_#Zk9____%=KqtH_#Zk<____" +
                    "'=KqtH_#Zl>____'=KqtH_#]9R____$=H/Lt_#]I6____#=KqtH_#]Z#____%=KqtH_#^*N____" +
                    "#=KqtH_#^:m____#=KqtH_#_*_____%=J^wI_#`-7____#=KqtH_#`T>____'=KqtH_#`T?____" +
                    "'=KqtH_#`TA____'=KqtH_#`TB____'=KqtH_#`TG____'=KqtH_#`TP____#=KqtH_#`U_____" +
                    "'=KqtH_#`U/____'=KqtH_#`U0____#=KqtH_#`U9____'=KqtH_#aEQ____%=KqtH_#b<)____" +
                    "'=KqtH_#c9-____%=KqtH_#dxC____%=KqtH_#dxE____%=KqtH_#ev$____'=KqtH_#fBi____" +
                    "#=KqtH_#fBj____'=KqtH_#fG)____'=KqtH_#fG+____'=KqtH_#g<d____'=KqtH_#g<e____" +
                    "'=KqtH_#g=J____'=KqtH_#gat____#=KqtH_#s`D____#=J_#p_#sg?____#=J_#p_#t<a____" +
                    "#=KqtH_#t<c____#=KqtH_#trY____$=JiYj_#vA$____'=KqtH_#xs_____'=KqtH_$$rO____" +
                    "#=KqtH_$$rP____#=KqtH_$(_%____'=KqtH_$)]o____%=KqtH_$_@)____'=KqtH_$_k]____" +
                    "'=KqtH_$1]+____%=KqtH_$3IO____%=KqtH_$3J#____'=KqtH_$3J.____'=KqtH_$3J:____" +
                    "#=KqtH_$3JH____#=KqtH_$3JI____#=KqtH_$3JK____%=KqtH_$3JL____'=KqtH_$3JS____" +
                    "'=KqtH_$8+M____#=KqtH_$99d____%=KqtH_$:Lw____#=LK+x_$:N@____#=KqtG_$:NC____" +
                    "#=KqtG_$:hW____'=KqtH_$:i[____'=KqtH_$:ih____'=KqtH_$:it____'=KqtH_$:kO____" +
                    "'=KqtH_$>*B____'=KqtH_$>hD____+=J^x0_$?lW____'=KqtH_$?ll____'=KqtH_$?lm____" +
                    "%=KqtH_$?mi____'=KqtH_$?mx____'=KqtH_$D7]____#=J_#p_$D@T____#=J_#p_$V<g____" +
                    "'=KqtH";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode("bh=\"" + LongValue + "\";");
            Assert.NotNull(cookie);

            Assert.Equal("bh", cookie.Name);
            Assert.Equal(LongValue, cookie.Value);
        }

        [Fact]
        public void IgnoreEmptyDomain()
        {
            const string EmptyDomain = "sessionid=OTY4ZDllNTgtYjU3OC00MWRjLTkzMWMtNGUwNzk4MTY0MTUw;Domain=;Path=/";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(EmptyDomain);
            Assert.NotNull(cookie);
            Assert.Null(cookie.Domain);
        }

        [Fact]
        public void IgnoreEmptyPath()
        {
            const string EmptyPath = "sessionid=OTY4ZDllNTgtYjU3OC00MWRjLTkzMWMtNGUwNzk4MTY0MTUw;Domain=;Path=";
            ICookie cookie = ClientCookieDecoder.StrictDecoder.Decode(EmptyPath);
            Assert.NotNull(cookie);
            Assert.Null(cookie.Path);
        }
    }
}
