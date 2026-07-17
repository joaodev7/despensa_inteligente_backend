using System;

namespace DespensaInteligente.Application.Exceptions
{
    public class LlmException : Exception
    {
        public LlmException(string message) : base(message) { }
        public LlmException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class InvalidApiKeyException : LlmException
    {
        public InvalidApiKeyException(string message) : base(message) { }
        public InvalidApiKeyException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class RateLimitExceededException : LlmException
    {
        public RateLimitExceededException(string message) : base(message) { }
        public RateLimitExceededException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class LlmTimeoutException : LlmException
    {
        public LlmTimeoutException(string message) : base(message) { }
        public LlmTimeoutException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class InvalidLlmResponseException : LlmException
    {
        public InvalidLlmResponseException(string message) : base(message) { }
        public InvalidLlmResponseException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class InvalidJsonException : LlmException
    {
        public InvalidJsonException(string message) : base(message) { }
        public InvalidJsonException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ModelUnavailableException : LlmException
    {
        public ModelUnavailableException(string message) : base(message) { }
        public ModelUnavailableException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ModelNotFoundException : LlmException
    {
        public ModelNotFoundException(string message) : base(message) { }
        public ModelNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class QuotaExceededException : LlmException
    {
        public QuotaExceededException(string message) : base(message) { }
        public QuotaExceededException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class HighDemandException : LlmException
    {
        public HighDemandException(string message) : base(message) { }
        public HighDemandException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class LlmCommunicationException : LlmException
    {
        public LlmCommunicationException(string message) : base(message) { }
        public LlmCommunicationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
