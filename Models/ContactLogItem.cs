using System;

namespace OnHouseLocal.Models
{
    public class ContactLogItem
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public DateTime ContactDate { get; set; } = DateTime.Now;
        public string ContactType { get; set; } = "전화"; // 전화, 문자, 방문
        public string Result { get; set; } = "생존확인"; // 생존확인, 이미나감, 통화중/부재, 거래진행
        public string Memo { get; set; } = string.Empty;
    }
}
