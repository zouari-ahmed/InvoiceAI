import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getStats } from "../api";
import type { DashboardStats } from "../types";

export default function Dashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getStats()
      .then(setStats)
      .catch((e) =>
        setError(e instanceof Error ? e.message : "Failed to load statistics.")
      );
  }, []);

  function money(value: number, currency: string) {
    return `${value.toFixed(3)} ${currency}`;
  }

  return (
    <div className="page">
      <div className="bar">
        <h1>Dashboard</h1>
        <div className="head-right">
          <Link to="/" className="linkish">Invoices</Link>
          <Link to="/upload" className="linkish">Upload</Link>
        </div>
      </div>

      {error && <div className="error">{error}</div>}
      {!stats && !error && <p className="muted">Loading...</p>}
      {stats && (
        <>
          <div className="cards">
            <div className="card stat">
              <span className="stat-label">Invoices</span>
              <span className="stat-value">{stats.totalInvoices}</span>
            </div>
            <div className="card stat">
              <span className="stat-label">Reviewed value</span>
              <span className="stat-value">{money(stats.totalAmount, stats.currency)}</span>
            </div>
            <div className="card stat">
              <span className="stat-label">Extraction accuracy</span>
              <span className="stat-value">
                {stats.accuracyRate === null ? "—" : `${stats.accuracyRate}%`}
              </span>
              <span className="stat-note">
                {stats.fieldsCorrect}/{stats.fieldsCompared} fields on{" "}
                {stats.accuracySampleSize} reviewed invoice
                {stats.accuracySampleSize === 1 ? "" : "s"}
              </span>
            </div>
          </div>

          <h3>By status</h3>
          <table>
            <thead>
              <tr><th>Status</th><th className="num">Count</th></tr>
            </thead>
            <tbody>
              {stats.byStatus.map((s) => (
                <tr key={s.status}>
                  <td><span className={`badge ${s.status}`}>{s.status}</span></td>
                  <td className="num">{s.count}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <h3>Top suppliers</h3>
          <table>
            <thead>
              <tr><th>Supplier</th><th className="num">Invoices</th><th className="num">Total</th></tr>
            </thead>
            <tbody>
              {stats.topSuppliers.map((s) => (
                <tr key={s.supplier}>
                  <td>{s.supplier}</td>
                  <td className="num">{s.count}</td>
                  <td className="num">{money(s.total, stats.currency)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}