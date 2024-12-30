import { Challenge, Player, Solve, Team } from "./api-model";

export class PlayerDetail {
    public id = '';
    public name = '';
    public team: Team | null = null;
    public solves: SolveDetail[] = [];
    public categoryProgress: Map<string, number> = new Map();
}

export class TeamDetail {
    public id = '';
    public name = '';
    public players: Player[] = [];
    public solves: SolveDetail[] = [];
    public categoryProgress: Map<string, number> = new Map();
}

export class ChallengeDetail {
    public challenge: Challenge = new Challenge();
    public solves: SolveDetail[] = [];
    public solvedByPlayer = false;
    public solvedByTeam = false;
}

export class ChallengeDetailCategory {
    public category: string = '';
    public challenges: ChallengeDetail[] = [];
    public numSolved = 0;
    public numTotal = 0;
}

export class SolveDetail {
    public playerId = '';
    public solvedAt = '';
    public challengeName = '';
    public playerName = '';
    public teamId: string | null = null;
    public teamName: string | null = null;
}