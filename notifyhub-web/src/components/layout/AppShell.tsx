import { useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { Menu } from "lucide-react";

import { useAuth } from "@/context/AuthContext";
import { useUIVersion } from "@/context/UIVersionContext";
import { useInboxHub } from "@/hooks/useInboxHub";
import { Button } from "@/components/ui/button";
import { TaskNavWidget } from "@/components/v2/task-nav-widget";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { cn } from "@/lib/utils";

const NAV_LINKS = [
  // `end` so this only matches the exact "/" path — otherwise NavLink's default prefix
  // matching would also mark Dashboard active on every other route.
  { to: "/", label: "Dashboard", end: true },
  { to: "/inbox", label: "Inbox" },
  { to: "/tasks", label: "Task board" },
  { to: "/templates", label: "Templates" },
  // Redesign-only restriction: Audit log is Admin-only in the new UI (legacy still shows
  // it to Staff scoped to their own actions via /api/audit/mine) — a deliberate product
  // decision, guarded again in AuditLogPageV2 itself for direct-URL access.
  { to: "/audit", label: "Audit log", adminOnlyInRedesign: true },
  // Unconditionally Admin-only (not just in redesign mode, unlike Audit log above) —
  // the server itself is Admin-only (no Staff-scoped "mine" variant exists for SMS
  // History), so hiding the link from Staff in every UI mode matches the API (P9-06).
  { to: "/sms-history", label: "SMS History", adminOnly: true },
  { to: "/settings", label: "Settings" },
];

export default function AppShell() {
  const { user, logout } = useAuth();
  const { version } = useUIVersion();
  // One shared real-time connection for every authenticated screen, not just Inbox —
  // a task escalation or reassignment could happen while a staff member is on the
  // task board, and re-mounting the hub per-page would drop/reconnect needlessly.
  useInboxHub();

  const [navOpen, setNavOpen] = useState(false);
  const isRedesign = version === "redesign";

  const visibleNavLinks = NAV_LINKS.filter(
    (link) =>
      !(link.adminOnlyInRedesign && isRedesign && user?.role !== "Admin") &&
      !("adminOnly" in link && link.adminOnly && user?.role !== "Admin"),
  );

  return (
    <div className="flex h-screen flex-col bg-background">
      <header className="flex shrink-0 items-center justify-between gap-3 border-b px-3 py-3 sm:px-4">
        <div className="flex min-w-0 items-center gap-3 sm:gap-6">
          <Button
            variant="ghost"
            size="icon"
            className="shrink-0 md:hidden"
            onClick={() => setNavOpen(true)}
          >
            <Menu className="size-5" />
            <span className="sr-only">Open navigation</span>
          </Button>
          <span className="shrink-0 font-semibold">NotifyHub</span>
          <nav className="hidden gap-1 md:flex">
            {visibleNavLinks.map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                end={link.end}
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
        <div className="flex items-center gap-2 sm:gap-3">
          <TaskNavWidget />
          <span className="hidden text-sm text-muted-foreground md:inline">
            {user?.username} ({user?.role})
          </span>
          <Button variant="outline" size="sm" onClick={() => void logout()}>
            Sign out
          </Button>
        </div>
      </header>
      <main className="min-h-0 flex-1 overflow-y-auto">
        <Outlet />
      </main>
      <Sheet open={navOpen} onOpenChange={setNavOpen}>
        <SheetContent side="left" className="flex w-3/4 max-w-xs flex-col gap-4">
          <SheetHeader>
            <SheetTitle>NotifyHub</SheetTitle>
          </SheetHeader>
          <nav className="flex flex-col gap-1">
            {visibleNavLinks.map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                end={link.end}
                onClick={() => setNavOpen(false)}
                className={({ isActive }) =>
                  cn(
                    "rounded-md px-3 py-2 text-sm font-medium transition-colors hover:bg-accent hover:text-accent-foreground",
                    isActive && "bg-accent text-accent-foreground",
                  )
                }
              >
                {link.label}
              </NavLink>
            ))}
          </nav>
          <div className="mt-auto border-t pt-4 text-sm text-muted-foreground">
            {user?.username} ({user?.role})
          </div>
        </SheetContent>
      </Sheet>
    </div>
  );
}
