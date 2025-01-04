export class Metadata {
  public start = '';
  public end = '';
  public allowAnonymousAccess = true;
  public playerAttributes: PlayerAttribute[] = [];
  public freezeStart: string | null = null;
  public freezeEnd: string | null = null;
  public teams = false;
  public challengeMaximumValue = 0;
  public challengeMinimumValue = 0;
  public challengeSolvesBeforeMinimum = 0;
}

export class PlayerAttribute {
  public name = '';
  public public = true;
  public required = false;
  public values: string[] = [];
}

export class Challenge {
  public name = '';
  public displayName = '';
  public author = '';
  public description = '';
  public event: string = '';
  public difficulty = '';
  public flagFormat = '';
  public hasRemote = false;
  public categories: string[] = [];
  public tags: string[] = [];
  public attachments: Attachment[] = [];
}

export class Attachment {
  public fileName = '';
  public downloadUrl = '';
}

export class Service {
  public name = '';
  public hostname = '';
  public port = 0;
  public protocol = '';
  public appProtocol = '';
  public tls = false;
}

export class Page {
  public title = '';
  public index = 0;
  public path = '';
  public content = '';
}

export class Player {
  public id = '';
  public name = '';
  public attributes: Record<string, string> = {};
}

export class CurrentPlayer {
  public id = '';
  public name = '';
  public federatedId = '';
  public apiKeyPlaceholder: string | null = null;
  public roles: string[] = [];
  public attributes: Record<string, string> = {};
}

export class CurrentTeam {
  public id = '';
  public name = '';
  public joinToken: string | null = '';
  public players: string[] = [];
}

export class Team {
  public id = '';
  public name = '';
  public players: string[] = [];
}

export class Instance {
  public name = '';
  public status: ChallengeInstanceState = ChallengeInstanceState.None;
  public services: Service[] = [];
}

export class Solve {
  public playerId = '';
  public solvedAt = '';
  public challengeName = '';
}

export enum ChallengeInstanceState {
  None,
  Starting,
  Running,
  Terminating,
}

export interface WebSocketMessage<T> {
  type: string;
  message: T;
}

export class ProblemDetails {
  public type = '';
  public title = '';
  public status = 0;
  public detail = '';
  public instance = '';
}