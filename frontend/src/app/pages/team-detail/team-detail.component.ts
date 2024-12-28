import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Metadata, Player, Solve, Team } from 'src/app/model';
import { DataService } from 'src/app/services/data.service';
import { Subscription } from 'rxjs';
import { HelperService } from 'src/app/services/helper.service';
import { PrettyDateComponent } from 'src/app/widgets/pretty-date/pretty-date.component';
import { AsyncPipe } from '@angular/common';

@Component({
  selector: 'app-team',
  templateUrl: './team-detail.component.html',
  styleUrls: ['./team-detail.component.less'],
  imports: [RouterLink, PrettyDateComponent, AsyncPipe]
})
export class TeamDetailComponent implements OnInit, OnDestroy {

  private metadataSubscription: Subscription | null = null;
  private teamsSubscription: Subscription | null = null;
  private playersSubscription: Subscription | null = null;
  private solvesSubscription: Subscription | null = null;
  private routeParamsSubscription: Subscription | null = null;
  private solves: Solve[] = [];
  private teams: Team[] = [];
  private players: Player[] = [];

  uuid = '';
  metadata: Metadata = new Metadata();
  team: Team | null = null;
  teamMembers: Player[] | null = null;
  teamSolves: Solve[] | null = null;

  constructor(
    public dataService: DataService,
    public helper: HelperService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.routeParamsSubscription = this.route.params.subscribe(params => {
      this.uuid = params['uuid'];
      this.handleChange();
    });

    this.metadataSubscription = this.dataService.metadata.subscribe(metadata => {
      this.metadata = metadata;
    });
    this.solvesSubscription = this.dataService.solves.subscribe(solves => {
      this.solves = solves;
      this.handleChange();
    })
    this.teamsSubscription = this.dataService.teams.subscribe(teams => {
      this.teams = teams;
      this.handleChange();
    })
    this.playersSubscription = this.dataService.players.subscribe(players => {
      this.players = players;
      this.handleChange();
    })
  }

  ngOnDestroy(): void {
    this.routeParamsSubscription?.unsubscribe();
    this.metadataSubscription?.unsubscribe();
    this.solvesSubscription?.unsubscribe();
    this.teamsSubscription?.unsubscribe();
    this.playersSubscription?.unsubscribe();
  }

  private handleChange() {
    let team = this.teams.find(t => t.id == this.uuid) || null;
    this.team = team;
    if (team != null) {
      this.teamMembers = this.players.filter(p => team.players.includes(p.id));
      let solves = this.solves.filter(s => team.players.includes(s.playerId));
      solves.sort((a, b) => new Date(b.solvedAt).getTime() - new Date(a.solvedAt).getTime());
      this.teamSolves = solves;
    } else {
      this.teamMembers = [];
      this.teamSolves = [];
    }
  }
}