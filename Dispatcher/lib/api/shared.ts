// ---------------------------------------------------------------------------
// Eventual consistency — reads come from projection tables that trail writes
// by (typically) well under a second. After a mutation, refetch; if the
// change isn't visible yet, retry the fetch briefly before settling.
// Shared by every domain API client (drivers, clients, trips, billing, …).
// ---------------------------------------------------------------------------

export async function refetchUntil<T>(
  fetcher: () => Promise<T>,
  satisfied: (value: T) => boolean,
  attempts = 6,
  delayMs = 350,
): Promise<T> {
  let last = await fetcher();
  for (let i = 1; i < attempts && !satisfied(last); i += 1) {
    await new Promise((resolve) => setTimeout(resolve, delayMs));
    last = await fetcher();
  }
  return last;
}
