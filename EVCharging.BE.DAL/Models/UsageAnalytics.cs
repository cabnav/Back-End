using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    [Table("UsageAnalytics")]
    public class UsageAnalytics
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("analytics_id")]
        public int analytics_id { get; set; }

        [Required]
        [Column("user_id")]
        public int user_id { get; set; }

        [Required]
        [Column("station_id")]
        public int station_id { get; set; }

        [Column("session_count")]
        public int session_count { get; set; } = 0;

        [Column("total_energy_used", TypeName = "decimal(10,2)")]
        public decimal total_energy_used { get; set; } = 0; // kWh

        [Column("total_cost", TypeName = "decimal(10,2)")]
        public decimal total_cost { get; set; } = 0;

        [Column("favorite_station_id")]
        public int? favorite_station_id { get; set; }

        [Range(0, 23)]
        [Column("peak_usage_hour")]
        public int peak_usage_hour { get; set; }

        [Required]
        [Column("analysis_month")]
        public DateTime analysis_month { get; set; }

        // Navigation properties
        [ForeignKey("user_id")]
        public virtual User User { get; set; }

        [ForeignKey("station_id")]
        public virtual ChargingStation ChargingStation { get; set; }

        [ForeignKey("favorite_station_id")]
        public virtual ChargingStation FavoriteStation { get; set; }
    }
}