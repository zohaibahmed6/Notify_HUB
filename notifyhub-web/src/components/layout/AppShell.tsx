import { NavLink, Outlet } from "react-router-dom";

import { useAuth } from "@/context/AuthContext";
import { useInboxHub } from "@/hooks/useInboxHub";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const NAV_LINKS = [
  { to: "/inbox", label: "Inbox" },
  { to: "/tasks", label: "Task board" },
];

export default function AppShell() {
  const { user, logout } = useAuth();
  // One shared real-time connection for every authenticated screen, not just Inbox —
  // a task escalation or reassignment could happen while a staff member is on the
  // task board, and re-mounting the hub per-page would drop/reconnect needlessly.
  useInboxHub();

  return (
    <div className="flex h-screen flex-col bg-background">
      <header className="flex shrink-0 items-center justify-between border-b px-4 py-3">
        <div className="flex items-center gap-6">
          <span className="font-semibold">NotifyHub</span>
          <nav className="flex gap-1">
            {NAV_LINKS.map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                className={({ isActive }) =>
                  cn(
                    "rounded-md px-3 py-1.5 text-sm font-medium transition-colors hover:bg-accent hover:text-accent-foreground",
                    isActive && "bg-accent text-accent-foreground",
                  )
                }
              >
                {link.label}
              </NavLink>
            ))}
          </nav>
        </div>
        <div className="flex items-center gap-3">
          <span className="text-sm text-muted-foreground">
            {user?.username} ({user?.role})
          </span>
          <Button variant="outline" size="sm" onClick={logout}>
            Sign out
          </Button>
        </div>
      </header>
      <main className="min-h-0 flex-1">
        <Outlet />
      </main>
    </div>
  );
}
