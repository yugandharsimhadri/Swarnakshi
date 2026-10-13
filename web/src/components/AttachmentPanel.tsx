import { useEffect, useRef, useState } from "react";
import { api, apiUpload, apiUrl, tokens, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { Button, Card, ErrorText, Spinner } from "@/components/ui";

interface Attachment {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
  createdAt: string;
}

const kb = (n: number) => (n < 1024 ? `${n} B` : n < 1024 * 1024 ? `${(n / 1024).toFixed(0)} KB` : `${(n / 1024 / 1024).toFixed(1)} MB`);

/**
 * Reusable list + upload for any entity. Used on purchase / expense / request detail.
 *
 * `photosOnly` switches it to the bill-photo rule the server enforces for expenses: images, 2.5 MB,
 * and the camera rather than a file browser. The limit is checked here as well as at the server
 * because a phone photo is commonly 6-8 MB — pushing that over a site's 4G only to be refused
 * wastes a minute and the engineer's patience, and he is standing at the shop counter.
 */
export function AttachmentPanel({
  entityType,
  entityId,
  canEdit,
  photosOnly = false,
  onChange,
}: {
  entityType: string;
  entityId: string;
  canEdit: boolean;
  photosOnly?: boolean;
  onChange?: (count: number) => void;
}) {
  const { data, loading, error, reload } = useAsync(
    () => api<Attachment[]>("/attachments", { query: { entityType, entityId } }),
    [entityType, entityId],
  );
  const fileRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [upErr, setUpErr] = useState<ApiError | null>(null);
  const [tooBig, setTooBig] = useState<string | null>(null);

  const maxBytes = photosOnly ? 2_621_440 : 15 * 1024 * 1024;

  // Lets the screen around this one react — the expense card only offers "Send for approval" once
  // a bill is actually attached.
  useEffect(() => {
    if (data) onChange?.(data.length);
  }, [data, onChange]);

  async function upload(file: File) {
    setTooBig(null);
    if (file.size > maxBytes) {
      setTooBig(`That photo is ${(file.size / 1024 / 1024).toFixed(1)} MB. The limit is ${photosOnly ? "2.5 MB" : "15 MB"}.`);
      if (fileRef.current) fileRef.current.value = "";
      return;
    }
    setBusy(true);
    setUpErr(null);
    try {
      const form = new FormData();
      form.set("entityType", entityType);
      form.set("entityId", entityId);
      form.set("file", file);
      await apiUpload("/attachments", form);
      reload();
    } catch (e) {
      setUpErr(e as ApiError);
    } finally {
      setBusy(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  }

  async function remove(id: string) {
    await api(`/attachments/${id}`, { method: "DELETE" });
    reload();
  }

  // apiUrl, not a bare /api path: the UI is served from Cloudflare Pages and the API from another
  // origin entirely, so a relative URL asks the CDN for an endpoint it has never heard of. This is
  // the one request in the app that goes out by hand rather than through api(), because it wants
  // the bytes rather than the JSON envelope.
  function download(a: Attachment) {
    fetch(apiUrl(`/attachments/${a.id}/download`), { headers: { Authorization: `Bearer ${tokens.access}` } })
      .then((r) => r.blob())
      .then((b) => {
        const url = URL.createObjectURL(b);
        const link = document.createElement("a");
        link.href = url;
        link.download = a.fileName;
        link.click();
        URL.revokeObjectURL(url);
      });
  }

  return (
    <div className="space-y-2">
      <div className="text-xs font-semibold uppercase tracking-wide text-text-dim">
        {photosOnly ? "Bill photo" : "Documents"}
      </div>
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.length ?? 0) === 0
          ? <div className="text-xs text-text-dim">{photosOnly ? "No bill attached yet." : "None attached."}</div>
          : data!.map((a) => (
            <Card key={a.id} className="flex items-center justify-between py-2">
              <button onClick={() => download(a)} className="min-w-0 text-left">
                <div className="truncate text-sm">{a.fileName}</div>
                <div className="text-xs text-text-dim">{kb(a.size)}</div>
              </button>
              {canEdit && <button onClick={() => remove(a.id)} className="px-2 text-text-dim" aria-label="Remove">✕</button>}
            </Card>
          ))
      )}
      {canEdit && (
        <>
          <input
            ref={fileRef}
            type="file"
            className="hidden"
            accept={photosOnly ? "image/jpeg,image/png,image/webp" : undefined}
            // Opens the camera straight away on a phone rather than a file browser. On a desktop
            // the attribute is ignored and it behaves as an ordinary file input.
            capture={photosOnly ? "environment" : undefined}
            onChange={(e) => e.target.files?.[0] && upload(e.target.files[0])}
          />
          <Button variant="ghost" className="w-full" disabled={busy} onClick={() => fileRef.current?.click()}>
            {busy ? "Uploading…" : photosOnly ? "Take or choose a photo" : "+ Attach file"}
          </Button>
          {photosOnly && <div className="text-xs text-text-dim">JPG, PNG or WebP · up to 2.5 MB</div>}
          {tooBig && <div className="text-xs text-danger">{tooBig}</div>}
          <ErrorText error={upErr} />
        </>
      )}
    </div>
  );
}
