import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { BehaviorSubject, Observable, filter, map, mergeMap } from 'rxjs';
import { Challenge, CurrentPlayer, Instance, Metadata, Page, Player, Solve, Team } from '../model';
import { LoginResponse, OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({
  providedIn: 'root'
})
export class DataService {

  private _challenges = new BehaviorSubject<Challenge[]>([]);
  private _players = new BehaviorSubject<Player[]>([]);
  private _currentPlayer = new BehaviorSubject<CurrentPlayer>(new CurrentPlayer());
  private _teams = new BehaviorSubject<Team[]>([]);
  private _solves = new BehaviorSubject<Solve[]>([]);
  private _pages = new BehaviorSubject<Page[]>([]);
  private _metadata= new BehaviorSubject<Metadata>(new Metadata());
  private _instance = new BehaviorSubject<Instance>(new Instance());

  public readonly challenges: Observable<Challenge[]> = this._challenges.asObservable();
  public readonly players: Observable<Player[]> = this._players.asObservable();
  public readonly pages: Observable<Page[]> = this._pages.asObservable();
  public readonly currentPlayer: Observable<CurrentPlayer> = this._currentPlayer.asObservable();
  public readonly teams: Observable<Team[]> = this._teams.asObservable();
  public readonly solves: Observable<Solve[]> = this._solves.asObservable();
  public readonly instance: Observable<Instance> = this._instance.asObservable();
  public readonly metadata: Observable<Metadata> = this._metadata.asObservable();
  public isAuthenticated = false;
  public currentPlayerId = '00000000-0000-0000-0000-000000000000';

  constructor(private apiService: ApiService, private oidcSecurityService: OidcSecurityService) {
    this.apiService.getMetadata().subscribe(metadata => {
      this._metadata.next(metadata);
    });

    let checkAuthSubscription = this.oidcSecurityService.checkAuth();
    checkAuthSubscription.subscribe((loginResponse: LoginResponse) => {
        const { isAuthenticated, userData, accessToken, idToken, configId } =
          loginResponse;

        if(isAuthenticated) {
          this.currentPlayerId = userData["sub"];
          this.apiService.getCurrentPlayer().subscribe(player => {
            this._currentPlayer.next(player);
          });
        } else {
          this.currentPlayerId = '00000000-0000-0000-0000-000000000000';
        }

        this.isAuthenticated = isAuthenticated;
        if (!this._metadata.getValue().allowAnonymousAccess && !isAuthenticated) {
          this.oidcSecurityService.authorize();
        }
        this.refreshAllData();
    });

    setInterval(() => {
      if (!this.isAuthenticated)
        return;
      this.apiService.getInstance().subscribe(instance => {
        this._instance.next(instance);
      });
    }, 3000);
  }

  private refreshAllData() {
    this.apiService.getMetadata().subscribe(metadata => {
      this._metadata.next(metadata);
    });
    this.apiService.getPages().subscribe(pages => {
      this._pages.next(pages.sort((a,b) => a.index - b.index));
    });
    this.apiService.getPlayers().subscribe(players => {
      this._players.next(players);
    });
    this.apiService.getChallenges().subscribe(challenges => {
      this._challenges.next(challenges);
    });
    this.apiService.getTeams().subscribe(teams => {
      this._teams.next(teams);
    });
    this.apiService.getSolves().subscribe(solves => {
      this._solves.next(solves);
    });
    this.apiService.getInstance().subscribe(instance => {
      this._instance.next(instance);
    });
  }

  login() {
    this.oidcSecurityService.authorize();
  }

  logout() {
    this.oidcSecurityService.logoff().subscribe((_) => {});
  }

  addSolve(challengeName: string, flag: string) {
    return this.apiService.addSolve(challengeName, flag);
  }

  startInstance(challengeName: string) {
    this.apiService.startInstance(challengeName).subscribe(instance => {
      this._instance.next(instance);
    });
  }

  stopInstance() {
    this.apiService.stopInstance().subscribe(instance => {
      this._instance.next(instance);
    });
  }

  getPlayerName(id: string): Observable<string> {
    return this.players.pipe(mergeMap(p => p), filter(p => p.id == id), map(p => p.name));
  }

  ensurePlayer(id: string) {
    let player = this._players.getValue().find(p => p.id == id);
    if (player === undefined) {
      this.apiService.getPlayer(id).subscribe(player => {
        let players = this._players.getValue();
        if (players.find(p => p.id == id))
          return;
        players.push(player);
        this._players.next(players);
      });
    }
  }

  ensureChallenge(name: string) {
    let challenge = this._challenges.getValue().find(c => c.name == name);
    if (challenge === undefined) {
      this.apiService.getChallenge(name).subscribe(challenge => {
        let challenges = this._challenges.getValue();
        if (challenges.find(c => c.name == name))
          return;
        challenges.push(challenge);
        this._challenges.next(challenges);
      });
    }
  }

}
