using System;

namespace OnHouseLocal.Models
{
    public class UserItem
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty; // 로그인 아이디
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty; // 중개사 성명
        public string AgencyName { get; set; } = string.Empty; // 상호명
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
