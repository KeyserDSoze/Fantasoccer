export interface UserSettings {
  aiPrompt?: string | null;
  autoOptimalFormation: boolean;
}

export const DEFAULT_USER_SETTINGS: UserSettings = {
  aiPrompt: null,
  autoOptimalFormation: false,
};
