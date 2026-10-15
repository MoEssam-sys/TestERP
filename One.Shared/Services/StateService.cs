using One.Domain.Enums;

namespace One.Shared.Services
{
    public class StateService
    {
        private LanguageState Languange { get; set; } = LanguageState.English;
        public bool IsEnglishLanguage => Languange == LanguageState.English;

        public ListsData Lists { get; set; } = new();

        public async Task SetLanguage(LanguageState lang)
        {
            Languange = lang;
            await Task.CompletedTask;
        }

    }

    public class ListsData
    {
        //public List<FacultySelectOutput> FacultiesList { get; set; }
        //public List<UsersSelectOutput> UsersList { get; set; }
    }
}
