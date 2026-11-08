import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api } from "../api";

const STATUSES = ["New", "NeedsReview", "InProgress", "Resolved", "Closed"];

export default function TicketDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [ticket, setTicket] = useState(null);
  const [departments, setDepartments] = useState([]);
  const [showOriginal, setShowOriginal] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function load() {
    setError("");
    try {
      const [t, depts] = await Promise.all([api.getTicket(id), api.listDepartments()]);
      setTicket(t);
      setDepartments(depts);
    } catch (err) {
      setError(err.message);
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function handleStatusChange(newStatus) {
    setBusy(true);
    try {
      await api.updateStatus(id, newStatus);
      await load();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function handleReassign(e) {
    const deptId = e.target.value || null;
    setBusy(true);
    try {
      await api.reassignDepartment(id, deptId);
      await load();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  if (error && !ticket) return <div className="error">{error}</div>;
  if (!ticket) return <div className="loading">Loading...</div>;

  return (
    <div className="ticket-detail">
      <button className="secondary back-btn" onClick={() => navigate(-1)}>&larr; Back</button>

      <div className="detail-card">
        <h1>{ticket.email.subject || "(no subject)"}</h1>
        <div className="detail-meta">
          <span>From: {ticket.email.from}</span>
          <span>Received: {new Date(ticket.email.receivedAt).toLocaleString()}</span>
        </div>

        <div className="controls-row">
          <label>
            Status
            <select value={ticket.status} onChange={(e) => handleStatusChange(e.target.value)} disabled={busy}>
              {STATUSES.map((s) => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
          </label>

          <label>
            Department
            <select value={ticket.departmentSlug ? departments.find(d => d.slug === ticket.departmentSlug)?.id ?? "" : ""} onChange={handleReassign} disabled={busy}>
              <option value="">(unassigned)</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>{d.name}</option>
              ))}
            </select>
          </label>
        </div>

        <section className="ai-section">
          <h2>AI Classification</h2>
          <div className="ai-grid">
            <div><strong>Suggested department:</strong> {ticket.aiDepartmentGuess || "n/a"}</div>
            <div><strong>Confidence:</strong> {ticket.aiConfidence != null ? `${(ticket.aiConfidence * 100).toFixed(0)}%` : "n/a"}</div>
          </div>
          <div className="ai-summary">
            <strong>Summary:</strong> {ticket.aiSummary || "n/a"}
          </div>
          <div className="ai-reasoning">
            <strong>Reasoning:</strong> {ticket.aiReasoning || "n/a"}
          </div>
        </section>

        <section className="original-section">
          <button className="secondary" onClick={() => setShowOriginal((s) => !s)}>
            {showOriginal ? "Hide original email" : "View original email"}
          </button>
          {showOriginal && (
            <div className="original-email">
              <div><strong>From:</strong> {ticket.email.from}</div>
              <div><strong>To:</strong> {ticket.email.to}</div>
              <div><strong>Subject:</strong> {ticket.email.subject}</div>
              <div><strong>Received:</strong> {new Date(ticket.email.receivedAt).toLocaleString()}</div>
              <hr />
              <pre className="email-body">{ticket.email.bodyText || "(no plain-text body)"}</pre>
            </div>
          )}
        </section>

        <section className="audit-section">
          <h2>Audit trail</h2>
          <ul className="audit-list">
            {ticket.auditLog.map((a, i) => (
              <li key={i}>
                <span className="audit-action">{a.action}</span>
                <span className="audit-detail">{a.detail}</span>
                <span className="audit-meta">{a.actorName} &middot; {new Date(a.createdAt).toLocaleString()}</span>
              </li>
            ))}
          </ul>
        </section>

        {error && <div className="error">{error}</div>}
      </div>
    </div>
  );
}
