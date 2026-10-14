using One.Domain.Base;
using One.Domain.Enums;

namespace One.Application.Services.AdvNavigation
{
    public static class Sort
    {
        public static SortDescriptor Asc(string prop)
            => new() { Property = prop, Direction = SortDirc.Asc };

        public static SortDescriptor Desc(string prop)
            => new() { Property = prop, Direction = SortDirc.Desc };
    }
}