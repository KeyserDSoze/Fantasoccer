import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { ScreenContent } from 'components/layout/ScreenContent';
import { StyleSheet, View } from 'react-native';

type RootStackParamList = {
  Details: { name?: string };
};

type Props = NativeStackScreenProps<RootStackParamList, 'Details'>;

export default function Details({ route }: Props) {
  return (
    <View style={styles.container}>
      <ScreenContent
        path="screens/details.tsx"
        title={`Showing details for user ${route.params?.name || 'unknown'}`}
      />
    </View>
  );
}

export const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 0,
  },
});
