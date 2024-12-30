import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { webSocket, WebSocketSubject } from 'rxjs/webSocket';
import { BehaviorSubject, Observable, combineLatest, distinctUntilChanged, filter, map, mergeMap, retry, share, tap, timer } from 'rxjs';
import { Challenge, CurrentPlayer, Instance, Metadata, Page, Player, Solve, Team, WebSocketMessage } from '../api-model';
import { LoginResponse, OidcSecurityService } from 'angular-auth-oidc-client';
import { ChallengeDetail, ChallengeDetailCategory, PlayerDetail, SolveDetail, TeamDetail } from '../model';

@Injectable({
  providedIn: 'root'
})
export class DataService {

  private _webSocket: WebSocketSubject<WebSocketMessage<any>> | null = null;
  private _challenges = new BehaviorSubject<readonly Challenge[]>(Object.freeze([]));
  private _players = new BehaviorSubject<readonly Player[]>(Object.freeze([]));
  private _currentPlayer = new BehaviorSubject<CurrentPlayer | null>(null);
  private _currentTeamJoinToken = new BehaviorSubject<string | null>(null);
  private _currentPlayerId = new BehaviorSubject<string | null>(null);
  private _teams = new BehaviorSubject<readonly Team[]>(Object.freeze([]));
  private _solves = new BehaviorSubject<readonly Solve[]>(Object.freeze([]));
  private _pages = new BehaviorSubject<readonly Page[]>(Object.freeze([]));
  private _metadata= new BehaviorSubject<Metadata>(new Metadata());
  private _instance = new BehaviorSubject<Instance>(new Instance());
  private _pingSent = false;
  private _lastCounterSent = 0;
  private _lastCounterReceived = 0;
  private _secondTimer = timer(0, 1000).pipe(share());

  public readonly challenges: Observable<readonly Challenge[]> = this._challenges.asObservable();
  public readonly players: Observable<readonly Player[]> = this._players.asObservable();
  public readonly pages: Observable<readonly Page[]> = this._pages.asObservable();
  public readonly currentPlayer: Observable<CurrentPlayer | null> = this._currentPlayer.asObservable();
  public readonly currentPlayerId: Observable<string | null> = this._currentPlayerId.asObservable();
  public readonly currentTeamJoinToken: Observable<string | null> = this._currentTeamJoinToken.asObservable().pipe(distinctUntilChanged());
  public readonly teams: Observable<readonly Team[]> = this._teams.asObservable();
  public readonly solves: Observable<readonly Solve[]> = this._solves.asObservable();
  public readonly instance: Observable<Instance> = this._instance.asObservable();
  public readonly metadata: Observable<Metadata> = this._metadata.asObservable();
  public readonly loginEvents: Observable<LoginResponse>;
  public isDisconnected = false;
  public isAuthenticated = false;

  constructor(
    private apiService: ApiService,
    private oidcSecurityService: OidcSecurityService) {
    this.loginEvents = this.oidcSecurityService.checkAuth().pipe(share());
    this.loginEvents.subscribe((loginResponse: LoginResponse) => {
        const { isAuthenticated, userData, accessToken, idToken, configId } =
          loginResponse;

        if(isAuthenticated) {
          this._currentPlayerId.next(userData["sub"]);
          this.refreshCurrentPlayer();
        } else {
          this._currentPlayerId.next(null);
        }

        this.isAuthenticated = isAuthenticated;
    });

    timer(10, 5000).subscribe(counter => {
      if (this._pingSent && this._lastCounterSent != this._lastCounterReceived) {
        this.isDisconnected = true;
      } else {
        this.isDisconnected = false;
      }
      this._lastCounterSent = counter;
      let message: WebSocketMessage<number> = {
        type: "ping",
        message: this._lastCounterSent
      };
      this._pingSent = true;
      this._webSocket?.next(message);
    });

    this.getCurrentTeamDetail().pipe(distinctUntilChanged((prev, curr) => {
      return curr?.name === prev?.name;
    })).subscribe(currentTeam => {
      if (currentTeam) {
        this.refreshCurrentTeamJoinToken();
      } else {
        this._currentTeamJoinToken.next(null);
      }
    });
  }

