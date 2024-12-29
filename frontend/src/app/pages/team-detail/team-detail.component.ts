import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Challenge, Metadata, Player, Solve, Team } from 'src/app/model';
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
  private challengesSubscription: Subscription | null = null;
  private teamsSubscription: Subscription | null = null;
  private playersSubscription: Subscription | null = null;
  private solvesSubscription: Subscription | null = null;
  private routeParamsSubscription: Subscription | null = null;
  private _solves: Solve[] = [];
  private _teams: Team[] = [];
  private _players: Player[] = [];
  private _challenges: Challenge[] = [];

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
    this.challengesSubscription = this.dataService.challenges.subscribe(challenges => {
      this._challenges = challenges;
    });
    this.solvesSubscription = this.dataService.solves.subscribe(solves => {
      this._solves = solves;
      this.handleChange();
    })
    this.teamsSubscription = this.dataService.teams.subscribe(teams => {
      this._teams = teams;
      this.handleChange();
    })
    this.playersSubscription = this.dataService.players.subscribe(players => {
      this._players = players;
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
    let team = this._teams.find(t => t.id == this.uuid) || null;
    this.team = team;
    if (team != null) {
      this.teamMembers = this._players.filter(p => team.players.includes(p.id));
      let solves = this._solves.filter(s => team.players.includes(s.playerId));
      solves.sort((a, b) => new Date(b.solvedAt).getTime() - new Date(a.solvedAt).getTime());
      this.teamSolves = solves;
    } else {
      this.teamMembers = [];
      this.teamSolves = [];
    }
  }

  getPrimaryCategories() {
    return [...new Set(this._challenges.map(c => c.categories.length == 0 ? 'uncategorized' : c.categories[0]))].sort();
  }

  getCategoryProgress(category: string) {
    let categoryChallenges = this._challenges.filter(c => c.categories.includes(category));
    let teamSolves = this.teamSolves;
    if (teamSolves) {
      let solvedChallenges = categoryChallenges.filter(challenge => teamSolves.some(solve => solve.challengeName === challenge.name));
      return Math.floor((solvedChallenges.length / categoryChallenges.length) * 100);
    } else {
      return 0;
    }
  }

  getBarColor(percentage: number) {
    const minHue = 240;
    const maxHue = 120;
    percentage /= 100;
    const colorString = `hsl(${percentage * (maxHue - minHue) + minHue},60%,${40 + 10 * percentage}%)`;
    return colorString;
  }
}