import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { Page } from 'src/app/api-model';
import { DataService } from 'src/app/services/data.service';

@Component({
  selector: 'app-dynamic-page',
  imports: [],
  templateUrl: './dynamic-page.component.html',
  styleUrl: './dynamic-page.component.less'
})
export class DynamicPageComponent implements OnInit, OnDestroy {

  private updatePagesSubscription: Subscription | undefined;
  private updateRouteSubscription: Subscription | undefined;
  private _pages: readonly Page[] = [];
  public page: Page = {
    title: 'Not found',
    index: 0,
    path: 'not-found',
    content: '<p>This page does not exist.</p>',
  };

  constructor(
    private router: Router,
    public dataService: DataService
  ) {}

  ngOnInit(): void {
    this.updatePagesSubscription = this.dataService.pages.subscribe(pages => this.handlePagesUpdate(pages));
    this.updateRouteSubscription = this.router.events.subscribe(event => this.handleUpdate());
  }

  ngOnDestroy(): void {
    this.updatePagesSubscription?.unsubscribe();
    this.updateRouteSubscription?.unsubscribe();
  }

  private handlePagesUpdate(pages: readonly Page[]) {
    this._pages = pages;
    this.handleUpdate();
  }

  private handleUpdate() {
    let currentPath = this.router.url;
    if (currentPath.startsWith('/')) {
      currentPath = currentPath.substring(1);
    }
    let page = this._pages.map(p => {
      if (p.path.startsWith('/')){
        p.path = p.path.substring(1);
      }
      return p;
    }).find(p => p.path == currentPath);
    if (page != undefined) {
      this.page = page;
    }
  }
}
