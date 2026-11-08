const API_BASE = "http://localhost:5074/api";

function getToken() {
  return localStorage.getItem("token");
}

export function setAuth(token, user) {
  localStorage.setItem("token", token);
  localStorage.setItem("user", JSON.stringify(user));
}

export function getUser() {
  const raw = localStorage.getItem("user");
  return raw ? JSON.parse(raw) : null;
}

export function clearAuth() {
  localStorage.removeItem("token");
  localStorage.removeItem("user");
}

async function request(path, options = {}) {
  const token = getToken();
  const headers = {
    "Content-Type": "application/json",
    ...(options.headers || {}),
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });

  if (res.status === 401) {
    clearAuth();
    window.location.href = "/login";
    throw new Error("Unauthorized");
  }

  if (!res.ok) {
    let message = `Request failed: ${res.status}`;
    try {
      const body = await res.json();
      message = body.message || message;
    } catch {
      // ignore
    }
    throw new Error(message);
  }

  if (res.status === 204) return null;
  return res.json();
}

export const api = {
  login: (email, password) =>
    request("/auth/login", { method: "POST", body: JSON.stringify({ email, password }) }),
  me: () => request("/auth/me"),
  listTickets: () => request("/tickets"),
  getTicket: (id) => request(`/tickets/${id}`),
  updateStatus: (id, status) =>
    request(`/tickets/${id}/status`, { method: "PUT", body: JSON.stringify({ status }) }),
  reassignDepartment: (id, departmentId) =>
    request(`/tickets/${id}/department`, { method: "PUT", body: JSON.stringify({ departmentId }) }),
  listDepartments: () => request("/departments"),
};
