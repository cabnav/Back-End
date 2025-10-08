using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("IncidentReport")]
    public class IncidentReport
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("report_id")]
        public int report_id { get; set; }

        [Required]
        [Column("reporter_id")]
        public int reporter_id { get; set; }

        [Required]
        [Column("point_id")]
        public int point_id { get; set; }

        [Required]
        [StringLength(200)]
        public string title { get; set; }

        public string description { get; set; }

        [StringLength(20)]
        public string priority { get; set; } = "medium"; // low, medium, high, critical

        [StringLength(20)]
        public string status { get; set; } = "pending"; // pending, in_progress, resolved

        [Column("reported_at")]
        public DateTime reported_at { get; set; } = DateTime.UtcNow;

        [Column("resolved_at")]
        public DateTime? resolved_at { get; set; }

        [Column("resolved_by")]
        public int? resolved_by { get; set; }

        // Navigation properties
        [ForeignKey("reporter_id")]
        public virtual User Reporter { get; set; }

        [ForeignKey("point_id")]
        public virtual ChargingPoint ChargingPoint { get; set; }

        [ForeignKey("resolved_by")]
        public virtual User Resolver { get; set; }
    }
}