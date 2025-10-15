namespace EVCharging.BE.Services.Services.Implementations
{
    public class LocationService : ILocationService
    {
        // Tính khoảng cách giữa 2 tọa độ (Haversine Formula)
        public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // km
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }

    public interface ILocationService
    {
        double CalculateDistance(double lat1, double lon1, double lat2, double lon2);
    }
}
