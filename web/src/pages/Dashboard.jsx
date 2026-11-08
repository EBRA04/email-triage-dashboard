import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api, clearAuth, getUser } from "../api";

const STATUS_COLORS = {
  New: "#2563eb",
  NeedsReview: "#d97706",
  InProgress: "#7c3aed",
  Resolved: "#16a34a",
  Closed: "#6b7280",
};

export default function Dashboard() {
  const [tickets, setTickets] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const user = getUser();
  const navigate = useNavigate();

  async function load() {
    setLoading(true);
    setError("");
    try {
      const data = await api.listTickets();
      setTickets(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  function logout() {
    clearAuth();
    navigate("/login");
  }

  const filtered = statusFilter === "all" ? tickets : tickets.filter((t) => t.status === statusFilter);

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <div>
          <h1>
            {user?.role === "SuperAdmin" ? "All Departments" : user?.departmentName || "Dashboard"}
          </h1>
          <p className="subtitle">Signed in as {user?.name} ({user?.email})</p>
        </div>
        <button className="secondary" onClick={logout}>Sign out</button>
      </header>

      <div className="toolbar">
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          <option value="all">All statuses</option>
          <option value="New">New</option>
          <option value="NeedsReview">Needs Review</option>
          <option value="InProgress">In Progress</option>
          <option value="Resolved">Resolved</option>
          <option value="Closed">Closed</option>
        </select>
        <button onClick={load}>Refresh</button>
      </div>

      {error && <div className="error">{error}</div>}
      {loading && <div className="loading">Loading tickets...</div>}

      {!loading && filtered.length === 0 && (
        <div className="empty-state">No tickets here. New requests will appear after ingestion + classification runs.</div>
      )}

      <div className="ticket-list">
        {filtered.map((t) => (
          <div key={t.id} className="ticket-card" onClick={() => navigate(`/tickets/${t.id}`)}>
            <div className="ticket-card-top">
              <span className="status-pill" style={{ background: STATUS_COLORS[t.status] || "#6b7280" }}>
                {t.status}
              </span>
              {t.departmentName && <span className="dept-tag">{t.departmentName}</span>}
              {t.aiConfidence != null && (
                <span className="confidence">AI confidence: {(t.aiConfidence * 100).toFixed(0)}%</span>
              )}
            </div>
            <div className="ticket-subject">{t.emailSubject || "(no subject)"}</div>
            <div className="ticket-summary">{t.aiSummary}</div>
            <div className="ticket-meta">
              <span>From: {t.emailFrom}</span>
              <span>{new Date(t.emailReceivedAt).toLocaleString()}</span>
              {t.assignedToName && <span>Assigned: {t.assignedToName}</span>}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
