import { useState, type FormEvent } from "react";
import { toast } from "sonner";
import { Plus, Trash2 } from "lucide-react";

import { useBookmarks, useCreateBookmarkMutation, useDeleteBookmarkMutation } from "@/hooks/useBookmarks";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";

/// §5: admin-managed snippet library, consumed by the template editor's bookmark dropdown.
export function TemplateTab() {
  const { data: bookmarks, isLoading } = useBookmarks();
  const createBookmark = useCreateBookmarkMutation();
  const deleteBookmark = useDeleteBookmarkMutation();

  const [label, setLabel] = useState("");
  const [description, setDescription] = useState("");
  const [insertText, setInsertText] = useState("");

  const handleCreate = async (event: FormEvent) => {
    event.preventDefault();
    if (!label.trim() || !description.trim() || !insertText.trim()) {
      toast.error("Label, description, and insert text are required");
      return;
    }

    try {
      await createBookmark.mutateAsync({ label, description, insertText });
      toast.success("Bookmark created");
      setLabel("");
      setDescription("");
      setInsertText("");
    } catch (error) {
      toast.error(errorMessage(error, "Bookmark creation failed"));
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await deleteBookmark.mutateAsync(id);
      toast.success("Bookmark deleted");
    } catch (error) {
      toast.error(errorMessage(error, "Delete failed"));
    }
  };

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Bookmarks</CardTitle>
          <CardDescription>Reusable snippets/merge-field shortcuts, insertable into any template's body.</CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-8 w-full" />
            </div>
          ) : !bookmarks || bookmarks.length === 0 ? (
            <p className="text-sm text-muted-foreground">No bookmarks yet — add one below.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Label</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>Insert text</TableHead>
                  <TableHead className="w-10" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {bookmarks.map((b) => (
                  <TableRow key={b.id}>
                    <TableCell className="font-medium">{b.label}</TableCell>
                    <TableCell className="text-muted-foreground">{b.description}</TableCell>
                    <TableCell className="font-mono text-xs">{b.insertText}</TableCell>
                    <TableCell>
                      <Button variant="ghost" size="icon" className="size-7" onClick={() => handleDelete(b.id)} disabled={deleteBookmark.isPending}>
                        <Trash2 className="size-3.5" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Add bookmark</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleCreate} className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div className="space-y-1.5">
              <Label htmlFor="bm-label">Label</Label>
              <Input id="bm-label" value={label} onChange={(e) => setLabel(e.target.value)} placeholder="Patient Name" />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="bm-description">Description</Label>
              <Input id="bm-description" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Inserts the patient's name." />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="bm-insert">Insert text</Label>
              <Input id="bm-insert" value={insertText} onChange={(e) => setInsertText(e.target.value)} placeholder="{{patient_name}}" />
            </div>
            <div className="sm:col-span-3 flex justify-end">
              <Button type="submit" size="sm" className="gap-1.5" disabled={createBookmark.isPending}>
                <Plus className="size-3.5" />
                Add bookmark
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
