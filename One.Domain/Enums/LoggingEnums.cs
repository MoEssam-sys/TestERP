namespace One.Domain.Enums
{
    public enum LogSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }

    public enum LogCategory
    {
        General = 0,
        Activity = 1,
        Exception = 2,
        UserError = 3,
        Security = 4,
        System = 5
    }
}
