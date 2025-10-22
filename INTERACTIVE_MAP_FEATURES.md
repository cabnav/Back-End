# Interactive Charging Station Map & Real-time Features

## Overview
This document describes the implementation of interactive charging station map functionality with real-time status updates, filtering capabilities, time-based pricing, and charging completion notifications.

## Features Implemented

### 1. Interactive Charging Station Map
- **Real-time status display** of charging stations and points
- **Google Maps integration** with station markers
- **Distance calculation** from user location
- **Station utilization** percentage display
- **Live availability** status updates

### 2. Advanced Filtering
- **Connector type filtering**: CCS, CHAdeMO, AC
- **Power output filtering**: Minimum power requirements
- **Price filtering**: Maximum price per kWh
- **Availability filtering**: Show only available stations
- **Distance filtering**: Search within radius

### 3. Time-based Pricing
- **Peak hour pricing**: 50% increase during 6 PM - 10 PM
- **Off-peak pricing**: 20% discount during 12 AM - 6 AM
- **Standard pricing**: Regular rates during other hours
- **Real-time price display** based on current time

### 4. Charging Completion Notifications
- **Email notifications** with detailed session summary
- **SMS notifications** (framework ready for implementation)
- **Real-time updates** via SignalR
- **Session completion** detection and alerts

### 5. Real-time SOC & Time Display
- **State of Charge (SOC)** percentage tracking
- **Estimated remaining time** calculation
- **Current power output** monitoring
- **Energy consumption** tracking
- **Cost calculation** in real-time

## API Endpoints

### Interactive Map Controller (`/api/InteractiveMap`)

#### Get Interactive Stations
```http
POST /api/InteractiveMap/stations
Content-Type: application/json

{
  "name": "Station Name",
  "address": "Station Address",
  "latitude": 10.762622,
  "longitude": 106.660172,
  "maxDistanceKm": 10,
  "connectorTypes": ["CCS", "CHAdeMO"],
  "minPowerOutput": 50,
  "maxPricePerKwh": 5.0,
  "isAvailable": true,
  "status": "Active"
}
```

#### Get Nearby Stations
```http
GET /api/InteractiveMap/nearby?latitude=10.762622&longitude=106.660172&radiusKm=10&connectorTypes=CCS,CHAdeMO
```

#### Get Station Status
```http
GET /api/InteractiveMap/station/{stationId}/status
```

#### Filter by Connector Type
```http
GET /api/InteractiveMap/filter/connector/CCS?latitude=10.762622&longitude=106.660172&maxDistanceKm=20
```

#### Get Stations with Pricing
```http
GET /api/InteractiveMap/pricing?latitude=10.762622&longitude=106.660172&showPeakHours=true
```

### Real-time Charging Controller (`/api/RealTimeCharging`)

#### Get Real-time Session Data
```http
GET /api/RealTimeCharging/session/{sessionId}
```

#### Update Session Data
```http
PUT /api/RealTimeCharging/session/{sessionId}/update
Content-Type: application/json

{
  "currentSOC": 75,
  "currentPower": 45.5
}
```

#### Get Remaining Time
```http
GET /api/RealTimeCharging/session/{sessionId}/remaining-time?currentSOC=75&targetSOC=90
```

#### Get Active Sessions
```http
GET /api/RealTimeCharging/driver/{driverId}/active-sessions
```

#### Check Charging Completion
```http
POST /api/RealTimeCharging/session/{sessionId}/check-completion
```

#### Get Current SOC
```http
GET /api/RealTimeCharging/session/{sessionId}/soc
```

#### Send Charging Completion Notification
```http
POST /api/RealTimeCharging/session/{sessionId}/send-notification
```

## Data Models

### InteractiveStationDTO
```csharp
public class InteractiveStationDTO
{
    public int StationId { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Status { get; set; }
    public double DistanceKm { get; set; }
    public string GoogleMapsUrl { get; set; }
    
    // Real-time status
    public int TotalPoints { get; set; }
    public int AvailablePoints { get; set; }
    public int BusyPoints { get; set; }
    public double UtilizationPercentage { get; set; }
    
    // Charging points
    public List<ChargingPointMapDTO> ChargingPoints { get; set; }
    
    // Pricing information
    public PricingInfoDTO Pricing { get; set; }
}
```

### RealTimeSessionDTO
```csharp
public class RealTimeSessionDTO
{
    public int SessionId { get; set; }
    public int DriverId { get; set; }
    public DateTime StartTime { get; set; }
    public string Status { get; set; }
    
    // Real-time charging data
    public int CurrentSOC { get; set; }
    public int InitialSOC { get; set; }
    public int? TargetSOC { get; set; }
    public double EnergyUsed { get; set; }
    public double CurrentPower { get; set; }
    public double AveragePower { get; set; }
    
    // Time estimates
    public int DurationMinutes { get; set; }
    public int? EstimatedRemainingMinutes { get; set; }
    public DateTime? EstimatedCompletionTime { get; set; }
    
    // Cost information
    public decimal CurrentCost { get; set; }
    public decimal EstimatedTotalCost { get; set; }
    public decimal PricePerKwh { get; set; }
}
```

## Frontend Integration

