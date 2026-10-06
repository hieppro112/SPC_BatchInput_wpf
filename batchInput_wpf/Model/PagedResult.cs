using System.Collections.Generic;

namespace batchInput_wpf.Model
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; }
        public int TotalPages { get; set; } = 1;
    }
}
