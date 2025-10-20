namespace backend.Config
{
    public class PulsarSettings
    {
        public string BrokerUrl { get; set; } = string.Empty;
        public TopicSettings Topics { get; set; } = new();
        public string SubscriptionName { get; set; } = string.Empty;
    }

    public class TopicSettings
    {
        public string PRODUCER { get; set; } = string.Empty;
        public string CONSUMER { get; set; } = string.Empty;
    }
}
