import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DataService } from 'src/app/services/data.service';
import { HelperService } from 'src/app/services/helper.service';
import { PrettyDateComponent } from '../../widgets/pretty-date/pretty-date.component';
import { map, Subscription } from 'rxjs';
import { PlayerDetail } from 'src/app/model';
import { KeyValuePipe } from '@angular/common';

@Component({
    selector: 'app-player',
    templateUrl: './player-detail.component.html',
    styleUrls: ['./player-detail.component.less'],
    imports: [RouterLink, PrettyDateComponent, KeyValuePipe]
})
export class PlayerDetailComponent implements OnInit, OnDestroy {

  private playerDetailSubscription: Subscription | null = null;
  private areTeamsEnabledSubscription: Subscription | null = null;

  playerDetail: PlayerDetail | null = null;
  areTeamsEnabled = false;

  constructor(
    public dataService: DataService,
    public helper: HelperService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    let playerId = this.route.params.pipe(map(params => params['uuid']));
    this.playerDetailSubscription = this.dataService.getPlayerDetail(playerId).subscribe(playerDetail => {
      this.playerDetail = playerDetail;
    });
    this.areTeamsEnabledSubscription = this.dataService.areTeamsEnabled().subscribe(areTeamsEnabled => {
      this.areTeamsEnabled = areTeamsEnabled;
    });
  }

  ngOnDestroy(): void {
    this.playerDetailSubscription?.unsubscribe();
    this.areTeamsEnabledSubscription?.unsubscribe();
  }
}
