using System.Collections.Generic;

namespace AppGroup.Models
{
    /// <summary>
    /// 그룹의 설정 및 경로 데이터
    /// </summary>
    public class GroupData
    {
        public required string GroupIcon { get; set; }
        public required string GroupName { get; set; }
        public bool GroupHeader { get; set; }
        public bool ShowGroupEdit { get; set; } = true;
        public int GroupCol { get; set; }
        public int GroupId { get; set; }
        public bool ShowLabels { get; set; } = false;
        public int LabelSize { get; set; } = 12;
        public string LabelPosition { get; set; } = "Bottom";

        public Dictionary<string, PathData> Path { get; set; }
    }
}
