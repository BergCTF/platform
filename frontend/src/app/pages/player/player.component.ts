import { Component, OnInit } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { ActivatedRoute } from '@angular/router';
import { Helpers } from 'src/app/services/helpers.service';
import { Player, PlayerSolve } from 'src/app/model';

@Component({
  selector: 'app-player',
  templateUrl: './player.component.html',
  styleUrls: ['./player.component.less'],
})
export class PlayerComponent implements OnInit {
  uuid = '';
  player: Player | undefined = undefined;
  constructor(
    public apiService: ApiService,
    public helpers: Helpers,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.uuid = params['uuid'];

      this.apiService.refreshCtfChallenges(() => {
        this.player = this.apiService.getPlayer(this.uuid);
      });

      this.apiService.refreshPlayerSelf(() => {
        const playerSelf = this.apiService.getPlayerSelf();
        if (playerSelf.player && playerSelf.player.id === this.uuid) {
          this.player = playerSelf.player;
        }
      });
    });
  }

  playerName() {
    if (this.player) {
      return this.player.name;
    }
    return 'Player not found';
  }

  getPlayerAttributes() {
    const scoreboard = this.apiService.getPlayerScoreboard();
    return scoreboard.find(entry => entry.playerId === this.uuid);
  }

  lastSolve() {
    const playerAttributes = this.getPlayerAttributes();
    if (playerAttributes) {
      return playerAttributes.lastSolve;
    }
    return '';
  }

  getSolves(): PlayerSolve[] {
    const playerAttributes = this.getPlayerAttributes();
    if (playerAttributes) {
      return playerAttributes.solves;
    }
    return [];
  }

  getScore() {
    const playerAttributes = this.getPlayerAttributes();
    if (playerAttributes) {
      return playerAttributes.score;
    }
    return 0;
  }

  getCategoryProgress(category: string) {
    const challenges = this.apiService.getCtfChallenges();
    if (Object.keys(challenges.challengesByCategory).includes(category)) {
      const categoryChallenges = challenges.challengesByCategory[category];
      const playerSolves = this.getSolves();
      const solvedChallenges = categoryChallenges.filter(challenge => playerSolves.some(solve => solve.challengeName === challenge.name));
      return Math.floor((solvedChallenges.length / categoryChallenges.length) * 100);
    }
    return 0;
  }

  getBarColor(percentage: number) {
    const minHue = 240;
    const maxHue = 120;
    percentage /= 100;
    const colorString = `hsl(${percentage * (maxHue - minHue) + minHue},60%,${40 + 10 * percentage}%)`;
    return colorString;
  }
}
