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

  const run = useCallback(() => {
    const request = ++latest.current;
    const isCurrent = () => request === latest.current;

    setLoading(true);
    setError(null);
    fn()
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
