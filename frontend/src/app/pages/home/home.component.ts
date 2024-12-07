import { Component, OnInit } from '@angular/core';
import { HelperService } from 'src/app/services/helper.service';

@Component({
    selector: 'app-home',
    templateUrl: './home.component.html',
    styleUrls: ['./home.component.less'],
    standalone: false
})
export class HomeComponent implements OnInit {
  constructor(
    public helpers: HelperService
  ) {}
  ngOnInit() {
  }
}