  refreshAllData() {
    this.apiService.getMetadata().subscribe(metadata => {
      this._metadata.next(Object.freeze(metadata));
    });
    this.apiService.getPages().subscribe(pages => {
      this._pages.next(Object.freeze(pages.sort((a,b) => a.index - b.index)));
    });
    this.apiService.getPlayers().subscribe(players => {
      this._players.next(Object.freeze(players));
    });
    this.apiService.getChallenges().subscribe(challenges => {
      this._challenges.next(Object.freeze(challenges));
    });
    this.apiService.getTeams().subscribe(teams => {
      this._teams.next(Object.freeze(teams));
    });
    this.apiService.getSolves().subscribe(solves => {
      this._solves.next(Object.freeze(solves));
    });
    if (this.isAuthenticated) {
      this.apiService.getInstance().subscribe(instance => {
        this._instance.next(Object.freeze(instance));
      });
    }
  }

  refreshWebSocket(accessToken: string | null) {
    this._webSocket?.complete();
    let protocol = window.location.protocol == 'https:' ? 'wss' : 'ws';
    let query = accessToken == null ? '' : ('?access_token=' + encodeURIComponent(accessToken));
    this._webSocket = webSocket<WebSocketMessage<any>>({
      url: protocol + '://' + window.location.host + '/api/v2/events' + query,
      openObserver: {
        next: () => {
          this.refreshAllData();
        }
      }
    });
    this._webSocket.pipe(retry()).subscribe(message => {
      switch (message.type) {
        case "pong":
          this._lastCounterReceived = message.message as number;
          break;
        case "solve":
          let solve = message.message as Solve;
          let solves = this._solves.getValue();
          let modifiedSolves = solves.filter(_ => true);
          modifiedSolves.push(solve);
          this._solves.next(Object.freeze(modifiedSolves));
          break;
        case "team":
          let team = message.message as Team;
          let teams = this._teams.getValue();
          let modifiedTeams = teams.filter(t => t.id != team.id)
          modifiedTeams.push(team);
          this._teams.next(Object.freeze(modifiedTeams));
          break;
        case "player":
          let player = message.message as Player;
          let players = this._players.getValue();
          let modifiedPlayers = players.filter(t => t.id != player.id)
          modifiedPlayers.push(player);
          this._players.next(Object.freeze(modifiedPlayers));
          break;
        case "challenge":
          let challenge = message.message as Challenge;
          let challenges = this._challenges.getValue();
          let modifiedChallenges = challenges.filter(t => t.name != challenge.name)
          modifiedChallenges.push(challenge);
          this._challenges.next(Object.freeze(modifiedChallenges));
          break;
        case "page":
          let page = message.message as Page;
          let pages = this._pages.getValue();
          let modifiedPages = pages.filter(p => p.path != page.path);
          modifiedPages.push(page);
          modifiedPages.sort((a,b) => a.index - b.index);
          this._pages.next(Object.freeze(modifiedPages));
          break;
        case "instance":
          let instance = message.message as Instance;
          this._instance.next(Object.freeze(instance));
          break;
        case "metadata":
          let metadata = message.message as Metadata;
          this._metadata.next(Object.freeze(metadata));
          break;
        case "current-player":
          let playerId = message.message as string | null;
          var currentPlayerId: string | null = this._currentPlayerId.getValue();
          if (playerId != currentPlayerId) {
            this.oidcSecurityService.getAccessToken().subscribe(accessToken => {
              this.refreshWebSocket(accessToken);
            });
          }
          break;
        default:
          console.warn("Unknown websocket message type: " + message.type);
          break;
      }
    });
  }

  refreshMetadata(): Observable<Metadata> {
    return this.apiService.getMetadata().pipe(tap(metadata => {
      this._metadata.next(Object.freeze(metadata));
    }));
  }

  refreshPages(): Observable<Page[]> {
    return this.apiService.getPages().pipe(tap(pages => {
      this._pages.next(Object.freeze(pages.sort((a,b) => a.index - b.index)));
    }));
  }

  refreshCurrentPlayer() {
    this.apiService.getCurrentPlayer().subscribe(player => {
      this._currentPlayer.next(Object.freeze(player));
    });
  }

