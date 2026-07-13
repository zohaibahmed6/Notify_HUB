import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { Bell, Loader2 } from "lucide-react";

import { useAuth } from "@/context/AuthContext";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent } from "@/components/ui/card";
import { cn } from "@/lib/utils";

// Same validation/auth/navigation behavior as the legacy screen (LoginPage.tsx) —
// this is a presentation-only redesign, no business-rule changes.
interface FieldErrors {
  username?: string;
  password?: string;
}

export default function LoginPageV2() {
  const { login, isLoggingIn } = useAuth();
  const navigate = useNavigate();

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();

    const errors: FieldErrors = {};
    if (!username.trim()) errors.username = "Username is required";
    if (!password) errors.password = "Password is required";
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    try {
      await login(username, password);
      navigate("/", { replace: true });
    } catch {
      toast.error("Login failed");
    }
  };

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-background p-4">
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "radial-gradient(circle at 50% 0%, hsl(var(--primary) / 0.14), transparent 55%)",
        }}
      />

      <div className="relative w-full max-w-[380px]">
        <div className="mb-6 flex flex-col items-center gap-3 text-center">
          <div className="flex size-10 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <Bell className="size-5" />
          </div>
          <div>
            <h1 className="text-lg font-semibold">NotifyHub</h1>
            <p className="text-sm text-muted-foreground">Sign in to continue</p>
          </div>
        </div>

        <Card className="border-border/80 shadow-lg shadow-black/[0.03] dark:shadow-black/20">
          <CardContent className="pt-6">
            <form onSubmit={handleSubmit} className="space-y-4" noValidate>
              <div className="space-y-2">
                <Label htmlFor="username">Username</Label>
                <Input
                  id="username"
                  autoComplete="username"
                  autoFocus
                  value={username}
                  onChange={(event) => setUsername(event.target.value)}
                  className={cn(fieldErrors.username && "border-destructive focus-visible:ring-destructive")}
                />
                {fieldErrors.username && <p className="text-sm text-destructive">{fieldErrors.username}</p>}
              </div>
              <div className="space-y-2">
                <Label htmlFor="password">Password</Label>
                <Input
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  className={cn(fieldErrors.password && "border-destructive focus-visible:ring-destructive")}
                />
                {fieldErrors.password && <p className="text-sm text-destructive">{fieldErrors.password}</p>}
              </div>
              <Button type="submit" className="w-full gap-2" disabled={isLoggingIn}>
                {isLoggingIn && <Loader2 className="size-4 animate-spin" />}
                {isLoggingIn ? "Signing in..." : "Sign in"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