### Google Maps Integration
```javascript
// Initialize map with charging stations
function initializeMap() {
    const map = new google.maps.Map(document.getElementById('map'), {
        zoom: 12,
        center: { lat: 10.762622, lng: 106.660172 }
    });
    
    // Load charging stations
    loadChargingStations(map);
}

// Load and display charging stations
async function loadChargingStations(map) {
    const response = await fetch('/api/InteractiveMap/nearby?latitude=10.762622&longitude=106.660172&radiusKm=20');
    const stations = await response.json();
    
    stations.forEach(station => {
        const marker = new google.maps.Marker({
            position: { lat: station.latitude, lng: station.longitude },
            map: map,
            title: station.name,
            icon: getStationIcon(station)
        });
        
        const infoWindow = new google.maps.InfoWindow({
            content: createStationInfoContent(station)
        });
        
        marker.addListener('click', () => {
            infoWindow.open(map, marker);
        });
    });
}

// Get appropriate icon based on station status
function getStationIcon(station) {
    if (station.availablePoints > 0) {
        return 'green-marker.png'; // Available
    } else if (station.busyPoints > 0) {
        return 'yellow-marker.png'; // Busy
    } else {
        return 'red-marker.png'; // Unavailable
    }
}
```

### Real-time Updates with SignalR
```javascript
// Connect to SignalR hub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chargingSessionHub")
    .build();

// Start connection
connection.start().then(() => {
    console.log("Connected to SignalR hub");
    
    // Join session group for real-time updates
    connection.invoke("JoinSessionGroup", sessionId);
}).catch(err => console.error(err));

// Listen for session updates
connection.on("SessionUpdated", (sessionData) => {
    updateSessionDisplay(sessionData);
});

// Update session display with real-time data
function updateSessionDisplay(sessionData) {
    document.getElementById('currentSOC').textContent = sessionData.currentSOC + '%';
    document.getElementById('remainingTime').textContent = sessionData.estimatedRemainingMinutes + ' minutes';
    document.getElementById('currentPower').textContent = sessionData.currentPower + ' kW';
    document.getElementById('energyUsed').textContent = sessionData.energyUsed.toFixed(2) + ' kWh';
    document.getElementById('currentCost').textContent = '$' + sessionData.currentCost.toFixed(2);
}
```

### Filtering Implementation
```javascript
// Apply filters to station search
function applyFilters() {
    const filters = {
        connectorTypes: getSelectedConnectorTypes(),
        minPowerOutput: document.getElementById('minPower').value,
        maxPricePerKwh: document.getElementById('maxPrice').value,
        isAvailable: document.getElementById('availableOnly').checked,
        maxDistanceKm: document.getElementById('maxDistance').value
    };
    
    searchStations(filters);
}

// Search stations with filters
async function searchStations(filters) {
    const response = await fetch('/api/InteractiveMap/stations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(filters)
    });
    
    const stations = await response.json();
    displayStations(stations);
}
```

## Configuration

### Time-based Pricing Configuration
The pricing system can be configured in the `ChargingStationService.CalculatePricingInfo` method:

```csharp
// Peak hours: 6 PM - 10 PM (50% increase)
var isPeakHour = currentHour >= 18 && currentHour <= 22;

// Off-peak hours: 12 AM - 6 AM (20% discount)
var isOffPeakHour = currentHour >= 0 && currentHour <= 6;

// Peak multiplier: 1.5x (50% increase)
var peakMultiplier = 1.5m;

// Off-peak multiplier: 0.8x (20% discount)
var offPeakMultiplier = 0.8m;
```

### Email Notification Configuration
Email notifications are configured in `appsettings.json`:

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "noreply@evcharging.com",
    "FromName": "EV Charging System"
  }
}
```

## Usage Examples

### 1. Find CCS Charging Stations Near User
```http
GET /api/InteractiveMap/filter/connector/CCS?latitude=10.762622&longitude=106.660172&maxDistanceKm=15
```

### 2. Get Stations with Peak Hour Pricing
```http
GET /api/InteractiveMap/pricing?latitude=10.762622&longitude=106.660172&showPeakHours=true
```

### 3. Monitor Charging Session in Real-time
```http
GET /api/RealTimeCharging/session/123
```

### 4. Update Session with Current SOC
```http
PUT /api/RealTimeCharging/session/123/update
Content-Type: application/json

{
  "currentSOC": 85,
  "currentPower": 42.3
}
```

### 5. Check if Charging is Complete
```http
POST /api/RealTimeCharging/session/123/check-completion
```

## Security Considerations

- All endpoints require authentication (`[Authorize]` attribute)
- User can only access their own charging sessions
- Input validation on all parameters
- SQL injection protection through Entity Framework
- Rate limiting recommended for production

## Performance Considerations

- Database queries are optimized with proper indexing
- Real-time updates use SignalR for efficient communication
- Caching can be implemented for frequently accessed station data
- Pagination recommended for large result sets

## Future Enhancements

1. **SMS Notifications**: Implement SMS service integration
2. **Push Notifications**: Mobile app push notifications
3. **Advanced Analytics**: Usage patterns and optimization suggestions
4. **Machine Learning**: Predictive availability and pricing
5. **IoT Integration**: Direct communication with charging hardware
6. **Blockchain**: Secure payment and energy trading
7. **AR Features**: Augmented reality station finder
8. **Voice Commands**: Voice-activated station search
