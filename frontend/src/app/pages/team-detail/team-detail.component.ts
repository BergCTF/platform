import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DataService } from 'src/app/services/data.service';
import { map, Subscription } from 'rxjs';
import { HelperService } from 'src/app/services/helper.service';
import { PrettyDateComponent } from 'src/app/widgets/pretty-date/pretty-date.component';
import { KeyValuePipe } from '@angular/common';
import { TeamDetail } from 'src/app/model';

@Component({
  selector: 'app-team',
  templateUrl: './team-detail.component.html',
  styleUrls: ['./team-detail.component.less'],
  imports: [RouterLink, PrettyDateComponent, KeyValuePipe]
})
export class TeamDetailComponent implements OnInit, OnDestroy {

  private teamDetailSubscription: Subscription | null = null;
  private areTeamsEnabledSubscription: Subscription | null = null;

  teamDetail: TeamDetail | null = null;
  areTeamsEnabled = false;

  constructor(
    public dataService: DataService,
    public helper: HelperService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    let teamId = this.route.params.pipe(map(params => params['uuid']));
    this.teamDetailSubscription = this.dataService.getTeamDetail(teamId).subscribe(teamDetail => {
      this.teamDetail = teamDetail;
    });
    this.areTeamsEnabledSubscription = this.dataService.areTeamsEnabled().subscribe(areTeamsEnabled => {
      this.areTeamsEnabled = areTeamsEnabled;
    });
  }

  ngOnDestroy(): void {
    this.teamDetailSubscription?.unsubscribe();
    this.areTeamsEnabledSubscription?.unsubscribe();
  }
}