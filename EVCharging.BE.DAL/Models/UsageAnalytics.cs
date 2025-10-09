using EVCharging.BE.DAL.Models;
using EVCharging.BE.DAL.Models.EVChargingManagement.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVCharging.BE.DAL.Models
{
    public class UsageAnalytics
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int analytics_id { get; set; }

        [Required]
        public int user_id { get; set; }

        [Required]
        public int station_id { get; set; }

        public int session_count { get; set; } = 0;

        [Column("total_energy_used", TypeName = "decimal(10,2)")]
        public decimal total_energy_used { get; set; } = 0; // kWh

        [Column("total_cost", TypeName = "decimal(10,2)")]
        public decimal total_cost { get; set; } = 0;

        public int? favorite_station_id { get; set; }

        [Range(0, 23)]
        public int peak_usage_hour { get; set; }

        [Required]
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