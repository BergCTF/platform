import { Injectable } from "@angular/core";
import { HttpClient, HttpHeaders } from "@angular/common/http";
import {
  Challenge,
  CurrentPlayer,
  CurrentTeam,
  Instance,
  Metadata,
  Page,
  Player,
  Solve,
  Team,
} from "../api-model";
import { Observable } from "rxjs";

@Injectable({
  providedIn: "root",
})
export class ApiService {
  private baseUrl = window.location.origin;

  constructor(private http: HttpClient) {}

  httpOptions = {
    headers: new HttpHeaders({
      Accept: "application/json",
      "Content-Type": "application/json",
      "Cache-Control":
        "no-cache, no-store, must-revalidate, post-check=0, pre-check=0",
      Pragma: "no-cache",
      Expires: "0",
    }),
  };

  /* Challenge */

  getChallenges(): Observable<Challenge[]> {
    return this.http.get<Challenge[]>(
      this.baseUrl + "/api/v2/challenges",
      this.httpOptions,
    );
  }

  getChallenge(name: string): Observable<Challenge> {
    return this.http.get<Challenge>(
      this.baseUrl + "/api/v2/challenges/" + name,
      this.httpOptions,
    );
  }

  /* Instance */

  getInstances(): Observable<Instance[]> {
    return this.http.get<Instance[]>(
      this.baseUrl + "/api/v2/instances",
      this.httpOptions,
    );
  }

  getInstance(): Observable<Instance> {
    return this.http.get<Instance>(
      this.baseUrl + "/api/v2/instances/current",
      this.httpOptions,
    );
  }

  startInstance(challengeName: string): Observable<Instance> {
    let data = {
      challenge: challengeName,
    };
    return this.http.post<Instance>(
      this.baseUrl + "/api/v2/instances/current",
      data,
      this.httpOptions,
    );
  }

  stopInstance(): Observable<Instance> {
    return this.http.delete<Instance>(
      this.baseUrl + "/api/v2/instances/current",
      this.httpOptions,
    );
  }

  /* Metadata */

  getMetadata(): Observable<Metadata> {
    return this.http.get<Metadata>(
      this.baseUrl + "/api/v2/metadata",
      this.httpOptions,
    );
  }

  /* Page */

  getPages(): Observable<Page[]> {
    return this.http.get<Page[]>(
      this.baseUrl + "/api/v2/pages",
      this.httpOptions,
    );
  }

  /* Player */

  getPlayers(): Observable<Player[]> {
    return this.http.get<Player[]>(
      this.baseUrl + "/api/v2/players",
      this.httpOptions,
    );
  }

  getPlayer(id: string): Observable<Player> {
    return this.http.get<Player>(
      this.baseUrl + "/api/v2/players/" + id,
      this.httpOptions,
    );
  }

  getCurrentPlayer(): Observable<CurrentPlayer> {
    return this.http.get<CurrentPlayer>(
      this.baseUrl + "/api/v2/players/current",
      this.httpOptions,
    );
  }

  setCurrentPlayerAttributes(attributes: {
    [key: string]: string;
  }): Observable<void> {
    let data = {
      attributes: attributes,
    };
    return this.http.patch<void>(
      this.baseUrl + "/api/v2/players/current",
      data,
      this.httpOptions,
    );
  }

  resetApiKey(): Observable<string> {
    return this.http.delete<string>(
      this.baseUrl + "/api/v2/players/current/api-key",
      this.httpOptions,
    );
  }

  deleteCurrentPlayer(): Observable<void> {
    return this.http.delete<void>(
      this.baseUrl + "/api/v2/players/current",
      this.httpOptions,
    );
  }

  /* Solve */

  getSolves(): Observable<Solve[]> {
    return this.http.get<Solve[]>(
      this.baseUrl + "/api/v2/solves",
      this.httpOptions,
    );
  }

  addSolve(challenge: string, flag: string): Observable<void> {
    let data = {
      challenge: challenge,
      flag: flag,
    };
    return this.http.post<void>(
      this.baseUrl + "/api/v2/solves",
      data,
      this.httpOptions,
    );
  }

  /* Team */

  getTeams(): Observable<Team[]> {
    return this.http.get<Team[]>(
      this.baseUrl + "/api/v2/teams",
      this.httpOptions,
    );
  }

  getCurrentTeam(): Observable<CurrentTeam> {
    return this.http.get<CurrentTeam>(
      this.baseUrl + "/api/v2/teams/current",
      this.httpOptions,
    );
  }

  leaveCurrentTeam(): Observable<void> {
    return this.http.delete<void>(
      this.baseUrl + "/api/v2/teams/current",
      this.httpOptions,
    );
  }

  createTeam(name: string): Observable<void> {
    let data = {
      name: name,
    };
    return this.http.post<void>(
      this.baseUrl + "/api/v2/teams/create",
      data,
      this.httpOptions,
    );
  }

  joinTeam(joinToken: string): Observable<CurrentTeam> {
    let data = {
      joinToken: joinToken,
    };
    return this.http.post<CurrentTeam>(
      this.baseUrl + "/api/v2/teams/join",
      data,
      this.httpOptions,
    );
  }

  downloadFile(relativeUrl: string): Observable<Blob> {
    return this.http.get(this.baseUrl + relativeUrl, { responseType: "blob" });
  }
}
