// COPIED FROM Dispatcher/lib/clipboard.ts — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see Budgeting/CLAUDE.md.
// Copy plain text to the clipboard, resolving to whether it succeeded. Fails
// gracefully (returns false) when the Clipboard API is unavailable — insecure
// context (non-HTTPS), missing permission, or an unsupported browser — so
// callers can fall back to selectable text or an inline note.

export async function copyToClipboard(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}
