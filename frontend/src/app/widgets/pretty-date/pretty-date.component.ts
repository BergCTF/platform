import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

@Component({
    selector: 'app-pretty-date',
    templateUrl: './pretty-date.component.html',
    styleUrls: ['./pretty-date.component.less'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: false
})
export class PrettyDateComponent {
  @Input()
  date: string | Date = '';
  prettyDate = (): string[] => {
    let dateStr = null;
    if (this.date instanceof Date) {
      dateStr = this.date.toISOString();
    } else {
      dateStr = this.date;
    }
    const datePart = dateStr.slice(0, 10);
    const timePart = dateStr.slice(11, 19);
    return [datePart, timePart];
  };
}
