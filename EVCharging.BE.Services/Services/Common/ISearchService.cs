using EVCharging.BE.DAL.Entities;

namespace EVCharging.BE.Services.Services.Common
{
    public interface ISearchService
    {
        IQueryable<ChargingStation> FilterStations(IQueryable<ChargingStation> query, string? keyword, string? status);
        IQueryable<ChargingPoint> FilterPoints(IQueryable<ChargingPoint> query, string? connectorType, int? minPower);
    }
}
