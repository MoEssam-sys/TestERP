using One.Domain.Localization;

namespace One.API.Services
{
    public class ServerTranslator : ITranslator
    {
        public string this[string key] => key; // Default to return the key itself
    }
}
