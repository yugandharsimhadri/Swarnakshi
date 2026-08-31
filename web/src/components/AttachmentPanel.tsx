import { useRef, useState } from "react";
import { api, apiUpload, tokens, type ApiError } from "@/lib/api";
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

/** Reusable list + upload for any entity. Used on purchase / expense / request detail. */
export function AttachmentPanel({ entityType, entityId, canEdit }: { entityType: string; entityId: string; canEdit: boolean }) {
  const { data, loading, error, reload } = useAsync(
    () => api<Attachment[]>("/attachments", { query: { entityType, entityId } }),
    [entityType, entityId],
  );
  const fileRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [upErr, setUpErr] = useState<ApiError | null>(null);

  async function upload(file: File) {
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

  function download(a: Attachment) {
    fetch(`/api/attachments/${a.id}/download`, { headers: { Authorization: `Bearer ${tokens.access}` } })
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
      <div className="text-xs font-semibold uppercase tracking-wide text-text-dim">Documents</div>
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.length ?? 0) === 0 ? <div className="text-xs text-text-dim">None attached.</div> :
          data!.map((a) => (
            <Card key={a.id} className="flex items-center justify-between py-2">
              <button onClick={() => download(a)} className="min-w-0 text-left">
                <div className="truncate text-sm">{a.fileName}</div>
                <div className="text-xs text-text-dim">{kb(a.size)}</div>
              </button>
              {canEdit && <button onClick={() => remove(a.id)} className="px-2 text-text-dim">✕</button>}
            </Card>
          ))
      )}
      {canEdit && (
        <>
          <input
            ref={fileRef}
            type="file"
            className="hidden"
            onChange={(e) => e.target.files?.[0] && upload(e.target.files[0])}
          />
          <Button variant="ghost" className="w-full" disabled={busy} onClick={() => fileRef.current?.click()}>
            {busy ? "Uploading…" : "+ Attach file"}
          </Button>
          <ErrorText error={upErr} />
        </>
      )}
    </div>
  );
}
