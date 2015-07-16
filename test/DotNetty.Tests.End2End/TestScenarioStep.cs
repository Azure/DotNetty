// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.End2End
{
    using System.Collections.Generic;
    using System.Linq;

    public class TestScenarioStep
    {
        TestScenarioStep()
        {
        }

        public IEnumerable<object> SendMessages { get; private set; }

        public static TestScenarioStep Messages(params object[] messages)
        {
            return new TestScenarioStep
            {
                SendMessages = messages
            };
        }

        public static TestScenarioStep Message(object message)
        {
            return new TestScenarioStep
            {
                SendMessages = Enumerable.Repeat(message, 1)
            };
        }

        public static TestScenarioStep MoreFeedbackExpected()
        {
            return new TestScenarioStep
            {
                SendMessages = Enumerable.Empty<object>()
            };
        }
    }
}