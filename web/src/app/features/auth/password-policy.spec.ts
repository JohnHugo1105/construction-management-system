import { FormControl } from '@angular/forms';
import { checkPassword, passwordMeetsPolicy, passwordPolicyValidator } from './password-policy';

const met = (password: string, username = '') =>
  Object.fromEntries(checkPassword(password, username).map((check) => [check.id, check.met]));

describe('password policy hints', () => {
  it('starts with every rule unmet', () => {
    expect(met('')).toEqual({ length: false, letter: false, number: false });
  });

  it('ticks each rule as it is satisfied', () => {
    expect(met('abcdefghij')).toEqual({ length: true, letter: true, number: false });
    expect(met('1234567890')).toEqual({ length: true, letter: false, number: true });
    expect(met('abc12')).toEqual({ length: false, letter: true, number: true });
    expect(met('abcdefghi1')).toEqual({ length: true, letter: true, number: true });
  });

  it('counts exactly ten characters as long enough, and nine as too short', () => {
    expect(met('abcdefgh1')['length']).toBe(false);
    expect(met('abcdefghi1')['length']).toBe(true);
  });

  it('only asks about the username once there is one, and ignores case', () => {
    expect(checkPassword('abcdefghi1').map((check) => check.id)).not.toContain('username');
    expect(met('abcdefghi1', 'ana')['username']).toBe(true);
    expect(met('XXANAxx123', 'ana')['username']).toBe(false);
  });

  it('judges the whole password, so a strong one passes and a weak one does not', () => {
    expect(passwordMeetsPolicy('correct-horse-9')).toBe(true);
    expect(passwordMeetsPolicy('short1')).toBe(false);
    expect(passwordMeetsPolicy('lettersonlylong')).toBe(false);
  });
});

describe('passwordPolicyValidator', () => {
  it('leaves an empty field to the required rule', () => {
    expect(passwordPolicyValidator()(new FormControl(''))).toBeNull();
  });

  it('points the person at the checklist instead of repeating the rules', () => {
    expect(passwordPolicyValidator()(new FormControl('weak'))).toEqual({
      message: 'Make your password meet every rule below.',
    });
    expect(passwordPolicyValidator()(new FormControl('strong-enough-1'))).toBeNull();
  });

  it('reads the username when the field is checked', () => {
    let username = 'ana';
    const validate = passwordPolicyValidator(() => username);

    expect(validate(new FormControl('ana-password-1'))).not.toBeNull();
    username = 'jo';
    expect(validate(new FormControl('ana-password-1'))).toBeNull();
  });
});
