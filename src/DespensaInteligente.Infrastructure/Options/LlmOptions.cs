namespace DespensaInteligente.Infrastructure.Options
{
    public class LlmOptions
    {
        public string Provider { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
    }
}
