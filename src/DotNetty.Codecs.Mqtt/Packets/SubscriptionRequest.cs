// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    using System;
    using System.Diagnostics.Contracts;

    public class SubscriptionRequest : IEquatable<SubscriptionRequest>
    {
        public SubscriptionRequest(string topicFilter, QualityOfService qualityOfService)
        {
            Contract.Requires(!string.IsNullOrEmpty(topicFilter));

            this.TopicFilter = topicFilter;
            this.QualityOfService = qualityOfService;
        }

        public string TopicFilter { get; private set; }

        public QualityOfService QualityOfService { get; private set; }

        public bool Equals(SubscriptionRequest other)
        {
            return this.QualityOfService == other.QualityOfService
                && this.TopicFilter.Equals(other.TopicFilter, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return string.Format("{0}[TopicFilter={1}, QualityOfService={2}]", this.GetType().Name, this.TopicFilter, this.QualityOfService);
        }
    }
}