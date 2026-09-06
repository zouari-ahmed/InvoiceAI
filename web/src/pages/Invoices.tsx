import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { listInvoices, setToken } from "../api";
import type { InvoiceSummary } from "../types";

export default function Invoices() {
  const [rows, setRows] = useState<InvoiceSummary[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    listInvoices()
      .then(setRows)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to load"))
      .finally(() => setLoading(false));
  }, []);

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
        <button className="linkish" onClick={logout}>
          Sign out
        </button>
      </div>

      {error && <div className="error">{error}</div>}
      {loading && <p className="muted">Loading...</p>}

      {!loading && !error && rows.length === 0 && (
        <p className="muted">No invoices yet.</p>
      )}

      {rows.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Number</th>
              <th>Date</th>
              <th>Supplier</th>
              <th className="num">Total</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id}>
                <td>{r.invoiceNumber ?? <span className="muted">—</span>}</td>
                <td>{r.issueDate ?? <span className="muted">—</span>}</td>
                <td>{r.supplierName ?? <span className="muted">—</span>}</td>
                <td className="num">{money(r.total, r.currency)}</td>
                <td>
                  <span className={`badge ${r.status}`}>{r.status}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}