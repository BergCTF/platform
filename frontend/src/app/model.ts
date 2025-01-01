import { Challenge, Player, Team } from "./api-model";

export class PlayerDetail {
    public id: string = '';
    public name: string = '';
    public team: Team | null = null;
    public score: number = 0;
    public solves: SolveDetail[] = [];
    public categoryProgress: Map<string, number> = new Map();
}

export class TeamDetail {
    public id: string = '';
    public name: string = '';
    public players: Player[] = [];
    public score: number = 0;
    public solves: TeamSolveDetail[] = [];
    public categoryProgress: Map<string, number> = new Map();
}

export class ChallengeDetail {
    public challenge: Challenge = new Challenge();
    public playerSolves: SolveDetail[] = [];
    public teamSolves: TeamSolveDetail[] = [];
    public solvedByPlayer: boolean = false;
    public solvedByTeam: boolean = false;
    public value: number = 0;
}

export class ChallengeDetailCategory {
    public category: string = '';
    public challenges: ChallengeDetail[] = [];
    public numSolved: number = 0;
    public numTotal: number = 0;
}

export class SolveDetail {
    public playerId: string = '';
    public solvedAt: Date = new Date();
    public challengeName: string = '';
    public playerName: string = '';
    public teamId: string | null = null;
    public teamName: string | null = null;
}

export class TeamSolveDetail {
    public solvedAt: Date = new Date();
    public challengeName: string = '';
    public players: Player[] = [];
    public teamId: string = '';
    public teamName: string | null = null;
}

export class ScoreboardRanking {
    public lastSolveAt: Date | null = null;
    public name: string = '';
    public id: string = '';
    public score: number = 0;
    public rank: number = 0;
    public challengesByCategory: ScoreboardChallengeByCategory[] = [];
}

export class ScoreboardChallengeByCategory {
    public category: string = '';
    public scoreboardChallengeEntries: ScoreboardChallengeEntry[] = [];
    public numSolved: number = 0;
    public numTotal: number = 0;
}

export class ScoreboardChallengeEntry {
    public challenge: Challenge = new Challenge();
    public solved: boolean = false;
    public solvedAt: Date | null = null;
}