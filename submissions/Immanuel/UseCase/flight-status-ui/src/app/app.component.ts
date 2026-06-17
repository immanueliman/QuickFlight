import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { FlightStatusService } from './flight-status.service';
import { FlightStatusResult } from './flight-status.model';

type SearchMode = 'flight' | 'route';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  mode: SearchMode = 'flight';

  flightNumber = '';
  fromCode = '';
  toCode = '';
  date = '2026-06-15'; // matches the stub data so the demo works out of the box

  // both searches feed the same list - flight search puts one card in it, route
  // search can put several
  results: FlightStatusResult[] = [];
  error = '';
  loading = false;
  searched = false;

  constructor(private api: FlightStatusService) {}

  setMode(mode: SearchMode) {
    this.mode = mode;
    this.results = [];
    this.error = '';
    this.searched = false;
  }

  search() {
    this.error = '';

    if (this.mode === 'flight') {
      if (!this.flightNumber.trim()) {
        this.fail('Please enter a flight number.');
        return;
      }
      this.run(this.api.getStatus(this.flightNumber.trim(), this.date), one => [one]);
    } else {
      if (!this.fromCode.trim() || !this.toCode.trim()) {
        this.fail('Please enter both a from and a to airport code.');
        return;
      }
      this.run(this.api.searchByRoute(this.fromCode.trim(), this.toCode.trim(), this.date), list => list);
    }
  }

  // shared subscribe handling. `shape` turns the API response into the results list.
  private run<T>(call: Observable<T>, shape: (res: T) => FlightStatusResult[]) {
    this.loading = true;
    this.results = [];
    this.searched = false;

    call.subscribe({
      next: (res) => {
        this.results = shape(res);
        this.loading = false;
        this.searched = true;
      },
      error: (err) => {
        this.error = err?.error?.error ?? 'Could not reach the flight status service.';
        this.loading = false;
      }
    });
  }

  private fail(message: string) {
    this.error = message;
    this.results = [];
    this.searched = false;
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
