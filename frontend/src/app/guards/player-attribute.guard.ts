import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';
import { DataService } from '../services/data.service';
import { combineLatest, map, take } from 'rxjs';

export const playerAttributeGuard: CanActivateFn = (
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot) => {

  let router = inject(Router);
  let dataService = inject(DataService);

  return combineLatest([dataService.metadata, dataService.currentPlayer]).pipe(take(1), map(params => {
    const [metadata, currentPlayer] = params;

    if (currentPlayer) {
      // Check if current player is missing some attributes
      let requiredAttributes = metadata.playerAttributes.filter(a => a.required);
      let playerAttributes = new Set(Object.keys(currentPlayer.attributes));
      let missingAttributes = requiredAttributes.filter(a => !playerAttributes.has(a.name));

      if (missingAttributes.length != 0) {
        return router.createUrlTree(['/player-attributes']);
      } else {
        return true;
      }
    } else {
      // If player is not logged in, always allow through
      return true;
    }
  }));
};
