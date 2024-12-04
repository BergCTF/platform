import { Injectable } from '@angular/core';
import { ApiService } from './api.service';
import { Observable, shareReplay, Subject, take } from 'rxjs';
import { Challenge, Instance, Metadata, Player, Solve, Team } from '../model';

@Injectable({
  providedIn: 'root'
})
export class DataService {

  private _challenges: Subject<Challenge[]> = new Subject<Challenge[]>();
  private _players: Subject<Player[]> = new Subject<Player[]>();
  private _teams: Subject<Team[]> = new Subject<Team[]>();
  private _solves: Subject<Solve[]> = new Subject<Solve[]>();
  private _metadata: Subject<Metadata> = new Subject<Metadata>();
  private _instance: Subject<Instance|null> = new Subject<Instance|null>();

  public readonly challenges: Observable<Challenge[]> = this._challenges.asObservable().pipe(shareReplay(1));
  public readonly players: Observable<Player[]> = this._players.asObservable().pipe(shareReplay(1));
  public readonly teams: Observable<Team[]> = this._teams.asObservable().pipe(shareReplay(1));
  public readonly solves: Observable<Solve[]> = this._solves.asObservable().pipe(shareReplay(1));
  public readonly instance: Observable<Instance|null> = this._instance.asObservable().pipe(shareReplay(1));
  public readonly metadata: Observable<Metadata> = this._metadata.asObservable().pipe(shareReplay(1));

  constructor(private apiService: ApiService) {
    this.refreshAllData();

    setTimeout(() => {
      this.apiService.getInstance().subscribe(instance => {
        this._instance.next(instance);
      });
    }, 3000);
  }

  private refreshAllData() {
    this._instance.next(null);
    this.apiService.getMetadata().subscribe(metadata => {
      this._metadata.next(metadata);
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

  ensurePlayer(id: string) {
    this.players.pipe(take(1)).subscribe(players => {
      let player = players.find(p => p.id == id);
      if (player === undefined) {
        this.apiService.getPlayer(id).subscribe(player => {
          players.push(player);
          this._players.next(players);
        });
      }
    });
  }

  ensureChallenge(name: string) {
    this.challenges.pipe(take(1)).subscribe(challenges => {
      let challenge = challenges.find(c => c.name == name);
      if (challenge === undefined) {
        this.apiService.getChallenge(name).subscribe(challenge => {
          challenges.push(challenge);
          this._challenges.next(challenges);
        });
      }
    });
  }

}
