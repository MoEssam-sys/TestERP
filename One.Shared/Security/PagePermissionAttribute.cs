namespace One.Shared.Security
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class PagePermissionAttribute(string pageKey) : Attribute
    {
        public string PageKey { get; } = pageKey;
    }
}
