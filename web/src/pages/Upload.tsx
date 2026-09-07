import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { listInvoices, uploadFiles } from "../api";
import type { InvoiceSummary, UploadResult } from "../types";

export default function Upload() {
  const [files, setFiles] = useState<File[]>([]);
  const [result, setResult] = useState<UploadResult | null>(null);
  const [tracked, setTracked] = useState<InvoiceSummary[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const ids = result?.accepted.map((a) => a.invoiceId) ?? [];
    if (ids.length === 0) return;

    let stopped = false;

    async function poll() {
      try {
        const all = await listInvoices();
        const mine = all.filter((i) => ids.includes(i.id));

        if (stopped) return;
        setTracked(mine);

        const settled = mine.every(
          (i) => i.status !== "Uploaded" && i.status !== "Processing"
        );
        if (settled) return;
      } catch {
        // transient errors are ignored; the next tick retries
      }

      if (!stopped) window.setTimeout(poll, 3000);
    }

    poll();

    return () => {
      stopped = true;
    };
  }, [result]);

  async function send() {
    if (files.length === 0) return;

    setBusy(true);
    setError(null);
    setTracked([]);

    try {
      const r = await uploadFiles(files);
      setResult(r);
      setFiles([]);
      if (inputRef.current) inputRef.current.value = "";
    } catch (e) {
      setError(e instanceof Error ? e.message : "Upload failed.");
    } finally {
      setBusy(false);
    }
  }

  function kb(bytes: number) {
    return `${(bytes / 1024).toFixed(0)} KB`;
  }
  return (
    <div className="page">
      <div className="bar">
        <h1>Upload invoices</h1>
        <Link to="/" className="linkish">
          Back to invoices
        </Link>
      </div>

      <div className="dropzone">
        <input
          ref={inputRef}
          type="file"
          multiple
          accept="application/pdf,image/jpeg,image/png"
          onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
        />
        <p className="muted">PDF, JPEG or PNG. Up to 20 files, 20 MB each.</p>

        {files.length > 0 && (
          <ul className="filelist">
            {files.map((f) => (
              <li key={f.name}>
                {f.name} <span className="muted">{kb(f.size)}</span>
              </li>
            ))}
          </ul>
        )}

        <button onClick={send} disabled={busy || files.length === 0}>
          {busy ? "Uploading..." : `Upload ${files.length || ""}`}
        </button>
      </div>

      {error && <div className="error">{error}</div>}

      {result && result.rejected.length > 0 && (
        <>
          <h3>Rejected</h3>
          <table>
            <thead>
              <tr><th>File</th><th>Reason</th></tr>
            </thead>
            <tbody>
              {result.rejected.map((r) => (
                <tr key={r.fileName}>
                  <td>{r.fileName}</td>
                  <td className="muted">{r.reason}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      {tracked.length > 0 && (
        <>
          <h3>Extraction progress</h3>
          <table>
            <thead>
              <tr><th>Id</th><th>Number</th><th>Supplier</th><th>Status</th></tr>
            </thead>
            <tbody>
              {tracked.map((t) => (
                <tr key={t.id}>
                  <td>{t.id}</td>
                  <td>{t.invoiceNumber ?? <span className="muted">—</span>}</td>
                  <td>{t.supplierName ?? <span className="muted">—</span>}</td>
                  <td><span className={`badge ${t.status}`}>{t.status}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}