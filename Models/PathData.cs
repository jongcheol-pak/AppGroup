namespace AppGroup.Models
{
    /// <summary>
    /// 앱 경로의 부가 정보 (툴팁, 실행 인수, 아이콘, 항목 유형)
    /// </summary>
    public class PathData
    {
        public string Tooltip { get; set; }
        public string Args { get; set; }
        public string Icon { get; set; }
        public string ItemType { get; set; } = "App";
    }
}
