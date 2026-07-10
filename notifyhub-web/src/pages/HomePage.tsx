import { useAuth } from "@/context/AuthContext";
import { Button } from "@/components/ui/button";

// Placeholder landing screen: the real shared inbox / task board / templates /
// audit screens (§6b) land in later build steps. This exists so step 1 has
// somewhere to redirect to after a successful login.
export default function HomePage() {
  const { user, logout } = useAuth();

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-background p-4">
      <h1 className="text-2xl font-semibold">Welcome to NotifyHub</h1>
      <p className="text-muted-foreground">
        Signed in as {user?.username} ({user?.role})
      </p>
      <Button variant="outline" onClick={logout}>
        Sign out
      </Button>
    </div>
  );
}
