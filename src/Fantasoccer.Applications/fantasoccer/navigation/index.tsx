import { NavigationContainer, Theme } from '@react-navigation/native';
import { createNativeStackNavigator, NativeStackNavigationProp } from '@react-navigation/native-stack';
import Home from 'screens/home';
import Details from 'screens/details';
import Ranking from 'screens/ranking';
import CalendarScreen from 'screens/calendar';
import PlayersScreen from 'screens/players';
import LiveScreen from 'screens/live';
import GameScreen from 'screens/game';
import SettingsScreen from 'screens/settings';
import TeamsScreen from 'screens/teams';
import EditScreen from 'screens/edit';
import MarketScreen from 'screens/market';
import MarketTradesScreen from 'screens/market-trades';
import HallOfFameScreen from 'screens/hall-of-fame';
import RulesScreen from 'screens/rules';
import { BackButton } from '../components/layout/BackButton';
import { Login } from 'screens/login';

// Props per il componente Navigation
interface NavigationProps {
  theme: Theme;
  onReady?: () => void;
}

// Definisco i tipi per i parametri delle schermate
export type RootStackParamList = {
  Home: undefined;
  Login: undefined;
  Details: { name?: string };
  Ranking: undefined;
  Calendar: undefined;
  Players: undefined;
  Teams: undefined;
  Edit: undefined;
  Live: undefined;
  Game: { 
    gameId?: string | null;
    leagueId?: string | null;
    year?: string | null;
    groupId?: string | null;
  };
  Market: undefined;
  MarketTrades: undefined;
  HallOfFame: undefined;
  Rules: undefined;
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
  prefixes: ['http://localhost:3000', 'https://fanta.zone', 'fantazone://'],
  config: {
    screens: {
      Home: '',  // root path
      Login: 'login',
      Details: 'details/:name?', // supporta parametro name opzionale
      Ranking: 'ranking',
      Calendar: 'calendar',
      Players: 'players',
      Teams: 'teams',
      Edit: 'edit',
      Live: 'live',
      Game: 'game/:groupId?/:leagueId?/:year?/:gameId?', // supporta parametri
      Market: 'market',
      MarketTrades: 'market/trades',
      HallOfFame: 'hall-of-fame',
      Rules: 'rules',
      Settings: 'settings',
    },
  },
};

export default function Navigation({ theme, onReady }: NavigationProps) {
  return (
    <NavigationContainer 
      linking={linking}
      theme={theme}
      onReady={onReady}
    >
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
        <Stack.Screen name="Teams" component={TeamsScreen} />
        <Stack.Screen name="Edit" component={EditScreen} />
        <Stack.Screen name="Live" component={LiveScreen} />
        <Stack.Screen name="Game" component={GameScreen} />
        <Stack.Screen name="Market" component={MarketScreen} />
        <Stack.Screen name="MarketTrades" component={MarketTradesScreen} />
        <Stack.Screen name="HallOfFame" component={HallOfFameScreen} />
        <Stack.Screen name="Rules" component={RulesScreen} />
        <Stack.Screen name="Settings" component={SettingsScreen} />
      </Stack.Navigator>
    </NavigationContainer>
  );
}
