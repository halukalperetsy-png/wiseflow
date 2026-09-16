/*
 * Checks for the permission helpers the shell uses to decide what to show.
 *
 * Run with:  npm run check:permissions
 *
 * Dependency-free, like healthApi.check.ts: Node 24 runs TypeScript directly
 * and throwing at the end makes the exit code non-zero.
 */

import {
  canManageProductGroups,
  canManageUsers,
  canViewProductGroups,
  hasPermission,
  PERMISSIONS,
} from './permissions.ts';
import type { CurrentUser } from './types.ts';

let failures = 0;

function check(name: string, passed: boolean, detail = ''): void {
  if (passed) {
    console.log(`  PASS  ${name}`);
    return;
  }
  failures += 1;
  console.error(`  FAIL  ${name}${detail ? ` -- ${detail}` : ''}`);
}

function user(permissions: string[], roles: string[] = []): CurrentUser {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    email: 'someone@example.test',
    displayName: 'Someone',
    mustChangePassword: false,
    roles,
    permissions,
  };
}

const administrator = user(
  [PERMISSIONS.productGroupView, PERMISSIONS.userManagement, PERMISSIONS.productGroupManagement],
  ['Admin'],
);

const viewer = user([PERMISSIONS.productGroupView], ['Viewer']);

function main(): void {
  console.log('permission checks\n');

  check('an administrator may manage users', canManageUsers(administrator));
  check('an administrator may manage product groups', canManageProductGroups(administrator));
  check('an administrator may view product groups', canViewProductGroups(administrator));

  check('a viewer may view product groups', canViewProductGroups(viewer));
  check('a viewer may not manage users', !canManageUsers(viewer));
  check('a viewer may not manage product groups', !canManageProductGroups(viewer));

  // Nobody signed in: every check answers no rather than throwing, so the shell
  // can render during the first session probe.
  check('nobody signed in may manage users', !canManageUsers(null));
  check('nobody signed in may view product groups', !canViewProductGroups(null));

  // A permission the server never granted is not inferred from a role name.
  check(
    'holding the Admin role without the permission grants nothing',
    !canManageUsers(user([], ['Admin'])),
  );

  check(
    'an unrelated permission does not satisfy another',
    !hasPermission(user([PERMISSIONS.userManagement]), PERMISSIONS.productGroupManagement),
  );

  check('an empty permission list grants nothing', !hasPermission(user([]), PERMISSIONS.productGroupView));

  if (failures > 0) {
    throw new Error(`${failures} permission check(s) failed.`);
  }
  console.log('\nAll permission checks passed.');
}

main();
