import { Component, OnInit } from '@angular/core';
import { DataService } from './services/data.service';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.less'],
    standalone: false
})
export class AppComponent implements OnInit {
  title = 'Berg Frontend';

  constructor(
    public dataService: DataService
  ) {}

  ngOnInit() {
  }
}
