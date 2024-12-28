import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { Metadata, Player, ProblemDetails, Team } from 'src/app/model';
import { DataService } from 'src/app/services/data.service';

@Component({
  selector: 'app-team',
  templateUrl: './team.component.html',
  styleUrl: './team.component.less',
  imports: [RouterLink, FormsModule]
})
export class TeamComponent {

  private metadataSubscription: Subscription | null = null;
  private teamsSubscription: Subscription | null = null;
  private playersSubscription: Subscription | null = null;
  private solvesSubscription: Subscription | null = null;
  private routeParamsSubscription: Subscription | null = null;
  private teams: Team[] = [];
  private players: Player[] = [];

  joinError: string | null = null;
  createError: string | null = null;
  teamJoinToken: string = '';
  joinToken: string = '';
  teamName: string = '';
  metadata: Metadata | null = null;
  team: Team | null = null;
  teamMembers: Player[] | null = null;

  constructor(
    public dataService: DataService
  ) {}

  ngOnInit(): void {
    this.metadataSubscription = this.dataService.metadata.subscribe(metadata => {
      this.metadata = metadata;
    });
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
    let team = this.teams.find(t => t.players.includes(this.dataService.currentPlayerId)) || null;
    this.team = team;
    if (team != null) {
      this.teamMembers = this.players.filter(p => team.players.includes(p.id));
      this.dataService.getCurrentTeam().subscribe(currentTeam => {
        if (currentTeam.joinToken != null) {
          this.teamJoinToken = currentTeam.joinToken;
        }
      });
    } else {
      this.teamMembers = [];
      this.teamJoinToken = '';
    }
  }

  leaveTeam() {
    this.dataService.leaveTeam().subscribe(() => {});
  }

  joinTeam() {
    this.dataService.joinTeam(this.joinToken).subscribe({
      next: () => {
        this.joinError = null;
      },
      error: (err) => {
        let httpError = err as HttpErrorResponse;
        let problemDetails = httpError.error as ProblemDetails;
        this.joinError = problemDetails.detail;
      },
    });
  }

  createTeam() {
    this.dataService.createTeam(this.teamName).subscribe({
      next: () => {
        this.createError = null;
      },
      error: (err) => {
        let httpError = err as HttpErrorResponse;
        let problemDetails = httpError.error as ProblemDetails;
        this.createError = problemDetails.detail;
      },
    });
  }
}
