import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { webSocket, WebSocketSubject } from 'rxjs/webSocket';
import { BehaviorSubject, Observable, combineLatest, distinctUntilChanged, filter, map, mergeMap, retry, share, tap, timer } from 'rxjs';
import { Challenge, CurrentPlayer, Instance, Metadata, Page, Player, Solve, Team, WebSocketMessage } from '../api-model';
import { LoginResponse, OidcSecurityService } from 'angular-auth-oidc-client';
import { ChallengeDetail, ChallengeDetailCategory, PlayerDetail, ScoreboardChallengeByCategory as ScoreboardChallengesByCategory, ScoreboardChallengeEntry, ScoreboardRanking, SolveDetail, TeamDetail, TeamSolveDetail, ActivityEntry } from '../model';
import { HelperService } from './helper.service';

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
    private helper: HelperService,
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
          {
            this._lastCounterReceived = message.message as number;
          }
          break;
        case "solve":
          {
            let solve = message.message as Solve;
            let solves = this._solves.getValue();
            let modifiedSolves = solves.filter(_ => true);
            modifiedSolves.push(solve);
            this._solves.next(Object.freeze(modifiedSolves));
          }
          break;
        case "team":
          {
            let team = message.message as Team;
            let teams = this._teams.getValue();
            let modifiedTeams = teams.filter(t => t.id != team.id);
            modifiedTeams.push(team);
            this._teams.next(Object.freeze(modifiedTeams));
          }
          break;
        case "team-delete":
          {
            let teamId = message.message as string;
            let teams = this._teams.getValue();
            let modifiedTeams = teams.filter(t => t.id != teamId);
            this._teams.next(Object.freeze(modifiedTeams));
          }
          break;
        case "player":
          {
            let player = message.message as Player;
            let players = this._players.getValue();
            let modifiedPlayers = players.filter(t => t.id != player.id);
            modifiedPlayers.push(player);
            this._players.next(Object.freeze(modifiedPlayers));
          }
          break;
        case "player-delete":
          {
            let playerId = message.message as string;
            let players = this._players.getValue();
            let modifiedPlayers = players.filter(t => t.id != playerId);
            this._players.next(Object.freeze(modifiedPlayers));
          }
          break;
        case "challenge":
          {
            let challenge = message.message as Challenge;
            let challenges = this._challenges.getValue();
            let modifiedChallenges = challenges.filter(t => t.name != challenge.name);
            modifiedChallenges.push(challenge);
            this._challenges.next(Object.freeze(modifiedChallenges));
          }
          break;
        case "page":
          {
            let page = message.message as Page;
            let pages = this._pages.getValue();
            let modifiedPages = pages.filter(p => p.path != page.path);
            modifiedPages.push(page);
            modifiedPages.sort((a,b) => a.index - b.index);
            this._pages.next(Object.freeze(modifiedPages));
          }
          break;
        case "instance":
          {
            let instance = message.message as Instance;
            this._instance.next(Object.freeze(instance));
          }
          break;
        case "metadata":
          {
            let metadata = message.message as Metadata;
            this._metadata.next(Object.freeze(metadata));
          }
          break;
        case "current-player":
          {
            let playerId = message.message as string | null;
            var currentPlayerId: string | null = this._currentPlayerId.getValue();
            if (playerId != currentPlayerId) {
              this.oidcSecurityService.getAccessToken().subscribe(accessToken => {
                this.refreshWebSocket(accessToken);
              });
            }
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

  getSolveDetails(): Observable<readonly SolveDetail[]> {
    return combineLatest([this.solves, this.players, this.teams]).pipe(map(params => {
      const [solves, players, teams] = params;
      return solves.map(s => this.toSolveDetail(s, players, teams));
    }));
  }

  getActivityEntries(): Observable<readonly ActivityEntry[]> {
    return combineLatest([this.getSolveDetails(), this.getChallengeDetails()]).pipe(map(params => {
      const [solveDetails, challengeDetails] = params;
      var entries = solveDetails.map(solveDetail => {
        var entry = new ActivityEntry();
        entry.solve = solveDetail;
        entry.challenge = challengeDetails.find(c => c.challenge.name == solveDetail.challengeName) || null;
        return entry;
      });
      entries.sort((a, b) => b.solve.solvedAt.getTime() - a.solve.solvedAt.getTime());
      return entries;
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

  getScoreboard(): Observable<readonly ScoreboardRanking[]> {
    return combineLatest([this.metadata, this.getPlayerDetails(), this.getTeamDetails(), this.getChallengeDetails()]).pipe(map((params) => {
      const [ metadata, players, teams, challengesDetails ] = params;
      var scoreboard: ScoreboardRanking[] = [];
      var challenges = challengesDetails.map(c => c.challenge);
      var primaryCategories = this.getPrimaryCategories(challenges);
      if (metadata.teams) {
        for (let team of teams) {
          var ranking = new ScoreboardRanking();
          ranking.id = team.id;
          ranking.name = team.name;
          ranking.score = team.score;
          for (let category of primaryCategories) {
            let challs = challengesDetails.filter(c => this.getPrimaryCategory(c.challenge) == category);
            challs.sort((a, b) => this.compareChallengeDetails(a, b));
            var challengesByCategory = new ScoreboardChallengesByCategory();
            challengesByCategory.category = category;
            challengesByCategory.scoreboardChallengeEntries = challs.map(c => {
              var entry = new ScoreboardChallengeEntry();
              entry.challenge = c.challenge;
              entry.solvedAt = team.solves.find(s => s.challengeName == entry.challenge.name)?.solvedAt || null;
              entry.solved = entry.solvedAt != null;
              return entry;
            });
            challengesByCategory.numSolved = challengesByCategory.scoreboardChallengeEntries.filter(c => c.solved).length;
            challengesByCategory.numTotal = challengesByCategory.scoreboardChallengeEntries.length;
            ranking.challengesByCategory.push(challengesByCategory);
          }
          if (team.solves.length > 0) {
            ranking.lastSolveAt = team.solves.map(s => s.solvedAt).reduce((a, b) => a > b ? a : b);
          }
          scoreboard.push(ranking);
        }
      } else {
        for (let player of players) {
          var ranking = new ScoreboardRanking();
          ranking.id = player.id;
          ranking.name = player.name;
          ranking.score = player.score;
          for (let category of primaryCategories) {
            let challs = challengesDetails.filter(c => this.getPrimaryCategory(c.challenge) == category);
            challs.sort((a, b) => this.compareChallengeDetails(a, b));
            var challengesByCategory = new ScoreboardChallengesByCategory();
            challengesByCategory.category = category;
            challengesByCategory.scoreboardChallengeEntries = challs.map(c => {
              var entry = new ScoreboardChallengeEntry();
              entry.challenge = c.challenge;
              entry.solvedAt = player.solves.find(s => s.challengeName == entry.challenge.name)?.solvedAt || null;
              entry.solved = entry.solvedAt != null;
              return entry;
            });
            challengesByCategory.numSolved = challengesByCategory.scoreboardChallengeEntries.filter(c => c.solved).length;
            challengesByCategory.numTotal = challengesByCategory.scoreboardChallengeEntries.length;
            ranking.challengesByCategory.push(challengesByCategory);
          }
          if (player.solves.length > 0) {
            ranking.lastSolveAt = player.solves.map(c => c.solvedAt).reduce((a, b) => a > b ? a : b);
          }
          scoreboard.push(ranking);
        }
      }
      scoreboard.sort((a, b) => this.compareScoreboardRanking(a, b));
      var i = 1;
      for (let ranking of scoreboard) {
        ranking.rank = i;
        i += 1;
      }
      return Object.freeze(scoreboard);
    }));
  }

  getChallengeDetailsByCategory(): Observable<readonly ChallengeDetailCategory[]> {
    return combineLatest([this.currentPlayerId, this.metadata, this.players, this.teams, this.solves, this.challenges]).pipe(map((params) => {
      const [ currentPlayerId, metadata, players, teams, solves, challenges ] = params;
      let categories = this.getPrimaryCategories(challenges);
      let result: ChallengeDetailCategory[] = [];
      for (let category of categories) {
        let challs = challenges.filter(c => this.getPrimaryCategory(c) == category).map(c => this.toChallengeDetail(metadata, c, currentPlayerId, solves, players, teams));
        challs.sort((a, b) => this.compareChallengeDetails(a, b));
        let challengeDetailCategory = new ChallengeDetailCategory();
        challengeDetailCategory.category = category;
        challengeDetailCategory.challenges = challs;
        challengeDetailCategory.numTotal = challs.length;
        challengeDetailCategory.numSolved = challs.filter(c => c.solvedByPlayer || (metadata.teams && c.solvedByTeam)).length;
        result.push(challengeDetailCategory);
      }
      return Object.freeze(result);
    }));
  }

  getChallengeDetails(): Observable<readonly ChallengeDetail[]> {
    return combineLatest([this.currentPlayerId, this.metadata, this.solves, this.players, this.teams, this.challenges]).pipe(map((params) => {
      const [ currentPlayerId, metadata, solves, players, teams, challenges ] = params;
      return Object.freeze(challenges.map(c => this.toChallengeDetail(metadata, c, currentPlayerId, solves, players, teams)));
    }));
  }

  getChallengeDetail(name: Observable<string | null>): Observable<ChallengeDetail | null> {
    return combineLatest([name, this.currentPlayerId, this.metadata, this.solves, this.players, this.teams, this.challenges]).pipe(map((params) => {
      const [ name, currentPlayerId, metadata, solves, players, teams, challenges ] = params;
      if (name == null)
        return null;
      var challenge = challenges.find(c => c.name == name) || null;
      if (challenge == null)
        return null;
      return this.toChallengeDetail(metadata, challenge, currentPlayerId, solves, players, teams);
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

  getTeamDetails(): Observable<readonly TeamDetail[]> {
    return combineLatest([this.teams, this.players, this.solves, this.getChallengeDetails()]).pipe(map((params) => {
      const [ teams, players, solves, challenges ] = params;
      return Object.freeze(teams.map(t => this.toTeamDetail(t, players, solves, challenges)));
    }));
  }

  getTeamDetail(teamId: Observable<string | null>): Observable<TeamDetail | null> {
    return combineLatest([teamId, this.teams, this.players, this.solves, this.getChallengeDetails()]).pipe(map((params) => {
      const [ teamId, teams, players, solves, challenges ] = params;
      if (teamId == null)
        return null;
      var team = teams.find(t => t.id == teamId) || null;
      if (team == null)
        return null;
      return this.toTeamDetail(team, players, solves, challenges);
    }));
  }

  getPlayerDetails(): Observable<readonly PlayerDetail[]> {
    return combineLatest([this.teams, this.players, this.solves, this.getChallengeDetails()]).pipe(map((params) => {
      const [ teams, players, solves, challenges ] = params;
      return players.map(p => this.toPlayerDetail(p, players, teams, solves, challenges));
    }));
  }

  getPlayerDetail(playerId: Observable<string | null>): Observable<PlayerDetail | null> {
    return combineLatest([playerId, this.teams, this.players, this.solves, this.getChallengeDetails()]).pipe(map((params) => {
      const [ playerId, teams, players, solves, challenges ] = params;
      if (playerId == null)
        return null;
      var player = players.find(t => t.id == playerId) || null;
      if (player == null)
        return null;
      return this.toPlayerDetail(player, players, teams, solves, challenges);
    }));
  }

  private toPlayerDetail(player: Player, players: readonly Player[], teams: readonly Team[], solves: readonly Solve[], challenges: readonly ChallengeDetail[]): PlayerDetail {
    var playerDetail = new PlayerDetail();
    playerDetail.id = player.id;
    playerDetail.name = player.name;
    playerDetail.team = teams.find(t => t.players.includes(player.id)) || null;
    playerDetail.solves = solves.filter(s => s.playerId == player.id).map(s => this.toSolveDetail(s, players, teams));
    let solvedChallengeNames = playerDetail.solves.map(s => s.challengeName);
    playerDetail.score = challenges.filter(c => solvedChallengeNames.includes(c.challenge.name)).map(c => c.value).reduce((a,b) => a + b, 0);
    let plainChallenges = challenges.map(c => c.challenge);
    let categories = this.getPrimaryCategories(plainChallenges);
    for (let category of categories) {
      playerDetail.categoryProgress.set(category, this.getCategoryProgress(category, plainChallenges, playerDetail.solves.map(s => s.challengeName)));
    }
    return Object.freeze(playerDetail)
  }

  private toTeamDetail(team: Team, players: readonly Player[], solves: readonly Solve[], challenges: readonly ChallengeDetail[]): TeamDetail {
    var teamDetail = new TeamDetail();
      teamDetail.id = team.id;
      teamDetail.name = team.name;
      teamDetail.players = players.filter(p => team!.players.includes(p.id));
      let solvedChallengeNames = solves.filter(s => team!.players.includes(s.playerId)).map(s => s.challengeName);
      let solvedChallengeDetails = challenges.filter(c => solvedChallengeNames.includes(c.challenge.name));
      teamDetail.solves = solvedChallengeDetails.flatMap(c => c.teamSolves).filter(s => s.teamId == team.id);
      teamDetail.score = solvedChallengeDetails.map(c => c.value).reduce((a,b) => a + b, 0);
      let plainChallenges = challenges.map(c => c.challenge);
      let categories = this.getPrimaryCategories(plainChallenges);
      for (let category of categories) {
        teamDetail.categoryProgress.set(category, this.getCategoryProgress(category, plainChallenges, teamDetail.solves.map(s => s.challengeName)));
      }
      return Object.freeze(teamDetail);
  }

  private toChallengeDetail(metadata: Metadata, challenge: Challenge, currentPlayerId: string | null, solves: readonly Solve[], players: readonly Player[], teams: readonly Team[]) {
    var challDetail = new ChallengeDetail();
    challDetail.challenge = challenge;
    challDetail.playerSolves = solves.filter(s => s.challengeName == challenge.name).map(s => this.toSolveDetail(s, players, teams));
    var solvedPlayerIds = challDetail.playerSolves.map(s => s.playerId);
    var teamsWithSolves = teams.filter(t => t.players.some(p => solvedPlayerIds.includes(p)));
    challDetail.teamSolves = teamsWithSolves.map(t => this.toTeamSolveDetail(t, challenge, solves, players));
    challDetail.teamSolves.sort((a, b) => a.solvedAt.getTime() - b.solvedAt.getTime());

    let max = metadata.challengeMaximumValue;
    let min = metadata.challengeMinimumValue;
    let decay = metadata.challengeSolvesBeforeMinimum;
    let numSolves = metadata.teams ? challDetail.teamSolves.length : challDetail.playerSolves.length;
    challDetail.value = Math.ceil((((min - max) / (decay * decay)) * (numSolves * numSolves)) + max);

    if (currentPlayerId) {
      challDetail.solvedByPlayer = solves.find(s => s.challengeName == challenge.name && s.playerId == currentPlayerId) != undefined;
      var team = teams.find(t => t.players.includes(currentPlayerId)) || null;
      if (team == null) {
        challDetail.solvedByTeam = false;
      }
      else {
        challDetail.solvedByTeam = challDetail.teamSolves.find(t => t.teamId == team!.id) != undefined;
      }
    }
    return Object.freeze(challDetail);
  }

  private toTeamSolveDetail(team: Team, challenge: Challenge, solves: readonly Solve[], players: readonly Player[]): TeamSolveDetail {
    var teamSolveDetail = new TeamSolveDetail();
    teamSolveDetail.challengeName = challenge.name;
    teamSolveDetail.teamId = team.id;
    teamSolveDetail.teamName = team.name;
    let teamChallengeSolves = solves.filter(s => s.challengeName == challenge.name && team.players.includes(s.playerId));
    teamChallengeSolves.sort((a, b) => new Date(a.solvedAt).getTime() - new Date(b.solvedAt).getTime());
    teamSolveDetail.solvedAt = new Date(teamChallengeSolves[0].solvedAt);
    let playerIds = teamChallengeSolves.map(s => s.playerId);
    teamSolveDetail.players = players.filter(p => playerIds.includes(p.id));
    return Object.freeze(teamSolveDetail);
  }

  private toSolveDetail(solve: Solve, players: readonly Player[], teams: readonly Team[]): SolveDetail {
    var solveDetail = new SolveDetail();
    solveDetail.challengeName = solve.challengeName;
    solveDetail.playerId = solve.playerId;
    solveDetail.solvedAt = new Date(solve.solvedAt);
    solveDetail.playerName = players.find(p => p.id == solve.playerId)?.name ?? '';
    var team = teams.find(t => t.players.includes(solve.playerId)) || null;
    solveDetail.teamId = team?.id || null;
    solveDetail.teamName = team?.name || null;
    return Object.freeze(solveDetail);
  }

  private compareChallengeDetails(a: ChallengeDetail, b: ChallengeDetail): number {
    // Sort by value
    let valueDifference = a.value - b.value;
    if (valueDifference == 0) {
      // If value is equal, sort by author rated difficulty
      let difficultyDifference = this.helper.difficultyToNumber(a.challenge.difficulty) - this.helper.difficultyToNumber(b.challenge.difficulty);
      if (difficultyDifference == 0) {
        // Fallback to sort alphabetically
        return a.challenge.name.localeCompare(b.challenge.name);
      }
      return difficultyDifference
    }
    return valueDifference;
  }

  private compareScoreboardRanking(a: ScoreboardRanking, b: ScoreboardRanking): number {
    // Sort by score (higher is better)
    let scoreDifference = b.score - a.score;
    if (scoreDifference == 0) {
      // If score is equal, solve by last solve (lower is better)
      let now = new Date().getTime();
      let lastSolveDifference = (a.lastSolveAt?.getTime() ?? now) - (b.lastSolveAt?.getTime() ?? now);
      if (lastSolveDifference == 0) {
        // If all else fails, sort alphabetically by team name (lower is better)
        return a.name.localeCompare(b.name);
      }
      return lastSolveDifference;
    }
    return scoreDifference;
  }

  private getCategoryProgress(category: string, challenges: readonly Challenge[], solvedChallengeNames: string[]): number {
    let categoryChallenges = challenges.filter(c => this.getPrimaryCategory(c) == category);
    let solvedChallenges = categoryChallenges.filter(challenge => solvedChallengeNames.some(solve => solve === challenge.name));
    return Math.floor((solvedChallenges.length / categoryChallenges.length) * 100);
  }

  private getPrimaryCategories(challenges: readonly Challenge[]) {
    return [...new Set(challenges.map(c => this.getPrimaryCategory(c)))].sort();
  }

  private getPrimaryCategory(challenge: Challenge) {
    return this.helper.getPrimaryCategory(challenge.categories);
  }
}
