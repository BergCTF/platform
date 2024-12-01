import { Component, OnInit } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { Router } from '@angular/router';
import { Helpers } from 'src/app/services/helpers.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.less'],
})
export class HomeComponent implements OnInit {
  constructor(
    private apiService: ApiService,
    private router: Router,
    public helpers: Helpers
  ) {}
  ngOnInit() {
    this.apiService.refreshPlayerSelf(() => {
      const player = this.apiService.getPlayerSelf()?.player;
      if (this.router.url === '/' && player) {
        this.router.navigate(['/challenges']);
      } else {
        this.router.navigate(['/home']);
      }
    });
  }
}
