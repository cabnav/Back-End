# API Migration Guide - EV Charging System

## 🚨 **Deprecated Endpoints**

### **ChargingStations/nearby** → **InteractiveMap/nearby**

#### ❌ **Deprecated (Old)**
```http
GET /api/ChargingStations/nearby?lat=10.762622&lon=106.660172&radiusKm=5
```

**Response:**
```json
[
  {
    "stationId": 1,
    "name": "Tesla Supercharger HCM",
    "address": "123 Nguyen Hue, HCM",
    "latitude": 10.762622,
    "longitude": 106.660172,
    "operator": "Tesla",
    "status": "Active",
    "distanceKm": 2.5,
    "googleMapsUrl": "https://www.google.com/maps?q=10.762622,106.660172"
  }
]
```

#### ✅ **New (Enhanced)**
```http
GET /api/InteractiveMap/nearby?latitude=10.762622&longitude=106.660172&radiusKm=10&connectorTypes=CCS,CHAdeMO
```

**Response:**
```json
[
  {
    "stationId": 1,
    "name": "Tesla Supercharger HCM",
    "address": "123 Nguyen Hue, HCM",
    "latitude": 10.762622,
    "longitude": 106.660172,
    "status": "Active",
    "distanceKm": 2.5,
    "googleMapsUrl": "https://www.google.com/maps?q=10.762622,106.660172",
    
    // 🆕 Real-time status
    "totalPoints": 8,
    "availablePoints": 3,
    "busyPoints": 4,
    "maintenancePoints": 1,
    "utilizationPercentage": 50.0,
    
    // 🆕 Charging points with connector types
    "chargingPoints": [
      {
        "pointId": 101,
        "connectorType": "CCS",
        "powerOutput": 150,
        "pricePerKwh": 4.5,
        "status": "Available",
        "currentPower": 0,
        "isAvailable": true
      }
    ],
    
    // 🆕 Time-based pricing
    "pricing": {
      "basePricePerKwh": 4.5,
      "peakHourPrice": 6.75,
      "offPeakPrice": 3.6,
      "peakHours": "18:00-22:00",
      "currentPrice": 4.5,
      "isPeakHour": false,
      "priceDescription": "Standard Rate"
    }
  }
]
```

## 📊 **Comparison Table**

| **Feature** | **ChargingStations/nearby** | **InteractiveMap/nearby** |
|-------------|----------------------------|---------------------------|
| **Basic Info** | ✅ Name, Address, Location | ✅ Name, Address, Location |
| **Distance** | ✅ Distance calculation | ✅ Distance calculation |
| **Real-time Status** | ❌ Static data | ✅ Live availability |
| **Connector Types** | ❌ Not supported | ✅ CCS, CHAdeMO, AC |
| **Power Output** | ❌ Not included | ✅ Per charging point |
| **Pricing** | ❌ Not included | ✅ Time-based pricing |
| **Utilization** | ❌ Not included | ✅ Real-time % |
| **Filtering** | ❌ Basic radius only | ✅ Advanced filters |

## 🔄 **Migration Steps**

### **Step 1: Update Frontend Code**

#### **Before (Old Code):**
```javascript
// Old implementation
async function getNearbyStations(lat, lon, radius) {
    const response = await fetch(`/api/ChargingStations/nearby?lat=${lat}&lon=${lon}&radiusKm=${radius}`);
    const stations = await response.json();
    
    // Basic display
    stations.forEach(station => {
        console.log(`${station.name} - ${station.distanceKm}km`);
    });
}
```

#### **After (New Code):**
```javascript
// New implementation with enhanced features
async function getNearbyStations(lat, lon, radius, connectorTypes = null) {
    let url = `/api/InteractiveMap/nearby?latitude=${lat}&longitude=${lon}&radiusKm=${radius}`;
    if (connectorTypes) {
        url += `&connectorTypes=${connectorTypes.join(',')}`;
    }
    
    const response = await fetch(url);
    const stations = await response.json();
    
    // Enhanced display with real-time data
    stations.forEach(station => {
        console.log(`${station.name} - ${station.distanceKm}km`);
        console.log(`Available: ${station.availablePoints}/${station.totalPoints}`);
        console.log(`Utilization: ${station.utilizationPercentage}%`);
        console.log(`Current Price: $${station.pricing.currentPrice}/kWh`);
        
        // Show charging points
        station.chargingPoints.forEach(point => {
            if (point.isAvailable) {
                console.log(`  Point ${point.pointId}: ${point.connectorType} ${point.powerOutput}kW - $${point.pricePerKwh}/kWh`);
            }
        });
    });
}
```

