import { useCallback, useEffect, useRef, useState } from "react";
import type { ApiError } from "@/lib/api";

export function useAsync<T>(fn: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(true);

  // Only the most recent request may write to state. Every list in this app re-queries on each
  // keystroke and on each filter change, so several requests are routinely in flight at once; without
  // this guard whichever one the server answers LAST wins, and a slow early response can overwrite
  // the results the user is actually looking at — the list flicks back to stale or empty rows a
  // moment after showing the right ones.
  const latest = useRef(0);

  // `reload` must always run the CURRENT query, even when the caller captured it in an earlier
  // render. A mutation handler closes over `reload` at the moment the row action is clicked, then
  // awaits its POST; if a filter changes while that request is in flight, the refresh afterwards
  // would otherwise re-query with the filters as they were at click time — and being the newest
  // request, it wins the ordering guard below and replaces what the user is now looking at. The
  // list then contradicts its own filter controls: Status reads "All" while the rows are the
  // Active-only ones. Reading `fn` through a ref keeps "reload" meaning "refresh what is on screen
  // now", which is what every caller assumes it means.
  const fnRef = useRef(fn);
  fnRef.current = fn;

  const run = useCallback(() => {
    const request = ++latest.current;
    const isCurrent = () => request === latest.current;

    setLoading(true);
    setError(null);
    fnRef.current()
      .then((value) => { if (isCurrent()) setData(value); })
      .catch((e: ApiError) => { if (isCurrent()) setError(e); })
      .finally(() => { if (isCurrent()) setLoading(false); });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  useEffect(run, [run]);

  // Superseding any in-flight request on unmount stops a late response calling setState on a
  // component that has gone away.
  useEffect(() => () => { latest.current++; }, []);

  return { data, error, loading, reload: run };
}
