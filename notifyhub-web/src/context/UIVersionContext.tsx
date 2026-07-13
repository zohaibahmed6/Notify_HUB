import { createContext, useContext, useEffect, useState, type ReactNode } from "react";

import { getInitialUIVersion, setStoredUIVersion, type UIVersion } from "@/config/uiVersion";

interface UIVersionContextValue {
  version: UIVersion;
  setVersion: (version: UIVersion) => void;
  toggleVersion: () => void;
}

const UIVersionContext = createContext<UIVersionContextValue | undefined>(undefined);

export function UIVersionProvider({ children }: { children: ReactNode }) {
  const [version, setVersionState] = useState<UIVersion>(getInitialUIVersion);

  // Drives the .redesign CSS scope (src/index.css) the same way a dark-mode toggle
  // would drive .dark — legacy screens never get this class, so their tokens stay
  // the untouched shadcn defaults.
  useEffect(() => {
    document.documentElement.classList.toggle("redesign", version === "redesign");
  }, [version]);

  const setVersion = (next: UIVersion) => {
    setStoredUIVersion(next);
    setVersionState(next);
  };

  const toggleVersion = () => setVersion(version === "redesign" ? "legacy" : "redesign");

  return (
    <UIVersionContext.Provider value={{ version, setVersion, toggleVersion }}>
      {children}
    </UIVersionContext.Provider>
  );
}

export function useUIVersion(): UIVersionContextValue {
  const ctx = useContext(UIVersionContext);
  if (!ctx) throw new Error("useUIVersion must be used within a UIVersionProvider");
  return ctx;
}
