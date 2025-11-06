namespace Data.Access.DTOs
{
    public class PaginationParameters
    {
        private const int MaxPageSize = 100;
        private int _pageSize = 25;

        public int PageNumber { get; set; } = 1;

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
        }
        public string? SearchTerm { get; set; }
        public int? Status { get; set; }

    }

    public class PagedList<T>
    {
        public List<T> Items { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }

        public PagedList(List<T> items, long totalCount, int pageNumber, int pageSize)
        {
            TotalCount = totalCount;
            PageSize = pageSize;
            CurrentPage = pageNumber;
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            Items = items;
        }

        public static PagedList<T> Empty<T>(int pageNumber, int pageSize)
        {
            return new PagedList<T>(new List<T>(), 0, pageNumber, pageSize);
        }
    }

}
