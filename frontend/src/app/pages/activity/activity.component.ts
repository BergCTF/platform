import { SlicePipe } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgbPagination, NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { Subscription } from 'rxjs';
import { ActivityEntry } from 'src/app/model';
import { DataService } from 'src/app/services/data.service';
import { HelperService } from 'src/app/services/helper.service';
import { PrettyDateComponent } from 'src/app/widgets/pretty-date/pretty-date.component';

@Component({
  selector: 'app-activity',
  templateUrl: './activity.component.html',
  styleUrl: './activity.component.less',
  imports: [NgbTooltip, NgbPagination, RouterLink, SlicePipe, PrettyDateComponent],
})
export class ActivityComponent implements OnInit, OnDestroy {

  private areTeamsEnabledSubscription: Subscription | null = null;
  private activityEntriesSubscription: Subscription | null = null;
  public areTeamsEnabled = false;
  public pageIndex = 1;
  public pageSize = 30;
  public activityEntries: readonly ActivityEntry[] = [];

  constructor(
    private dataService: DataService,
    public helper: HelperService
  ) {}

  ngOnInit(): void {
    this.areTeamsEnabledSubscription = this.dataService.areTeamsEnabled().subscribe(areTeamsEnabled => {
      this.areTeamsEnabled = areTeamsEnabled;
    });
    this.activityEntriesSubscription = this.dataService.getActivityEntries().subscribe(activityEntries => {
      this.activityEntries = activityEntries;
    });
  }

  ngOnDestroy(): void {
    this.areTeamsEnabledSubscription?.unsubscribe();
    this.activityEntriesSubscription?.unsubscribe();
  }

  pageSizeChanged(pageSizeValue: string) {
    this.pageSize = parseInt(pageSizeValue);
  }
}
