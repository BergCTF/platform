import { Component, OnDestroy, OnInit } from '@angular/core';
import { DataService } from './services/data.service';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { Metadata } from './model';
import { AsyncPipe } from '@angular/common';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.less'],
    imports: [RouterLink, RouterLinkActive, RouterOutlet, AsyncPipe]
})
export class AppComponent implements OnInit, OnDestroy {

  private metadataSubscription: Subscription | null = null;

  title = 'Berg Frontend';
  theme: string | null = null;
  metadata: Metadata | null = null;

  constructor(
    public dataService: DataService,
  ) {}

  ngOnInit() {
    this.theme = localStorage.getItem('theme');
    this.setTheme(this.getPreferredTheme());
    this.metadataSubscription = this.dataService.metadata.subscribe(metadata => {
      this.metadata = metadata;
    });
  }

  ngOnDestroy(): void {
    this.metadataSubscription?.unsubscribe();
  }

  getPreferredTheme(){
    if (this.theme) {
        return this.theme;
    }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  setTheme(theme: string) {
    if (theme === 'auto' && window.matchMedia('(prefers-color-scheme: dark)').matches) {
        document.documentElement.setAttribute('data-bs-theme', 'dark');
        localStorage.setItem('theme', 'dark');
    } else {
        document.documentElement.setAttribute('data-bs-theme', theme);
        localStorage.setItem('theme', theme);
    }
    this.theme = theme;
  }
}
