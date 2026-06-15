// Mirrors the FlightStatusResult the API returns. The optional fields only come back
// from AeroTrack, so they may be missing.
export type UnifiedStatus = 'OnTime' | 'Delayed' | 'Cancelled' | 'Diverted' | 'Unknown';

export interface FlightStatusResult {
  flightNumber: string;
  date: string;
  status: UnifiedStatus;
  scheduledDeparture?: string;
  actualDeparture?: string;
  scheduledArrival?: string;
  actualArrival?: string;
  terminal?: string;
  gate?: string;
  delayReason?: string;
  source?: string;
  lastUpdatedUtc?: string;
  message?: string;
}
