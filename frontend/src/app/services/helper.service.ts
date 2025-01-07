import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class HelperService {

  relativeTime(time: string | Date): string {
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
    let seconds = Math.floor(Math.abs(diff / 1000));
    let minutes = Math.floor(seconds / 60);
    let hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    hours = hours % 24;
    minutes = minutes % 60;
    seconds = seconds % 60;
    let countdown = days.toString().padStart(2, '0') + 'd ';
    countdown += hours.toString().padStart(2, '0') + 'h ';
    countdown += minutes.toString().padStart(2, '0') + 'm ';
    countdown += seconds.toString().padStart(2, '0') + 's';
    return countdown;
  }

  getPercentageColor(percentage: number) {
    const minHue = 240;
    const maxHue = 120;
    percentage /= 100;
    const colorString = `hsl(${percentage * (maxHue - minHue) + minHue},60%,${40 + 10 * percentage}%)`;
    return colorString;
  }

  getDifficultyTextColorClass(difficulty: string) {
    const colorMap = new Map();
    colorMap.set('baby', 'text-info');
    colorMap.set('easy', 'text-success');
    colorMap.set('medium', 'text-warning');
    colorMap.set('hard', 'text-danger');
    colorMap.set('leet', 'text-leet');
    let color = colorMap.get(difficulty);
    if (color) {
      return color;
    }
    return 'text-primary';
  }

  difficultyToNumber(difficulty: string): number {
    switch (difficulty) {
      case 'baby':
        return 0;
      case 'easy':
        return 1;
      case 'medium':
        return 2;
      case 'hard':
        return 3;
      case 'leet':
        return 5;
      default:
        return 6;
    }
  }

  getPrimaryCategory(categories: string[]): string {
    return categories.length == 0 ? 'uncategorized' : categories[0];
  }

  getUnsolvedCategoryIconClass(category: string) {
    const iconMap = new Map();
    iconMap.set('crypto', 'bi-key');
    iconMap.set('forensics', 'bi-fingerprint');
    iconMap.set('misc', 'bi-puzzle');
    iconMap.set('pwn', 'bi-cpu');
    iconMap.set('re', 'bi-gear');
    iconMap.set('rev', 'bi-gear');
    iconMap.set('web', 'bi-globe2');
    let icon = iconMap.get(category);
    if (icon) {
      return icon;
    }
    return 'bi-hexagon';
  }

  getSolvedCategoryIconClass(category: string) {
    const iconMap = new Map();
    iconMap.set('crypto', 'bi-key-fill');
    iconMap.set('forensics', 'bi-fingerprint');
    iconMap.set('misc', 'bi-puzzle-fill');
    iconMap.set('pwn', 'bi-cpu-fill');
    iconMap.set('re', 'bi-gear-fill');
    iconMap.set('rev', 'bi-gear-fill');
    iconMap.set('web', 'bi-globe2');
    let icon = iconMap.get(category);
    if (icon) {
      return icon;
    }
    return 'bi-hexagon-fill';
  }
}
