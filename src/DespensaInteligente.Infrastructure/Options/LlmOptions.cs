using System.Collections.Generic;

namespace DespensaInteligente.Infrastructure.Options
{
    public class LlmOptions
    {
        public string Provider { get; set; } = string.Empty;

        public string? Model { get; set; }

        public List<string>? Models { get; set; }

        public string ApiKey { get; set; } = string.Empty;
    }
}
