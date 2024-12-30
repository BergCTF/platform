import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class HelperService {

  relativeTime(time: string): string {
    return this.relativeTimeTo(new Date(time), new Date(Date.now()));
  }

  relativeTimeTo(date: Date, now: Date | null): string {
    if (now == null)
      now = new Date();
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

  getCountdownText(target: Date, now: Date | null): string {
    if (now == null)
      now = new Date();
    const diff = target.getTime() - now.getTime();
    const seconds = Math.floor(Math.abs(diff / 1000));
    let minutes = Math.floor(seconds / 60);
    let hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    hours = hours % 24;
    minutes = minutes % 60;
    let countdown = days.toString().padStart(2, '0') + 'd ';
    countdown += hours.toString().padStart(2, '0') + 'h ';
    countdown += minutes.toString().padStart(2, '0') + 'm';
    return countdown;
  }

  getBarColor(percentage: number) {
    const minHue = 240;
    const maxHue = 120;
    percentage /= 100;
    const colorString = `hsl(${percentage * (maxHue - minHue) + minHue},60%,${40 + 10 * percentage}%)`;
    return colorString;
  }
}
