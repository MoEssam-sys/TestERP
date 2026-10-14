namespace One.Domain.Localization;

public interface ITranslator
{
    string this[string key] { get; }
}
