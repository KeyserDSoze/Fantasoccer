import { createContext, type ReactNode, useContext, useState } from 'react';

const BottomInsetContext = createContext(0);
const SetBottomInsetContext = createContext<(height: number) => void>(() => {});

/** The floating widget owns its measured inset; scrolling screens only consume it. */
export function LiveWidgetLayoutProvider({ children }: { children: ReactNode }) {
  const [bottomInset, setBottomInset] = useState(0);

  return (
    <SetBottomInsetContext.Provider value={setBottomInset}>
      <BottomInsetContext.Provider value={bottomInset}>
        {children}
      </BottomInsetContext.Provider>
    </SetBottomInsetContext.Provider>
  );
}

export const useLiveWidgetBottomInset = () => useContext(BottomInsetContext);
export const useSetLiveWidgetBottomInset = () => useContext(SetBottomInsetContext);
