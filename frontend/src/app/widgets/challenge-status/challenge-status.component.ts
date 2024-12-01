import { Component, OnInit } from '@angular/core';
import { PlayerSelf } from 'src/app/model';
import { ApiService } from 'src/app/services/api.service';

@Component({
  selector: 'app-challenge-status',
  templateUrl: './challenge-status.component.html',
  styleUrls: ['./challenge-status.component.less'],
})
export class ChallengeStatusComponent implements OnInit {
  public playerSelf: PlayerSelf | null = null;

  constructor(private apiService: ApiService) {}

  ngOnInit(): void {
    this.playerSelf = this.apiService.getPlayerSelf();
  }

  stopChallengeInstance() {
    this.apiService.stopChallengeInstance().subscribe(() => {
      console.log('Challenge instance stopped');
      this.playerSelf = this.apiService.getPlayerSelf();
    });
  }

  copyToClipboard(element: HTMLElement) {
    this.selectElementContents(element);
    navigator.clipboard
      .writeText(element.innerText)
      .then()
      .catch(e => console.error(e));
  }

  copyToClipboardNcat(hostname: string, port: number, ssl: boolean) {
    navigator.clipboard
      .writeText(`${ssl ? 'ncat --ssl' : 'nc'} ${hostname} ${port}`)
      .then()
      .catch(e => console.error(e));
  }

  copyToClipboardPwntools(hostname: string, port: number, ssl: boolean) {
    navigator.clipboard
      .writeText(`r = remote('${hostname}', ${port}${ssl ? ', ssl=True' : ''})`)
      .then()
      .catch(e => console.error(e));
  }

  selectElementContents(element: HTMLElement) {
    const range = document.createRange();
    range.selectNodeContents(element);
    const selection = window.getSelection();
    selection?.removeAllRanges();
    selection?.addRange(range);
  }
}
