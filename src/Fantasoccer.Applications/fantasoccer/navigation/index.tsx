import { createStaticNavigation, StaticParamList } from '@react-navigation/native';
import { createStackNavigator } from '@react-navigation/stack';
import Home from 'screens/home';
import Details from 'screens/details';
import { BackButton } from '../components/BackButton';
import { Login } from 'screens/login';

const Stack = createStackNavigator({
  screens: {
    home: {
      screen: Home,
    },
    login:{
      screen: Login,
    },
    details: {
      screen: Details,
      options: ({ navigation }) => ({
        headerLeft: () => <BackButton onPress={navigation.goBack} />,
      }),
    },
  },
});

type RootNavigatorParamList = StaticParamList<typeof Stack>;

declare global {
  namespace ReactNavigation {
    // eslint-disable-next-line @typescript-eslint/no-empty-object-type
    interface RootParamList extends RootNavigatorParamList {}
  }
}

const Navigation = createStaticNavigation(Stack);
export default Navigation;
