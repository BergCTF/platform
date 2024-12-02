import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class HelperService {

  relativeTime(time: string): string {
    return this.relativeTimeTo(new Date(time), new Date(Date.now()));
  }

  relativeTimeTo(date: Date, now: Date): string {
    const rtf1 = new Intl.RelativeTimeFormat('en', {});
    const diff = date.getTime() - now.getTime();
    const seconds = Math.floor(Math.abs(diff / 1000));
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    if (days > 0) {
      return rtf1.format(-days, 'day');
    }
    if (hours > 0) {
      return rtf1.format(-hours, 'hour');
    }
    if (minutes > 0) {
      return rtf1.format(-minutes, 'minute');
    } else {
      return rtf1.format(-seconds, 'second');
    }
  }
}
