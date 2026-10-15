namespace One.Shared.Services
{
    public static class EnumConverter
    {
        public static List<string> GetEnumNames<T>() where T : Enum
        {
            return [.. Enum.GetNames(typeof(T))]; //Convert the enum values to list
        }
    }
}