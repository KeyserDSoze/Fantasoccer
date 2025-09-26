import type { ReactNode } from 'react';
import { Theme, XStack, YStack } from 'tamagui';
import { NavigationBar } from './NavigationBar';
import { FantasoccerBrand } from './FantasoccerLogo';
import { useAuth } from 'providers/AuthProvider';

type ScreenContentProps = {
  title?: string;
  path?: string;
  children?: ReactNode;
};

export const ScreenContent = ({ children }: ScreenContentProps) => {
  const { isAuthenticated } = useAuth();

  return (
    <Theme name="dark">
      <YStack width="100%" maxWidth={900} alignSelf="center" gap="$6">
        <XStack
          width="100%"
          justifyContent="space-between"
          alignItems="center"
          paddingHorizontal="$3"
          paddingVertical="$3"
        >
          <FantasoccerBrand width={48} height={48} textSize="medium" showText={false}/>
          {isAuthenticated && <NavigationBar />}
        </XStack>

        <YStack gap="$4" alignItems="center">
          {children}
        </YStack>
      </YStack>
    </Theme>
  );
};
