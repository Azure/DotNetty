// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;

    public static class WebSocketExtensionUtil
    {
        const char ExtensionSeparator = ',';
        const char ParameterSeparator = ';';
        const char ParameterEqual = '=';

        static readonly Regex Parameter = new Regex("^([^=]+)(=[\\\"]?([^\\\"]+)[\\\"]?)?$", RegexOptions.Compiled);

        internal static bool IsWebsocketUpgrade(HttpHeaders headers) =>
            headers.ContainsValue(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade, true) 
                && headers.Contains(HttpHeaderNames.Upgrade, HttpHeaderValues.Websocket, true);

        public static List<WebSocketExtensionData> ExtractExtensions(string extensionHeader)
        {
            string[] rawExtensions = extensionHeader.Split(ExtensionSeparator);
            if (rawExtensions.Length > 0)
            {
                var extensions = new List<WebSocketExtensionData>(rawExtensions.Length);
                foreach (string rawExtension in rawExtensions)
                {
                    string[] extensionParameters = rawExtension.Split(ParameterSeparator);
                    string name = extensionParameters[0].Trim();
                    Dictionary<string, string> parameters;
                    if (extensionParameters.Length > 1)
                    {
                        parameters = new Dictionary<string, string>(extensionParameters.Length - 1);
                        for (int i = 1; i < extensionParameters.Length; i++)
                        {
                            string parameter = extensionParameters[i].Trim();
                            
                            Match match = Parameter.Match(parameter);
                            if (match.Success)
                            {
                                parameters.Add(match.Groups[1].Value, match.Groups[3].Value);
                            }
                        }
                    }
                    else
                    {
                        parameters = new Dictionary<string, string>();
                    }
                    extensions.Add(new WebSocketExtensionData(name, parameters));
                }
                return extensions;
            }
            else
            {
                return new List<WebSocketExtensionData>();
            }
        }

        internal static string AppendExtension(string currentHeaderValue, string extensionName,
            Dictionary<string, string> extensionParameters)
        {
            var newHeaderValue = new StringBuilder(currentHeaderValue?.Length ?? extensionName.Length + 1);
            if (currentHeaderValue != null && currentHeaderValue.Trim() != string.Empty)
            {
                newHeaderValue.Append(currentHeaderValue);
                newHeaderValue.Append(ExtensionSeparator);
            }
            newHeaderValue.Append(extensionName);
            foreach (KeyValuePair<string, string> extensionParameter in extensionParameters)
            {
                newHeaderValue.Append(ParameterSeparator);
                newHeaderValue.Append(extensionParameter.Key);
                if (extensionParameter.Value != null)
                {
                    newHeaderValue.Append(ParameterEqual);
                    newHeaderValue.Append(extensionParameter.Value);
                }
            }
            return newHeaderValue.ToString();
        }
    }
}
