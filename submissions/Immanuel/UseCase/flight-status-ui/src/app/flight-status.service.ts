import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { FlightStatusResult } from './flight-status.model';

// Base URL of the .NET API. Hardcoded for the dev setup - in a real app this would
// come from the environment files.
const API_BASE = 'http://localhost:5279';

@Injectable({ providedIn: 'root' })
export class FlightStatusService {
  constructor(private http: HttpClient) {}

  getStatus(flightNumber: string, date: string): Observable<FlightStatusResult> {
    const params = new HttpParams()
      .set('flightNumber', flightNumber)
      .set('date', date);

    return this.http.get<FlightStatusResult>(`${API_BASE}/flights/status`, { params });
  }
}
