import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FlightStatusService } from './flight-status.service';
import { FlightStatusResult } from './flight-status.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  flightNumber = '';
  date = '2026-06-15'; // matches the stub data so the demo works out of the box

  result?: FlightStatusResult;
  error = '';
  loading = false;

  constructor(private api: FlightStatusService) {}

  search() {
    // simple client-side check - the API validates too
    if (!this.flightNumber.trim() || !this.date) {
      this.error = 'Please enter a flight number and a date.';
      this.result = undefined;
      return;
    }

    this.loading = true;
    this.error = '';
    this.result = undefined;

    this.api.getStatus(this.flightNumber.trim(), this.date).subscribe({
      next: (res) => {
        this.result = res;
        this.loading = false;
      },
      error: (err) => {
        // show whatever the API said, or a generic message if it's unreachable
        this.error = err?.error?.error ?? 'Could not reach the flight status service.';
        this.loading = false;
      }
    });
  }

  // maps the unified status to a css class for the colour coding
  statusClass(status?: string): string {
    switch (status) {
      case 'OnTime': return 'status-ontime';
      case 'Delayed': return 'status-delayed';
      case 'Cancelled':
      case 'Diverted': return 'status-bad';
      default: return 'status-unknown';
    }
  }
}
