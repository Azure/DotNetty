// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public class SubscriptionRequest
    {
        public SubscriptionRequest(string topicFilter, QualityOfService qualityOfService)
        {
            this.TopicFilter = topicFilter;
            this.QualityOfService = qualityOfService;
        }

        public string TopicFilter { get; private set; }

        public QualityOfService QualityOfService { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}[TopicFilter={1}, QualityOfService={2}]", this.GetType().Name, this.TopicFilter, this.QualityOfService);
        }
    }
}