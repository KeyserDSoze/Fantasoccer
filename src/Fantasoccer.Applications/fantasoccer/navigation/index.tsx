import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator, NativeStackNavigationProp } from '@react-navigation/native-stack';
import Home from 'screens/home';
import Details from 'screens/details';
import Ranking from 'screens/ranking';
import CalendarScreen from 'screens/calendar';
import PlayersScreen from 'screens/players';
import LiveScreen from 'screens/live';
import GameScreen from 'screens/game';
import SettingsScreen from 'screens/settings';
import { BackButton } from '../components/layout/BackButton';
import { Login } from 'screens/login';

// Definisco i tipi per i parametri delle schermate
export type RootStackParamList = {
  Home: undefined;
  Login: undefined;
  Details: { name?: string };
  Ranking: undefined;
  Calendar: undefined;
  Players: undefined;
  Live: undefined;
  Game: { gameId?: string };
  Settings: undefined;
};

// Tipo per la navigazione
export type NavigationProp = NativeStackNavigationProp<RootStackParamList>;

// Dichiarazione globale per React Navigation
declare global {
  namespace ReactNavigation {
    interface RootParamList extends RootStackParamList {}
  }
}

const Stack = createNativeStackNavigator<RootStackParamList>();

// Configurazione linking completa per URL sync
const linking = {
  prefixes: ['http://localhost:3000', 'https://fanta.zone', 'fantasoccer://'],
  config: {
    screens: {
      Home: '',  // root path
      Login: 'login',
      Details: 'details/:name?', // supporta parametro name opzionale
      Ranking: 'ranking',
      Calendar: 'calendar',
      Players: 'players',
      Live: 'live',
      Game: 'game/:gameId?', // supporta parametri
      Settings: 'settings',
    },
  },
};

export default function Navigation() {
  return (
    <NavigationContainer linking={linking}>
      <Stack.Navigator 
        screenOptions={{
          headerShown: false,
        }}
      >
        <Stack.Screen name="Home" component={Home} />
        <Stack.Screen name="Login" component={Login} />
        <Stack.Screen 
          name="Details" 
          component={Details}
          options={{
            headerShown: true,
            headerLeft: () => <BackButton onPress={() => {}} />,
          }}
        />
        <Stack.Screen name="Ranking" component={Ranking} />
        <Stack.Screen name="Calendar" component={CalendarScreen} />
        <Stack.Screen name="Players" component={PlayersScreen} />
        <Stack.Screen name="Live" component={LiveScreen} />
        <Stack.Screen name="Game" component={GameScreen} />
        <Stack.Screen name="Settings" component={SettingsScreen} />
      </Stack.Navigator>
    </NavigationContainer>
  );
}
