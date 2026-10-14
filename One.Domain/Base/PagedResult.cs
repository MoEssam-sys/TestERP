using One.Domain.Enums;

namespace One.Domain.Base
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

        public List<FilterDescriptor>? Filters { get; set; }
        public List<SortDescriptor>? Sorts { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }

        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        public PagedResult()
        {
        }

        public PagedResult(
            List<T> items,
            int totalCount,
            int pageNumber,
            int pageSize,
            List<FilterDescriptor>? filters = null,
            List<SortDescriptor>? sorts = null,
            Dictionary<string, object>? metadata = null)
        {
            Items = items;
            TotalCount = totalCount;
            PageNumber = pageNumber;
            PageSize = pageSize;
            Filters = filters;
            Sorts = sorts;
            Metadata = metadata;
        }
    }

    public class SortDescriptor
    {
        public string Property { get; set; } = "";
        public SortDirc Direction { get; set; } = SortDirc.Asc;
    }

    public class PagedQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public List<FilterDescriptor>? Filters { get; set; }
        public List<SortDescriptor>? Sorts { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class FilterDescriptor
    {
        public string Property { get; set; } = "";
        public FilterType Type { get; set; } = FilterType.Equals;
        public object? Value { get; set; } = null;             // القيمة الرئيسية
        public object? ValueTo { get; set; } = null;           // في حالة Range أو بين قيمتين
    }
}