### **Step 2: Update API Calls**

#### **Old API Call:**
```http
GET /api/ChargingStations/nearby?lat=10.762622&lon=106.660172&radiusKm=5
```

#### **New API Call:**
```http
GET /api/InteractiveMap/nearby?latitude=10.762622&longitude=106.660172&radiusKm=10&connectorTypes=CCS,CHAdeMO
```

### **Step 3: Handle New Response Structure**

#### **Old Response Handling:**
```javascript
// Old - basic station info only
const station = {
    stationId: 1,
    name: "Tesla Supercharger",
    distanceKm: 2.5
};
```

#### **New Response Handling:**
```javascript
// New - enhanced with real-time data
const station = {
    stationId: 1,
    name: "Tesla Supercharger",
    distanceKm: 2.5,
    
    // Real-time status
    availablePoints: 3,
    totalPoints: 8,
    utilizationPercentage: 50.0,
    
    // Charging points with details
    chargingPoints: [
        {
            pointId: 101,
            connectorType: "CCS",
            powerOutput: 150,
            isAvailable: true,
            pricePerKwh: 4.5
        }
    ],
    
    // Time-based pricing
    pricing: {
        currentPrice: 4.5,
        isPeakHour: false,
        priceDescription: "Standard Rate"
    }
};
```

## 🛠️ **Database First Considerations**

### **Entity Framework Database First**
- **No Code First migrations needed** - Database schema is already defined
- **Entity classes** are auto-generated from existing database
- **DTOs** are manually created for API responses
- **Service layer** handles business logic

### **Current Database Schema (Unchanged)**
```sql
-- Existing tables (no changes needed)
ChargingStations
ChargingPoints  
ChargingSessions
Reservations
Users
DriverProfiles
```

### **New DTOs Added (No Database Changes)**
```csharp
// New DTOs for enhanced API responses
InteractiveStationDTO
RealTimeSessionDTO
ChargingPointMapDTO
PricingInfoDTO
StationFilterDTO
```

## 🚀 **Benefits of Migration**

### **Enhanced Features:**
1. **Real-time Status**: Live availability updates
2. **Advanced Filtering**: By connector type, power, price
3. **Time-based Pricing**: Peak/off-peak rates
4. **Detailed Charging Points**: Individual point status
5. **Utilization Metrics**: Station usage statistics

### **Better User Experience:**
1. **Accurate Availability**: Know which points are free
2. **Price Comparison**: See current vs peak/off-peak rates
3. **Connector Matching**: Filter by your car's connector type
4. **Power Requirements**: Find stations with sufficient power
5. **Real-time Updates**: Live status changes

## ⚠️ **Breaking Changes**

### **Parameter Names:**
- `lat` → `latitude`
- `lon` → `longitude`
- `radiusKm` → `radiusKm` (same)

### **Response Structure:**
- **Old**: `StationResultDTO[]`
- **New**: `InteractiveStationDTO[]`

### **Authentication:**
- **Old**: No authentication required
- **New**: Requires JWT token (`[Authorize]`)

## 📝 **Migration Checklist**

- [ ] Update frontend API calls to use new endpoint
- [ ] Update parameter names (`lat` → `latitude`, `lon` → `longitude`)
- [ ] Handle new response structure with enhanced data
- [ ] Add authentication headers for new endpoint
- [ ] Test new filtering capabilities
- [ ] Update documentation and API tests
- [ ] Remove old endpoint calls (after migration complete)

## 🔧 **Breaking Changes**

The old endpoint has been **completely removed**:
- ❌ **Removed**: `/api/ChargingStations/nearby`
- ✅ **Use**: `/api/InteractiveMap/nearby` (enhanced features)
- ⚠️ **Breaking**: All existing calls to old endpoint will return 404

## 📞 **Support**

For migration assistance:
- Check API documentation: `/swagger`
- Test endpoints: Use provided HTTP test files
- Database queries: No changes needed (Database First)
- Entity updates: Auto-generated from existing schema
