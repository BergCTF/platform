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
    public playerSolves: SolveDetail[] = [];
    public teamSolves: TeamSolveDetail[] = [];
    public solvedByPlayer = false;
    public solvedByTeam = false;
    public value = 0;
}

export class ChallengeDetailCategory {
    public category: string = '';
    public challenges: ChallengeDetail[] = [];
    public numSolved = 0;
    public numTotal = 0;
}

export class SolveDetail {
    public playerId = '';
    public solvedAt: Date = new Date();
    public challengeName = '';
    public playerName = '';
    public teamId: string | null = null;
    public teamName: string | null = null;
}

export class TeamSolveDetail {
    public solvedAt: Date = new Date();
    public challengeName = '';
    public players: Player[] = [];
    public teamId: string | null = null;
    public teamName: string | null = null;
}