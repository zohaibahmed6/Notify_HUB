import { useEffect, useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { Menu, Search } from "lucide-react";

import { useAuth } from "@/context/AuthContext";
import { useUIVersion } from "@/context/UIVersionContext";
import { useInboxHub } from "@/hooks/useInboxHub";
import { Button } from "@/components/ui/button";
import { CommandPalette } from "@/components/v2/command-palette";
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
  { to: "/settings", label: "Settings" },
];

export default function AppShell() {
  const { user, logout } = useAuth();
  const { version } = useUIVersion();
  // One shared real-time connection for every authenticated screen, not just Inbox —
  // a task escalation or reassignment could happen while a staff member is on the
  // task board, and re-mounting the hub per-page would drop/reconnect needlessly.
  useInboxHub();

  // Command palette (Cmd/Ctrl+K) is redesign-only — trigger, shortcut listener, and
  // the palette itself all gated on `version` here rather than in a separate
  // AppShellV2, per the redesign plan.
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [navOpen, setNavOpen] = useState(false);
  const isRedesign = version === "redesign";

  const visibleNavLinks = NAV_LINKS.filter(
    (link) => !(link.adminOnlyInRedesign && isRedesign && user?.role !== "Admin"),
  );

  useEffect(() => {
    if (!isRedesign) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setPaletteOpen((open) => !open);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isRedesign]);

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
          {isRedesign && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPaletteOpen(true)}
              className="hidden gap-2 text-muted-foreground sm:inline-flex"
            >
              <Search className="size-4" />
              Search...
              <kbd className="ml-1 rounded border bg-muted px-1.5 py-0.5 text-2xs font-medium text-muted-foreground">
                {"⌘"}K
              </kbd>
            </Button>
          )}
          {isRedesign && (
            <Button
              variant="outline"
              size="icon"
              onClick={() => setPaletteOpen(true)}
              className="text-muted-foreground sm:hidden"
            >
              <Search className="size-4" />
              <span className="sr-only">Search</span>
            </Button>
          )}
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
      {isRedesign && <CommandPalette open={paletteOpen} onOpenChange={setPaletteOpen} />}
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
