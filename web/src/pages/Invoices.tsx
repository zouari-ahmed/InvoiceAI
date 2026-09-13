import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { searchInvoices, setToken } from "../api";
import type { InvoiceSummary } from "../types";

const STATUSES = [
  "", "Uploaded", "Processing", "PendingReview",
  "Reviewed", "ExtractionFailed", "Exported"
];

export default function Invoices() {
  const [rows, setRows] = useState<InvoiceSummary[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [minTotal, setMinTotal] = useState("");
  const [maxTotal, setMaxTotal] = useState("");

  const navigate = useNavigate();

  function run(params: Record<string, string>) {
    setLoading(true);
    setError(null);
    searchInvoices(params)
      .then(setRows)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to load"))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    run({});
  }, []);

  function apply(e: FormEvent) {
    e.preventDefault();
    run({ search, status, from, to, minTotal, maxTotal });
  }

  function clearAll() {
    setSearch(""); setStatus(""); setFrom("");
    setTo(""); setMinTotal(""); setMaxTotal("");
    run({});
  }

  function logout() {
    setToken(null);
    navigate("/login");
  }

  function money(value: number, currency: string) {
    return `${value.toFixed(3)} ${currency}`;
  }
  return (
    <div className="page">
      <div className="bar">
        <h1>Invoices</h1>
        <div className="head-right">
          <Link to="/dashboard" className="linkish">Dashboard</Link>
          <Link to="/upload" className="linkish">Upload</Link>
          <button className="linkish" onClick={logout}>Sign out</button>
        </div>
      </div>

      <form className="filters" onSubmit={apply}>
        <input placeholder="Number or supplier" value={search}
          onChange={(e) => setSearch(e.target.value)} />
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          {STATUSES.map((s) => (
            <option key={s} value={s}>{s === "" ? "Any status" : s}</option>
          ))}
        </select>
        <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        <input type="number" step="0.001" placeholder="Min" value={minTotal}
          onChange={(e) => setMinTotal(e.target.value)} />
        <input type="number" step="0.001" placeholder="Max" value={maxTotal}
          onChange={(e) => setMaxTotal(e.target.value)} />
        <button type="submit">Filter</button>
        <button type="button" className="linkish" onClick={clearAll}>Clear</button>
      </form>

      {error && <div className="error">{error}</div>}
      {loading && <p className="muted">Loading...</p>}

      {!loading && !error && rows.length === 0 && (
        <p className="muted">No invoices match these filters.</p>
      )}

      {rows.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Number</th><th>Date</th><th>Supplier</th>
              <th className="num">Total</th><th>Status</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id} onClick={() => navigate(`/invoices/${r.id}`)}>
                <td>{r.invoiceNumber ?? <span className="muted">—</span>}</td>
                <td>{r.issueDate ?? <span className="muted">—</span>}</td>
                <td>{r.supplierName ?? <span className="muted">—</span>}</td>
                <td className="num">{money(r.total, r.currency)}</td>
                <td><span className={`badge ${r.status}`}>{r.status}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}