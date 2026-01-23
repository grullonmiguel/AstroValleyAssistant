
namespace AstroValleyAssistant.Models
{
    public class RegridSearchResult
    {
        public RegridRecord? Record { get; set; }
        public bool IsMultiple { get; set; }
        public bool NotFound => Record == null && !IsMultiple;
        public List<RegridMatch> Matches { get; set; }
    }
}
