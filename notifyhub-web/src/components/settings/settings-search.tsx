import { useMemo, useState } from "react";
import { Search } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { SETTINGS_SEARCH_INDEX, type SettingsSearchItem, type SettingsTabValue } from "@/components/settings/settings-search-index";

interface SettingsSearchProps {
  isAdmin: boolean;
  onNavigate: (tab: SettingsTabValue, sectionId: string) => void;
}

/// Cross-tab settings finder — lists every setting Card across all Settings tabs and lets
/// the user jump straight to one instead of hunting through tabs manually.
export function SettingsSearch({ isAdmin, onNavigate }: SettingsSearchProps) {
  const [open, setOpen] = useState(false);

  const groups = useMemo(() => {
    const visible = SETTINGS_SEARCH_INDEX.filter((item) => !item.adminOnly || isAdmin);
    const byGroup = new Map<string, SettingsSearchItem[]>();
    for (const item of visible) {
      const list = byGroup.get(item.group) ?? [];
      list.push(item);
      byGroup.set(item.group, list);
    }
    return Array.from(byGroup.entries());
  }, [isAdmin]);

  const handleSelect = (item: SettingsSearchItem) => {
    onNavigate(item.tab, item.id);
    setOpen(false);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className="w-full justify-start gap-2 text-muted-foreground sm:w-80"
        >
          <Search className="size-4" />
          Search settings...
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-80 p-0" align="start">
        <Command>
          <CommandInput placeholder="Search settings..." />
          <CommandList>
            <CommandEmpty>No settings found.</CommandEmpty>
            {groups.map(([group, items]) => (
              <CommandGroup key={group} heading={group}>
                {items.map((item) => (
                  <CommandItem key={item.id} value={`${item.label} ${item.group}`} onSelect={() => handleSelect(item)}>
                    {item.label}
                  </CommandItem>
                ))}
              </CommandGroup>
            ))}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
