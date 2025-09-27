import { createStaticNavigation, StaticParamList } from '@react-navigation/native';
import { createStackNavigator } from '@react-navigation/stack';
import Home from 'screens/home';
import Details from 'screens/details';
import Ranking from 'screens/ranking';
import CalendarScreen from 'screens/calendar';
import { BackButton } from '../components/BackButton';
import { Login } from 'screens/login';

const Stack = createStackNavigator({
  screens: {
    "Home": {
      screen: Home,
    },
    "Login": {
      screen: Login,
    },
    "Details": {
      screen: Details,
      options: ({ navigation }) => ({
        headerLeft: () => <BackButton onPress={navigation.goBack} />,
      }),
    },
    "Ranking": {
      screen: Ranking,
    },
    "Calendar": {
      screen: CalendarScreen,
    },
  },
  screenOptions: {
    headerShown: false,
  },
});

export type RootNavigatorParamList = StaticParamList<typeof Stack>;

declare global {
  namespace ReactNavigation {
    // eslint-disable-next-line @typescript-eslint/no-empty-object-type
    interface RootParamList extends RootNavigatorParamList {}
  }
}

const Navigation = createStaticNavigation(Stack);
export default Navigation;
