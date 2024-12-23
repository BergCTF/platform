import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { webSocket, WebSocketSubject } from 'rxjs/webSocket';
import { BehaviorSubject, Observable, filter, map, mergeMap, share, tap } from 'rxjs';
import { Challenge, CurrentPlayer, Instance, Metadata, Page, Player, Solve, Team, WebSocketMessage } from '../model';
import { LoginResponse, OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({
  providedIn: 'root'
})
export class DataService {

  private _webSocket: WebSocketSubject<WebSocketMessage<any>> | null = null;
  private _challenges = new BehaviorSubject<Challenge[]>([]);
  private _players = new BehaviorSubject<Player[]>([]);
  private _currentPlayer = new BehaviorSubject<CurrentPlayer | null>(null);
  private _teams = new BehaviorSubject<Team[]>([]);
  private _solves = new BehaviorSubject<Solve[]>([]);
  private _pages = new BehaviorSubject<Page[]>([]);
  private _metadata= new BehaviorSubject<Metadata>(new Metadata());
  private _instance = new BehaviorSubject<Instance>(new Instance());

  public readonly challenges: Observable<Challenge[]> = this._challenges.asObservable();
  public readonly players: Observable<Player[]> = this._players.asObservable();
  public readonly pages: Observable<Page[]> = this._pages.asObservable();
  public readonly currentPlayer: Observable<CurrentPlayer | null> = this._currentPlayer.asObservable();
  public readonly teams: Observable<Team[]> = this._teams.asObservable();
  public readonly solves: Observable<Solve[]> = this._solves.asObservable();
  public readonly instance: Observable<Instance> = this._instance.asObservable();
  public readonly metadata: Observable<Metadata> = this._metadata.asObservable();
  public readonly loginEvents: Observable<LoginResponse>;
  public isAuthenticated = false;
  public currentPlayerId = '00000000-0000-0000-0000-000000000000';

  constructor(private apiService: ApiService, private oidcSecurityService: OidcSecurityService) {
    this.loginEvents = this.oidcSecurityService.checkAuth().pipe(share());
    this.loginEvents.subscribe((loginResponse: LoginResponse) => {
        const { isAuthenticated, userData, accessToken, idToken, configId } =
          loginResponse;

        if(isAuthenticated) {
          this.currentPlayerId = userData["sub"];
          this.refreshCurrentPlayer();
        } else {
          this.currentPlayerId = '00000000-0000-0000-0000-000000000000';
        }

        this.isAuthenticated = isAuthenticated;
        if (!this._metadata.getValue().allowAnonymousAccess && !isAuthenticated) {
          this.oidcSecurityService.authorize();
        }

        this.refreshWebSocket();
        this.refreshAllData();
    });

    this.refreshWebSocket();

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

  refreshWebSocket() {
    this._webSocket?.complete();
    this._webSocket = webSocket<WebSocketMessage<any>>('wss://' + window.location.host + '/api/v2/events');
    this._webSocket.subscribe(message => {
      console.debug(message);
      switch (message.type) {
        case "solve":
          let solve = message.message as Solve;
          let solves = this._solves.getValue();
          this.ensurePlayer(solve.playerId);
          solves.push(solve);
          this._solves.next(solves);
          break;
        case "team":
          let team = message.message as Team;
          let teams = this._teams.getValue();
          teams = teams.filter(t => t.id != team.id)
          teams.push(team);
          this._teams.next(teams);
          break;
        case "player":
          let player = message.message as Player;
          let players = this._players.getValue();
          players = players.filter(t => t.id != player.id)
          players.push(player);
          this._players.next(players);
          break;
        case "challenge":
          let challenge = message.message as Challenge;
          let challenges = this._challenges.getValue();
          challenges = challenges.filter(t => t.name != challenge.name)
          challenges.push(challenge);
          this._challenges.next(challenges);
          break;
        default:
          console.warn("Unknown websocket message type: " + message.type);
          break;
      }
    });
  }

  refreshMetadata(): Observable<Metadata> {
    return this.apiService.getMetadata().pipe(tap(metadata => {
      this._metadata.next(metadata);
    }));
  }

  refreshPages(): Observable<Page[]> {
    return this.apiService.getPages().pipe(tap(pages => {
      this._pages.next(pages.sort((a,b) => a.index - b.index));
    }));
  }

  refreshCurrentPlayer() {
    this.apiService.getCurrentPlayer().subscribe(player => {
      this._currentPlayer.next(player);
    });
  }

  login() {
    this.oidcSecurityService.authorize();
  }

  logout(local = false) {
    if (local) {
      this.oidcSecurityService.logoffLocal();
    } else {
      this.oidcSecurityService.logoff().subscribe((_) => {});
    }
    this._currentPlayer.next(null);
    this.currentPlayerId = '00000000-0000-0000-0000-000000000000';
    this.isAuthenticated = false;
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
