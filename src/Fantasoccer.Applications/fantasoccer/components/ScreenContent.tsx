import { useEffect, type ReactNode } from 'react';
import { Platform, ScrollView, StyleSheet, View } from 'react-native';
import { XStack, YStack } from 'tamagui';
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
  useEffect(() => {
      if (Platform.OS === "web") {
        document.body.style.overflow = "auto";
      }
    }, []);
  return (
    <View style={styles.container}>
      <ScrollView
        style={styles.scroll}
        contentContainerStyle={styles.content}
        showsVerticalScrollIndicator={false}
        scrollEnabled={true}
        alwaysBounceHorizontal={true}
        bounces
      >
        <YStack width="100%" alignSelf="center" gap="$6">
          <XStack
            width="100%"
            justifyContent="space-between"
            alignItems="center"
            paddingHorizontal="$4"
            paddingVertical="$3"
            backgroundColor="$color2"
            borderBottomWidth="$0.5"
            borderBottomColor="$color5"
          >
            <FantasoccerBrand width={48} height={48} textSize="medium" showText={false} />
            {isAuthenticated && <NavigationBar />}
          </XStack>

          <YStack gap="$4" alignItems="center" paddingBottom="$6" width="100%">
            {children}
          </YStack>
        </YStack>
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    width: '100%'
  },
  scroll: {
    flex: 1,
    width: '100%',
  },
  content: {
    flexGrow: 1,
    paddingHorizontal: 0,
    paddingVertical: 0,
  },
});
