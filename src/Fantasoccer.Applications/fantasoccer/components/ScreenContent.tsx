import { YStack, H2, Separator, Theme } from 'tamagui';
import { NavigationBar } from './NavigationBar';

type ScreenContentProps = {
  title: string;
  path: string;
  children?: React.ReactNode;
};

export const ScreenContent = ({ title, path, children }: ScreenContentProps) => {
  return (
    <Theme name="dark">
      <YStack gap="$6" maxWidth={600} alignItems="center">
        <YStack gap="$4" alignItems="center">
          <NavigationBar />
          {children}
        </YStack>
      </YStack>
    </Theme>
  );
};
