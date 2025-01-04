import { Component, OnInit } from '@angular/core';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { Instance } from 'src/app/api-model';
import { DataService } from 'src/app/services/data.service';

@Component({
    selector: 'app-challenge-status',
    templateUrl: './challenge-status.component.html',
    styleUrls: ['./challenge-status.component.less'],
    imports: [NgbDropdownModule]
})
export class ChallengeStatusComponent implements OnInit {
  public instance: Instance | null = null;

  constructor(private dataService: DataService) {}

  ngOnInit(): void {
    this.dataService.instance.subscribe(instance => {
      this.instance = instance;
    });
  }

  stopChallengeInstance() {
    this.dataService.stopInstance();
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
