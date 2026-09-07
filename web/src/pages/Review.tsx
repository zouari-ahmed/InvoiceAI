import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { fetchFileBlobUrl, getInvoice, updateInvoice } from "../api";
import type { InvoiceDetail } from "../types";

type EditableLine = {
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number | null;
  lineTotal: number | null;
};

export default function Review() {
  const { id } = useParams();
  const invoiceId = Number(id);
  const navigate = useNavigate();

  const [detail, setDetail] = useState<InvoiceDetail | null>(null);
  const [docUrl, setDocUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const [invoiceNumber, setInvoiceNumber] = useState("");
  const [issueDate, setIssueDate] = useState("");
  const [dueDate, setDueDate] = useState("");
  const [supplierName, setSupplierName] = useState("");
  const [supplierTaxId, setSupplierTaxId] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [customerTaxId, setCustomerTaxId] = useState("");
  const [subtotal, setSubtotal] = useState(0);
  const [vatAmount, setVatAmount] = useState(0);
  const [total, setTotal] = useState(0);
  const [currency, setCurrency] = useState("TND");
  const [lines, setLines] = useState<EditableLine[]>([]);

  useEffect(() => {
    let created: string | null = null;

    getInvoice(invoiceId)
      .then((d) => {
        setDetail(d);
        setInvoiceNumber(d.invoiceNumber ?? "");
        setIssueDate(d.issueDate ?? "");
        setDueDate(d.dueDate ?? "");
        setSupplierName(d.supplier?.name ?? "");
        setSupplierTaxId(d.supplier?.taxId ?? "");
        setCustomerName(d.customer?.name ?? "");
        setCustomerTaxId(d.customer?.taxId ?? "");
        setSubtotal(d.subtotal);
        setVatAmount(d.vatAmount);
        setTotal(d.total);
        setCurrency(d.currency);
        setLines(
          d.lineItems.map((l) => ({
            description: l.description,
            quantity: l.quantity,
            unitPrice: l.unitPrice,
            vatRate: l.vatRate,
            lineTotal: l.lineTotal,
          }))
        );
        return fetchFileBlobUrl(invoiceId);
      })
      .then((url) => {
        created = url;
        setDocUrl(url);
      })
      .catch((e) =>
        setError(e instanceof Error ? e.message : "Failed to load invoice.")
      );

    return () => {
      if (created) URL.revokeObjectURL(created);
    };
  }, [invoiceId]);
  function patchLine(index: number, patch: Partial<EditableLine>) {
    setLines((current) =>
      current.map((l, i) => (i === index ? { ...l, ...patch } : l))
    );
    setSaved(false);
  }

  function removeLine(index: number) {
    setLines((current) => current.filter((_, i) => i !== index));
    setSaved(false);
  }

  function addLine() {
    setLines((current) => [
      ...current,
      { description: "", quantity: 1, unitPrice: 0, vatRate: 19, lineTotal: null },
    ]);
    setSaved(false);
  }

  async function save() {
    setSaving(true);
    setError(null);

    try {
      const body = {
        invoiceNumber: invoiceNumber.trim() || null,
        issueDate: issueDate || null,
        dueDate: dueDate || null,
        supplier: supplierName.trim()
          ? { name: supplierName.trim(), address: null, taxId: supplierTaxId.trim() || null }
          : null,
        customer: customerName.trim()
          ? { name: customerName.trim(), address: null, taxId: customerTaxId.trim() || null }
          : null,
        subtotal,
        vatAmount,
        total,
        currency: currency.trim() || "TND",
        lineItems: lines,
      };

      const updated = await updateInvoice(invoiceId, body);
      setDetail(updated);
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed.");
    } finally {
      setSaving(false);
    }
  }

  const linesSum = lines.reduce(
    (sum, l) => sum + (l.lineTotal ?? l.quantity * l.unitPrice),
    0
  );

  return (
    <div className="review">
      <div className="review-head">
        <button className="linkish" onClick={() => navigate("/")}>
          &larr; Back to invoices
        </button>
        <div className="head-right">
          {detail && <span className={`badge ${detail.status}`}>{detail.status}</span>}
          {saved && <span className="ok">Saved</span>}
          <button onClick={save} disabled={saving}>
            {saving ? "Saving..." : "Save & mark reviewed"}
          </button>
        </div>
      </div>

      {error && <div className="error">{error}</div>}

      <div className="review-body">
        <div className="pane doc">
          {docUrl ? (
            <iframe title="Source document" src={docUrl} />
          ) : (
            <p className="muted">Loading document...</p>
          )}
        </div>
        <div className="pane form">
          <div className="grid2">
            <label>Invoice number
              <input value={invoiceNumber}
                onChange={(e) => { setInvoiceNumber(e.target.value); setSaved(false); }} />
            </label>
            <label>Currency
              <input value={currency}
                onChange={(e) => { setCurrency(e.target.value); setSaved(false); }} />
            </label>
            <label>Issue date
              <input type="date" value={issueDate}
                onChange={(e) => { setIssueDate(e.target.value); setSaved(false); }} />
            </label>
            <label>Due date
              <input type="date" value={dueDate}
                onChange={(e) => { setDueDate(e.target.value); setSaved(false); }} />
            </label>
            <label>Supplier
              <input value={supplierName}
                onChange={(e) => { setSupplierName(e.target.value); setSaved(false); }} />
            </label>
            <label>Supplier tax ID
              <input value={supplierTaxId}
                onChange={(e) => { setSupplierTaxId(e.target.value); setSaved(false); }} />
            </label>
            <label>Customer
              <input value={customerName}
                onChange={(e) => { setCustomerName(e.target.value); setSaved(false); }} />
            </label>
            <label>Customer tax ID
              <input value={customerTaxId}
                onChange={(e) => { setCustomerTaxId(e.target.value); setSaved(false); }} />
            </label>
          </div>

          <h3>Line items</h3>

          <table className="lines">
            <thead>
              <tr><th>Description</th><th>Qty</th><th>Unit</th><th>VAT%</th><th>Total</th><th></th></tr>
            </thead>
            <tbody>
              {lines.map((l, i) => (
                <tr key={i}>
                  <td><input value={l.description}
                    onChange={(e) => patchLine(i, { description: e.target.value })} /></td>
                  <td><input type="number" step="0.001" value={l.quantity}
                    onChange={(e) => patchLine(i, { quantity: Number(e.target.value) })} /></td>
                  <td><input type="number" step="0.001" value={l.unitPrice}
                    onChange={(e) => patchLine(i, { unitPrice: Number(e.target.value) })} /></td>
                  <td><input type="number" step="0.01" value={l.vatRate ?? ""}
                    onChange={(e) => patchLine(i, { vatRate: e.target.value === "" ? null : Number(e.target.value) })} /></td>
                  <td><input type="number" step="0.001" value={l.lineTotal ?? ""}
                    onChange={(e) => patchLine(i, { lineTotal: e.target.value === "" ? null : Number(e.target.value) })} /></td>
                  <td><button className="linkish danger" onClick={() => removeLine(i)}>×</button></td>
                </tr>
              ))}
            </tbody>
          </table>

          <button className="linkish" onClick={addLine}>+ Add line</button>

          <div className="grid2">
            <label>Subtotal
              <input type="number" step="0.001" value={subtotal}
                onChange={(e) => { setSubtotal(Number(e.target.value)); setSaved(false); }} />
            </label>
            <label>VAT amount
              <input type="number" step="0.001" value={vatAmount}
                onChange={(e) => { setVatAmount(Number(e.target.value)); setSaved(false); }} />
            </label>
            <label>Total
              <input type="number" step="0.001" value={total}
                onChange={(e) => { setTotal(Number(e.target.value)); setSaved(false); }} />
            </label>
          </div>

          {Math.abs(linesSum - subtotal) > 0.005 && (
            <div className="warn">
              Line items add up to {linesSum.toFixed(3)} but the subtotal says {subtotal.toFixed(3)}.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}