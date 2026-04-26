import { Component, OnDestroy, OnInit } from "@angular/core";
import { DataService } from "./services/data.service";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { Subscription } from "rxjs";
import { Metadata, Page, Player } from "./api-model";
import { NgbDropdownModule, NgbTooltip } from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: "app-root",
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.less"],
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    NgbDropdownModule,
    NgbTooltip,
  ],
})
export class AppComponent implements OnInit, OnDestroy {
  year = new Date().getFullYear();
  theme: string | null = null;
  currentPlayer: Player | null = null;
  metadata: Metadata | null = null;
  areTeamsEnabled = false;
  pages: readonly Page[] = [];

  private metadataSubscription: Subscription | null = null;
  private pagesSubscription: Subscription | null = null;
  private loggedInPlayerSubscription: Subscription | null = null;
  private areTeamsEnabledSubscription: Subscription | null = null;

  constructor(public dataService: DataService) {}

  ngOnInit() {
    this.theme = localStorage.getItem("theme");
    this.setTheme(this.getPreferredTheme());
    this.metadataSubscription = this.dataService.metadata.subscribe(
      (metadata) => {
        this.metadata = metadata;
        let favicon: HTMLLinkElement | null =
          document.querySelector("#favicon");
        if (favicon) {
          favicon.href = metadata.eventLogoUrl;
        }
      },
    );
    this.pagesSubscription = this.dataService.pages.subscribe((pages) => {
      this.pages = pages;
    });
    this.loggedInPlayerSubscription = this.dataService
      .getLoggedInPlayer()
      .subscribe((loggedinPlayer) => {
        this.currentPlayer = loggedinPlayer;
      });
    this.areTeamsEnabledSubscription = this.dataService
      .areTeamsEnabled()
      .subscribe((areTeamsEnabled) => {
        this.areTeamsEnabled = areTeamsEnabled;
      });
  }

  ngOnDestroy() {
    this.metadataSubscription?.unsubscribe();
    this.pagesSubscription?.unsubscribe();
    this.loggedInPlayerSubscription?.unsubscribe();
    this.areTeamsEnabledSubscription?.unsubscribe();
  }

  getPreferredTheme() {
    if (this.theme) {
      return this.theme;
    }
    return window.matchMedia("(prefers-color-scheme: dark)").matches
      ? "dark"
      : "light";
  }

  setTheme(theme: string) {
    if (
      theme === "auto" &&
      window.matchMedia("(prefers-color-scheme: dark)").matches
    ) {
      document.documentElement.setAttribute("data-bs-theme", "dark");
    } else {
      document.documentElement.setAttribute("data-bs-theme", theme);
    }

    localStorage.setItem("theme", theme);
    this.theme = theme;
  }
}
