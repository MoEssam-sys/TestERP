using One.Domain.Base;

namespace One.Application.Services.AdvNavigation
{
    public static class PagingHelper
    {
        public static PagedQuery CreatePagedQuery(
            int pageNumber = 1,
            int pageSize = 10,
            List<FilterDescriptor>? filters = null,
            List<SortDescriptor>? sorts = null,
            Dictionary<string, object>? metadata = null
        )
        {
            return new PagedQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                Filters = filters,
                Sorts = sorts,
                Metadata = metadata
            };
        }

        public static List<FilterDescriptor> AddFilters(params FilterDescriptor[] descriptors)
            => [.. descriptors];

        public static List<SortDescriptor> AddSorts(params SortDescriptor[] descriptors)
            => [.. descriptors];
    }
}