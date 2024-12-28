import { Component, OnDestroy, OnInit } from '@angular/core';
import { AsyncPipe, Location } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { Challenge, Instance, Metadata, ProblemDetails, Solve, Team } from 'src/app/model';
import { DataService } from 'src/app/services/data.service';
import { ChallengeStatusComponent } from 'src/app/widgets/challenge-status/challenge-status.component';
import { PrettyDateComponent } from 'src/app/widgets/pretty-date/pretty-date.component';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-challenge-detail',
  imports: [ChallengeStatusComponent, PrettyDateComponent, RouterLink, AsyncPipe],
  templateUrl: './challenge-detail.component.html',
  styleUrl: './challenge-detail.component.less'
})
export class ChallengeDetailComponent implements OnInit, OnDestroy {

  challenge: Challenge | null = null;
  instance: Instance | null = null;
  solves: Solve[] = [];
  isFlagSubmitting = false;
  isSolvedByPlayer = false;
  isSolvedByTeam = false;
  flagErrorText: string | null = null;

  private _metadata = new Metadata();
  private _solves: Solve[] = [];
  private _challengeName = '';
  private _challenges: Challenge[] = [];
  private _teams: Team[] = [];
  private routeParamsSubscription: Subscription | null = null;
  private instanceSubscription: Subscription | null = null;
  private metadataSubscription: Subscription | null = null;
  private challengesSubscription: Subscription | null = null;
  private teamsSubscription: Subscription | null = null;
  private solvesSubscription: Subscription | null = null;

  constructor(
    public dataService: DataService,
    public location: Location,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.routeParamsSubscription = this.route.params.subscribe(params => {
      this._challengeName = params['name'];
      this.handleUpdate();
    });
    this.instanceSubscription = this.dataService.instance.subscribe(instance => {
      this.instance = instance;
    });
    this.metadataSubscription = this.dataService.metadata.subscribe(metadata => {
      this._metadata = metadata;
    });
    this.challengesSubscription = this.dataService.challenges.subscribe(challenges => {
      this._challenges = challenges;
      this.handleUpdate();
    });
    this.teamsSubscription = this.dataService.teams.subscribe(teams => {
      this._teams = teams;
      this.handleUpdate();
    });
    this.solvesSubscription = this.dataService.solves.subscribe(solves => {
      this._solves = solves;
      this.handleUpdate();
    });
  }

  ngOnDestroy(): void {
    this.routeParamsSubscription?.unsubscribe();
    this.instanceSubscription?.unsubscribe();
    this.metadataSubscription?.unsubscribe();
    this.challengesSubscription?.unsubscribe();
    this.teamsSubscription?.unsubscribe();
    this.solvesSubscription?.unsubscribe();
  }

  submitFlag(flag: string) {
    this.isFlagSubmitting = true;
    this.dataService.addSolve(this._challengeName, flag).subscribe({
      next: _ => {
        this.flagErrorText = null;
      },
      error: err => {
        let httpError = err as HttpErrorResponse;
        let problemDetails = httpError.error as ProblemDetails;
        this.flagErrorText = problemDetails.detail;
      }
    }).add(() => {
      this.isFlagSubmitting = false;
    });
  }

  getStart(): Date {
    return new Date(this._metadata.start);
  }

  getEnd(): Date {
    return new Date(this._metadata.end);
  }

  now(): Date {
    return new Date();
  }

  private handleUpdate() {
    let chall = this._challenges.find(c => c.name == this._challengeName) || null;
    if (chall != null) {
      this.challenge = chall;
    }
    this.solves = this._solves.filter(s => s.challengeName == this._challengeName);

    this.isSolvedByPlayer = this._solves.find(s => s.challengeName == this._challengeName && s.playerId == this.dataService.currentPlayerId) != undefined;

    let team = this._teams.find(t => t.players.includes(this.dataService.currentPlayerId));
    if (team) {
      this.isSolvedByTeam = this._solves.find(s => s.challengeName == this._challengeName && team.players.includes(s.playerId)) != undefined;
    } else {
      this.isSolvedByTeam = false;
    }
  }
}
