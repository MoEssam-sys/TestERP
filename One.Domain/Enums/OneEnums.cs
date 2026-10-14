namespace One.Domain.Enums
{
    public enum Operation
    {
        Create,
        GetAll,
        GetById,
        Update,
        Delete,
        Activate,
        Deactivate,
        Other,
    }

    public enum OneBtnClick : byte
    {
        Save = 1,
        Update,
        Clear,
        Delete,
    }

    public enum LikeConditions
    {
        OR,
        AND,
    }

    public enum SortDirc
    {
        Asc, // Ascending
        Desc // Descending
    }

    public enum FilterType
    {
        Equals = 1,
        NotEquals,
        Range,
        Contains,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual
    }

    public enum LanguageState : byte
    {
        Arabic = 1,
        English,
        German,
        French
    }

    public enum CallZaCenter : byte
    {
        CloseMe = 1,
        ImTouched
    }

    public enum ChildRequest : byte
    {
        RequestLists = 1,
        UpdateList,
        OpenFaculties,
        SelectFaculty,
        LoginSuccessfully,
        UpdateFaculty,
    }

    public enum LoaderType
    {
        OneERP,
        Wifi,
        Hand,
        Circles,
        Writer,
        DayNight
    }
}