  refreshCurrentTeamJoinToken() {
    this.apiService.getCurrentTeam().subscribe(currentTeam => {
      this._currentTeamJoinToken.next(currentTeam.joinToken);
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
    this._currentPlayerId.next(null);
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

  joinTeam(joinToken: string) {
    return this.apiService.joinTeam(joinToken).pipe(tap(currentTeam => {
      this._currentTeamJoinToken.next(currentTeam.joinToken);
    }));
  }

  createTeam(name: string) {
    return this.apiService.createTeam(name);
  }

  leaveTeam() {
    return this.apiService.leaveCurrentTeam();
  }

  hasCTFStarted(): Observable<boolean> {
    return combineLatest([this._secondTimer, this.metadata]).pipe(map(params => {
      const [_, metadata] = params;
      let now = new Date();
      let start = new Date(metadata.start);
      return start <= now;
    }), distinctUntilChanged());
  }

  hasCTFEnded(): Observable<boolean> {
    return combineLatest([this._secondTimer, this.metadata]).pipe(map(params => {
      const [_, metadata] = params;
      let now = new Date();
      let end = new Date(metadata.end);
      return end <= now;
    }), distinctUntilChanged());
  }

  hasFreezeStarted(): Observable<boolean> {
    return combineLatest([this._secondTimer, this.metadata]).pipe(map(params => {
      const [_, metadata] = params;
      let now = new Date();
      if (metadata.freezeStart) {
        let freezeStart = new Date(metadata.freezeStart);
        return freezeStart <= now;
      }
      return false;
    }), distinctUntilChanged());
  }

  hasFreezeEnded(): Observable<boolean> {
    return combineLatest([this._secondTimer, this.metadata]).pipe(map(params => {
      const [_, metadata] = params;
      let now = new Date();
      if (metadata.freezeEnd) {
        let freezeEnd = new Date(metadata.freezeEnd);
        return freezeEnd <= now;
      }
      return false;
    }), distinctUntilChanged());
  }

  getCTFStart(): Observable<Date> {
    return this.metadata.pipe(map(m => new Date(m.start)), distinctUntilChanged());
  }

  getCTFEnd(): Observable<Date> {
    return this.metadata.pipe(map(m => new Date(m.end)), distinctUntilChanged());
  }

  areTeamsEnabled(): Observable<boolean> {
    return this.metadata.pipe(map(m => m.teams), distinctUntilChanged());
  }

  getPlayerName(id: string): Observable<string> {
    return this.players.pipe(mergeMap(p => p), filter(p => p.id == id), map(p => p.name), distinctUntilChanged());
  }

  getLoggedInPlayer(): Observable<Player | null> {
    return combineLatest([this.currentPlayerId, this.players]).pipe(map(params => {
      const [currentPlayerId, players] = params;
      if (currentPlayerId) {
        return players.find(p => p.id == currentPlayerId) || null;
      }
      return null;
    }));
  }

  getPrimaryChallengeCategories(): Observable<readonly string[]> {
    return this.challenges.pipe(map(c => Object.freeze(this.getPrimaryCategories(c))));
  }

  getChallengeDifficulties(): Observable<readonly string[]> {
    return this.challenges.pipe(map(challenges => {
      return Object.freeze([...new Set(challenges.flatMap(c => c.difficulty))].sort());
    }));
  }

  getChallengeDetailsByCategory(): Observable<readonly ChallengeDetailCategory[]> {
    return combineLatest([this.currentPlayerId, this.players, this.teams, this.solves, this.challenges]).pipe(map((params) => {
      const [ currentPlayerId, players, teams, solves, challenges ] = params;
      let categories = this.getPrimaryCategories(challenges);
      let result: ChallengeDetailCategory[] = [];
      for (let category of categories) {
        let challs = this.getChallengesByCategory(category, challenges).map(c => this.toChallengeDetail(c, currentPlayerId, solves, players, teams));
        let challengeDetailCategory = new ChallengeDetailCategory();
        challengeDetailCategory.category = category;
        challengeDetailCategory.challenges = challs;
        challengeDetailCategory.numTotal = challs.length;
        challengeDetailCategory.numSolved = challs.filter(c => c.solvedByPlayer || c.solvedByTeam).length;
        result.push(challengeDetailCategory);
      }
      return Object.freeze(result);
    }));
  }

  getChallengeDetail(name: Observable<string | null>): Observable<ChallengeDetail | null> {
    return combineLatest([name, this.currentPlayerId, this.solves, this.players, this.teams, this.challenges]).pipe(map((params) => {
      const [ name, currentPlayerId, solves, players, teams, challenges ] = params;
      if (name == null)
        return null;
      var challenge = challenges.find(c => c.name == name) || null;
      if (challenge == null)
        return null;
      return this.toChallengeDetail(challenge, currentPlayerId, solves, players, teams);
    }));
  }

  getCurrentTeamDetail(): Observable<TeamDetail | null> {
    let currentTeamId = combineLatest([this.currentPlayerId, this.teams]).pipe(map((params) => {
      const [ currentPlayerId, teams ] = params;
      if (currentPlayerId == null)
        return null;
      var team = teams.find(t => t.players.includes(currentPlayerId)) || null;
      if (team == null)
        return null;
      return team.id;
    }));
    return this.getTeamDetail(currentTeamId);
  }

  getTeamDetail(teamId: Observable<string | null>): Observable<TeamDetail | null> {
    return combineLatest([teamId, this.teams, this.players, this.solves, this.challenges]).pipe(map((params) => {
      const [ teamId, teams, players, solves, challenges ] = params;
      if (teamId == null)
        return null;
      var team = teams.find(t => t.id == teamId) || null;
      if (team == null)
        return null;
      var teamDetail = new TeamDetail();
      teamDetail.id = team.id;
      teamDetail.name = team.name;
      teamDetail.players = players.filter(p => team!.players.includes(p.id));
      teamDetail.solves = solves.filter(s => team!.players.includes(s.playerId)).map(s => this.toSolveDetail(s, players, teams));
      let categories = this.getPrimaryCategories(challenges);
      for (let category of categories) {
        teamDetail.categoryProgress.set(category, this.getCategoryProgress(category, challenges, teamDetail.solves));
      }
      return Object.freeze(teamDetail);
    }));
  }

  getPlayerDetail(playerId: Observable<string | null>): Observable<PlayerDetail | null> {
    return combineLatest([playerId, this.teams, this.players, this.solves, this.challenges]).pipe(map((params) => {
      const [ playerId, teams, players, solves, challenges ] = params;
      if (playerId == null)
        return null;
      var player = players.find(t => t.id == playerId) || null;
      if (player == null)
        return null;
      var playerDetail = new PlayerDetail();
      playerDetail.id = player.id;
      playerDetail.name = player.name;
      playerDetail.team = teams.find(t => t.players.includes(playerId)) || null;
      playerDetail.solves = solves.filter(s => s.playerId == playerId).map(s => this.toSolveDetail(s, players, teams));
      let categories = this.getPrimaryCategories(challenges);
      for (let category of categories) {
        playerDetail.categoryProgress.set(category, this.getCategoryProgress(category, challenges, playerDetail.solves));
      }
      return Object.freeze(playerDetail);
    }));
  }

  private toChallengeDetail(challenge: Challenge, currentPlayerId: string | null, solves: readonly Solve[], players: readonly Player[], teams: readonly Team[]) {
    var challDetail = new ChallengeDetail();
    challDetail.challenge = challenge;
    challDetail.solves = solves.filter(s => s.challengeName == challenge.name).map(s => this.toSolveDetail(s, players, teams));
    if (currentPlayerId) {
      challDetail.solvedByPlayer = solves.find(s => s.challengeName == challenge.name && s.playerId == currentPlayerId) != undefined;
      var team = teams.find(t => t.players.includes(currentPlayerId)) || null;
      if (team == null) {
        challDetail.solvedByTeam = false;
      }
      else {
        challDetail.solvedByTeam = challDetail.solves.find(s => team!.players.includes(s.playerId)) != undefined;
      }
    }
    return Object.freeze(challDetail);
  }

  private toSolveDetail(solve: Solve, players: readonly Player[], teams: readonly Team[]) {
    var solveDetail = new SolveDetail();
    solveDetail.challengeName = solve.challengeName;
    solveDetail.playerId = solve.playerId;
    solveDetail.solvedAt = solve.solvedAt;
    solveDetail.playerName = players.find(p => p.id == solve.playerId)?.name ?? '';
    var team = teams.find(t => t.players.includes(solve.playerId)) || null;
    solveDetail.teamId = team?.id || null;
    solveDetail.teamName = team?.name || null;
    return Object.freeze(solveDetail);
  }

  private getChallengesByCategory(category: string, challenges: readonly Challenge[]): Challenge[] {
    return challenges.filter(c => c.categories.length == 0 ? ('uncategorized' == category) : (c.categories[0] == category));
  }

  private getCategoryProgress(category: string, challenges: readonly Challenge[], solves: Solve[]): number {
    let categoryChallenges = this.getChallengesByCategory(category, challenges);
    let solvedChallenges = categoryChallenges.filter(challenge => solves.some(solve => solve.challengeName === challenge.name));
    return Math.floor((solvedChallenges.length / categoryChallenges.length) * 100);
  }

  private getPrimaryCategories(challenges: readonly Challenge[]) {
    return [...new Set(challenges.map(c => c.categories.length == 0 ? 'uncategorized' : c.categories[0]))].sort();
  }
}
