/**
 * A stored phone number, written the way people read one out.
 *
 * The API normalises to E.164 — "+14165550148" — which is right for storage and for
 * dialling and wrong for a screen. An emergency contacts panel is read under pressure and
 * sometimes read aloud to somebody else, and a twelve-digit run with no grouping is where
 * that goes wrong.
 *
 * Only North American numbers are reshaped, because that is the only grouping this can be
 * confident about; anything else is returned untouched rather than guessed at. Formatting
 * a foreign number into the wrong shape would be worse than leaving it plain.
 */
export function formatPhoneNumber(value: string): string {
  const digits = value.replace(/\D/g, "");

  const national = digits.length === 11 && digits.startsWith("1")
    ? digits.slice(1)
    : digits;

  if (national.length !== 10) {
    return value;
  }

  return `(${national.slice(0, 3)}) ${national.slice(3, 6)}-${national.slice(6)}`;
}
