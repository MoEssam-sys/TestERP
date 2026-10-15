using AKSoftware.Localization.MultiLanguages;
using One.Domain.Localization;

namespace One.Shared.Localization;

public class LanguageContainerTranslator(ILanguageContainerService languageContainer) : ITranslator
{
    public string this[string key] => languageContainer.Keys[key];
}
