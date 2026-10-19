import { useEffect, type ReactNode } from 'react';
import { Platform, ScrollView, StyleSheet, View } from 'react-native';
import { XStack, YStack, useMedia } from 'tamagui';
import { NavigationBar } from './NavigationBar';
import { FantasoccerBrand } from './FantasoccerLogo';
import { useAuth } from 'providers/AuthProvider';
import { useLiveWidgetBottomInset } from 'providers/LiveWidgetLayoutProvider';

type ScreenContentProps = {
  title?: string;
  path?: string;
  children?: ReactNode;
};

export const ScreenContent = ({ children }: ScreenContentProps) => {
  const { isAuthenticated } = useAuth();
  const bottomInset = useLiveWidgetBottomInset();
  const media = useMedia();
  const isWideLayout = media.gtMd ?? false;
  const contentWidth = isWideLayout ? '100%' : 640;
  const horizontalPadding = isWideLayout ? '$6' : '$4';
  useEffect(() => {
      if (Platform.OS === "web") {
        document.body.style.overflow = "auto";
      }
    }, []);
  return (
    <View style={styles.container}>
      <ScrollView
        testID="screen-scroll"
        style={styles.scroll}
        contentContainerStyle={[styles.content, { paddingBottom: bottomInset }]}
        scrollIndicatorInsets={{ bottom: bottomInset }}
        showsVerticalScrollIndicator={false}
        scrollEnabled={true}
        alwaysBounceHorizontal={true}
        bounces
      >
        <YStack width="100%" alignItems="center" gap="$6" flexShrink={0}>
          <XStack
            width="100%"
            maxWidth={contentWidth}
            alignSelf="center"
            justifyContent="space-between"
            alignItems="center"
            paddingHorizontal={horizontalPadding}
            paddingVertical="$3"
            backgroundColor="$color2"
            borderBottomWidth="$0.5"
            borderBottomColor="$color5"
            borderRadius={isWideLayout ? '$0' : '$4'}
          >
            <FantasoccerBrand width={40} height={40} textSize="medium" showText={false} />
            {isAuthenticated && <NavigationBar />}
          </XStack>

          <YStack
            gap="$4"
            width="100%"
            maxWidth={contentWidth}
            alignSelf="center"
            paddingHorizontal={horizontalPadding}
            paddingBottom="$6"
          >
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
