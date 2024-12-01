import { Component, OnInit } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.less'],
})
export class AppComponent implements OnInit {
  title = 'Berg Frontend';

  constructor(
    private router: Router,
    private modalService: NgbModal
  ) {
  }

  ngOnInit() {
    this.router.events.subscribe(event => {
      if (event instanceof NavigationEnd) {
        this.modalService.dismissAll();
      }
    });
  }
}
