
namespace AstroValleyAssistant.Models
{
    public class RegridSearchResult
    {
        public PropertyRecord? Record { get; set; }
        public bool IsMultiple { get; set; }
        public bool NotFound => Record == null && !IsMultiple;
        public List<RegridMatch> Matches { get; set; }
    }
}
