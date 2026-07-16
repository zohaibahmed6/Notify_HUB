import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { Plus } from "lucide-react";

import { useCreateUserMutation, useUpdateUserStatusMutation, useUsers } from "@/hooks/useUsers";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { DateTimePicker } from "@/components/v2/date-time-picker";
import { Skeleton } from "@/components/ui/skeleton";
import type { UserDto, UserRole, UserStatus } from "@/types/users";

const ROLES: UserRole[] = ["Staff", "Admin"];
const STATUSES: UserStatus[] = ["Active", "Inactive", "OnLeave"];

/// §7: create users, activate/deactivate/mark on leave. Deactivating or marking a user
/// on leave auto-forwards their open tasks server-side (UsersController.UpdateStatus).
export function UserManagementTab() {
  const { data: users, isLoading } = useUsers();
  const createUser = useCreateUserMutation();
  const updateStatus = useUpdateUserStatusMutation();

  const [username, setUsername] = useState("");
  const [fullName, setFullName] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<UserRole>("Staff");

  // P9-12: LeaveFrom/LeaveTo required together when marking a user OnLeave — collected
  // via a small dialog rather than inline in the status Select, since a bare status
  // change to OnLeave isn't itself enough information for the server to accept.
  const [onLeaveTarget, setOnLeaveTarget] = useState<UserDto | null>(null);
  const [leaveFrom, setLeaveFrom] = useState("");
  const [leaveTo, setLeaveTo] = useState("");

  const handleCreate = async (event: FormEvent) => {
    event.preventDefault();
    if (!username.trim() || !password.trim()) {
      toast.error("Username and password are required");
      return;
    }

    try {
      await createUser.mutateAsync({ username, fullName: fullName || undefined, password, role });
      toast.success("User created");
      setUsername("");
      setFullName("");
      setPassword("");
      setRole("Staff");
    } catch (error) {
      toast.error(errorMessage(error, "User creation failed"));
    }
  };

  const handleStatusChange = async (user: UserDto, status: UserStatus) => {
    if (status === "OnLeave") {
      setOnLeaveTarget(user);
      setLeaveFrom(user.leaveFrom ? user.leaveFrom.slice(0, 10) : "");
      setLeaveTo(user.leaveTo ? user.leaveTo.slice(0, 10) : "");
      return;
    }
    try {
      await updateStatus.mutateAsync({ id: user.id, status });
      toast.success(`User marked ${status}`);
    } catch (error) {
      toast.error(errorMessage(error, "Status update failed"));
    }
  };

  const handleConfirmOnLeave = async () => {
    if (!onLeaveTarget) return;
    if (!leaveFrom || !leaveTo) {
      toast.error("Both From and To dates are required");
      return;
    }
    try {
      await updateStatus.mutateAsync({
        id: onLeaveTarget.id,
        status: "OnLeave",
        leaveFrom: new Date(leaveFrom).toISOString(),
        leaveTo: new Date(leaveTo).toISOString(),
      });
      toast.success("User marked On Leave");
      setOnLeaveTarget(null);
    } catch (error) {
      toast.error(errorMessage(error, "Status update failed"));
    }
  };

  return (
    <div className="space-y-4">
      <Card id="users-list">
        <CardHeader>
          <CardTitle className="text-base">Users</CardTitle>
          <CardDescription>
            Deactivated and On Leave users are excluded from assignment, don't receive new tasks
            or messages, and get read-only access until reactivated.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-8 w-full" />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Username</TableHead>
                  <TableHead>Full name</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(users?.items ?? []).map((u) => (
                  <TableRow key={u.id}>
                    <TableCell className="font-medium">{u.username}</TableCell>
                    <TableCell className="text-muted-foreground">{u.fullName ?? "—"}</TableCell>
                    <TableCell>{u.role}</TableCell>
                    <TableCell>
                      <div className="space-y-1">
                        <Select value={u.status} onValueChange={(v) => handleStatusChange(u, v as UserStatus)}>
                          <SelectTrigger className="h-8 w-32 text-xs">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {STATUSES.map((s) => (
                              <SelectItem key={s} value={s}>
                                {s}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        {u.status === "OnLeave" && u.leaveFrom && u.leaveTo && (
                          <div className="text-2xs text-muted-foreground">
                            {new Date(u.leaveFrom).toLocaleDateString()} – {new Date(u.leaveTo).toLocaleDateString()}
                          </div>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card id="users-create">
        <CardHeader>
          <CardTitle className="text-base">Create user</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleCreate} className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <div className="space-y-1.5">
              <Label htmlFor="user-username">Username</Label>
              <Input id="user-username" value={username} onChange={(e) => setUsername(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="user-fullname">Full name</Label>
              <Input id="user-fullname" value={fullName} onChange={(e) => setFullName(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="user-password">Password</Label>
              <Input id="user-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="user-role">Role</Label>
              <Select value={role} onValueChange={(v) => setRole(v as UserRole)}>
                <SelectTrigger id="user-role">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ROLES.map((r) => (
                    <SelectItem key={r} value={r}>
                      {r}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="sm:col-span-2 lg:col-span-4 flex justify-end">
              <Button type="submit" size="sm" className="gap-1.5" disabled={createUser.isPending}>
                <Plus className="size-3.5" />
                Create user
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Dialog open={onLeaveTarget !== null} onOpenChange={(open) => !open && setOnLeaveTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Mark {onLeaveTarget?.username} On Leave</DialogTitle>
            <DialogDescription>Both dates are required — auto-reverts to Active once To passes.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="leave-from">From</Label>
              <DateTimePicker id="leave-from" mode="date" value={leaveFrom} onChange={setLeaveFrom} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="leave-to">To</Label>
              <DateTimePicker id="leave-to" mode="date" value={leaveTo} onChange={setLeaveTo} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setOnLeaveTarget(null)}>
              Cancel
            </Button>
            <Button onClick={handleConfirmOnLeave} disabled={updateStatus.isPending}>
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
