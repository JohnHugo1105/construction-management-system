import { Signal, linkedSignal } from '@angular/core';

/**
 * The latest value a resource loaded, kept on screen while the next one loads (so a list dims instead of flashing empty).
 * It is undefined only before the first value arrives.
 */
export function latestValue<T>(resource: {
  hasValue(): boolean;
  value(): T | undefined;
}): Signal<T | undefined> {
  return linkedSignal<T | undefined, T | undefined>({
    source: () => (resource.hasValue() ? resource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });
}
