import { useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useCreateConversationMutation } from "@/hooks/useThreads";
import { errorMessage } from "@/lib/errorMessage";
import { DEFAULT_COUNTRY, toE164, type CountryCode } from "@/lib/phone";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { DateTimePicker } from "@/components/v2/date-time-picker";
import { PhoneInput } from "@/components/v2/phone-input";

/// §6: send SMS to a brand-new patient — creates the Patient + ConversationThread +
/// first outbound message in one call (POST /api/threads).
export function NewConversationDialog({
  open,
  onOpenChange,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (threadId: number) => void;
}) {
  const [name, setName] = useState("");
  const [country, setCountry] = useState<CountryCode>(DEFAULT_COUNTRY);
  const [nationalNumber, setNationalNumber] = useState("");
  const [phoneValid, setPhoneValid] = useState(false);
  const [message, setMessage] = useState("");
  const [scheduledAt, setScheduledAt] = useState("");
  const createConversation = useCreateConversationMutation();

  const reset = () => {
    setName("");
    setCountry(DEFAULT_COUNTRY);
    setNationalNumber("");
    setPhoneValid(false);
    setMessage("");
    setScheduledAt("");
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (!name.trim() || !nationalNumber.trim() || !message.trim()) {
      toast.error("Name, phone, and message are required");
      return;
    }

    const phone = toE164(nationalNumber, country);
    if (!phoneValid || !phone) {
      toast.error("Enter a valid phone number");
      return;
    }

    try {
      const thread = await createConversation.mutateAsync({
        name,
        phone,
        message,
        scheduledAt: scheduledAt ? new Date(scheduledAt).toISOString() : undefined,
      });
      toast.success("Conversation started");
      reset();
      onOpenChange(false);
      onCreated(thread.id);
    } catch (error) {
      toast.error(errorMessage(error, "Could not start conversation"));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New conversation</DialogTitle>
          <DialogDescription>Send an SMS to a patient who doesn't have a thread yet.</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-3">
          <div className="space-y-1.5">
            <Label htmlFor="new-conv-name">Patient name</Label>
            <Input id="new-conv-name" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="new-conv-phone">Phone</Label>
            <PhoneInput
              id="new-conv-phone"
              country={country}
              nationalNumber={nationalNumber}
              onCountryChange={setCountry}
              onNationalNumberChange={setNationalNumber}
              onValidityChange={setPhoneValid}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="new-conv-message">Message</Label>
            <Textarea id="new-conv-message" rows={3} maxLength={1000} value={message} onChange={(e) => setMessage(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="new-conv-scheduled">Schedule for later (optional)</Label>
            <DateTimePicker
              id="new-conv-scheduled"
              value={scheduledAt}
              onChange={setScheduledAt}
              placeholder="Send immediately"
            />
          </div>
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={createConversation.isPending || !phoneValid || !nationalNumber.trim()}>
              {createConversation.isPending ? "Sending..." : "Send"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
