using System.ComponentModel.DataAnnotations;

namespace One.Domain.Base
{
    public class BaseFilter
    {
        public const int MaxPageSize = 100;

        // Pagination
        [Range(1, 1_000_000)]
        public int PageNumber { get; set; } = 1;

        [Range(1, MaxPageSize)]
        public int PageSize { get; set; } = 10;

        public string? SortBy { get; set; }

        public bool SortDescending { get; set; } = true;
    }
}
