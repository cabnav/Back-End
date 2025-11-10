using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;

namespace EVCharging.BE.Services.Services.Common.Implementations
{
    public class SearchService : ISearchService
    {
        public IQueryable<ChargingStation> FilterStations(IQueryable<ChargingStation> query, string? keyword, string? status)
        {
            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(s => s.Name.Contains(keyword) || s.Address.Contains(keyword));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(s => s.Status != null && s.Status.ToLower() == status.ToLower());

            return query;
        }

        public IQueryable<ChargingPoint> FilterPoints(IQueryable<ChargingPoint> query, string? connectorType, int? minPower)
        {
            if (!string.IsNullOrWhiteSpace(connectorType))
                query = query.Where(p => p.ConnectorType == connectorType);

            if (minPower.HasValue)
                query = query.Where(p => p.PowerOutput >= minPower.Value);

            return query;
        }
    }
}
