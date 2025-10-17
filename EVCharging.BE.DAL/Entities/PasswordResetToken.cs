using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Entities
{
    /// <summary>
    /// Entity cho bảng PasswordResetToken - lưu token đặt lại mật khẩu
    /// </summary>
    [Table("PasswordResetToken")]
    public class PasswordResetToken
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("used_at")]
        public DateTime? UsedAt { get; set; }

        [Required]
        [Column("is_revoked")]
        public bool IsRevoked { get; set; } = false;

        // Navigation property
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}