import { NavigationContainer, Theme, useNavigation } from '@react-navigation/native';
import {
  createNativeStackNavigator,
  NativeStackNavigationProp,
} from '@react-navigation/native-stack';
import { useEffect, useRef, useState } from 'react';
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
import InfoScreen from 'screens/info';
import UsersScreen from 'screens/users';
import LogsScreen from 'screens/logs';
import SerieAAdminScreen from 'screens/serie-a-admin';
import { GlobalMatchWidget } from 'components/live/GlobalMatchWidget';
import GroupUsersAdminScreen from 'screens/group-users-admin';
import GroupBasketsAdminScreen from 'screens/group-baskets-admin';
import GroupLeagueAdminScreen from 'screens/group-league-admin';
import CardsAdminScreen from 'screens/cards-admin';
import CardEditScreen from 'screens/card-edit';
import AuctionCreateScreen from 'screens/auction-create';
import AuctionLiveScreen from 'screens/auction-live';
import AuctionDisplayScreen from 'screens/auction-display';
import PatchNotesScreen from 'screens/patch-notes';
import { BackButton } from '../components/layout/BackButton';
import { Login } from 'screens/login';
import PushNotificationScreen from 'screens/push-notification';
import { VersionUpdateBanner } from 'components/version/VersionUpdateBanner';
import OpeningCompetitionScreen from 'screens/opening-competition';

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
  OpeningCompetition: undefined;
  Calendar: undefined;
  Players: undefined;
  Teams: undefined;
  Edit: { leagueId?: string; year?: number } | undefined;
  Live: { leagueId?: string; groupId?: string } | undefined;
  Game: {
    gameId?: string | null;
    leagueId?: string | null;
    year?: string | null;
    groupId?: string | null;
    editMode?: 'formation' | 'live' | 'view';
  };
  Market: undefined;
  MarketTrades: undefined;
  HallOfFame: undefined;
  Rules: undefined;
  Users: undefined;
  Logs: undefined;
  SerieAAdmin: undefined;
  GroupUsersAdmin: undefined;
  GroupBasketsAdmin: undefined;
  GroupLeagueAdmin: undefined;
  CardsAdmin: undefined;
  CardEdit: { cardId?: string | null };
  Settings: undefined;
  Info: undefined;
  PatchNotes: undefined;
  PushNotification: undefined;
  AuctionCreate: undefined;
  AuctionLive: { auctionId: string };
  AuctionDisplay: { auctionId: string };
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

// Internal component with navigation access for auth-failure handling
function NavigationListener() {
  const navigation = useNavigation<NavigationProp>();
  const hasNavigatedRef = useRef(false);

  useEffect(() => {
    console.log('🎧 [NavigationListener] Mounted and listening for auth-failure events');

    const handleAuthFailure = () => {
      console.log('� [NavigationListener] AUTH-FAILURE EVENT RECEIVED!');
      if (!hasNavigatedRef.current) {
        hasNavigatedRef.current = true;
        console.log('🏠 [NavigationListener] Navigating to Home...');
        // Navigate to Home screen
        navigation.reset({
          index: 0,
          routes: [{ name: 'Home' }],
        });
        console.log('✅ [NavigationListener] Navigation completed');
        // Reset the flag after a delay to allow future navigations
        setTimeout(() => {
          hasNavigatedRef.current = false;
        }, 1000);
      } else {
        console.log('⚠️ [NavigationListener] Already navigated, skipping');
      }
    };

    if (typeof window !== 'undefined') {
      window.addEventListener('auth-failure', handleAuthFailure);
      console.log('✅ [NavigationListener] Event listener registered');
      return () => {
        console.log('🔇 [NavigationListener] Removing event listener');
        window.removeEventListener('auth-failure', handleAuthFailure);
      };
    } else {
      console.log('⚠️ [NavigationListener] Window not available (React Native?)');
    }
  }, [navigation]);

  return null;
}

// Configurazione linking completa per URL sync
const linking = {
  prefixes: ['http://localhost:3000', 'https://fanta.zone', 'fantazone://'],
  config: {
    screens: {
      Home: '', // root path
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
      Users: 'users',
      Logs: 'logs',
      SerieAAdmin: 'serie-a-admin',
      GroupUsersAdmin: 'group-users-admin',
      GroupBasketsAdmin: 'group-baskets-admin',
      GroupLeagueAdmin: 'group-league-admin',
      CardsAdmin: 'cards-admin',
      CardEdit: 'card-edit/:cardId?',
      Settings: 'settings',
      Info: 'info',
      PatchNotes: 'patch-notes',
      PushNotification: 'push-notification',
      AuctionCreate: 'auction/create',
      AuctionLive: 'auction/:auctionId',
      AuctionDisplay: 'auction/:auctionId/display',
    },
  },
};

export default function Navigation({ theme, onReady }: NavigationProps) {
  const [activeRouteName, setActiveRouteName] = useState('Home');

  return (
    <NavigationContainer
      linking={linking}
      theme={theme}
      onReady={onReady}
      onStateChange={(state) =>
        setActiveRouteName(state ? (state.routes[state.index]?.name ?? 'Home') : 'Home')
      }>
      <Stack.Navigator
        screenOptions={{
          headerShown: false,
        }}>
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
        <Stack.Screen name="OpeningCompetition" component={OpeningCompetitionScreen} />
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
        <Stack.Screen name="Users" component={UsersScreen} />
        <Stack.Screen name="Logs" component={LogsScreen} />
        <Stack.Screen name="SerieAAdmin" component={SerieAAdminScreen} />
        <Stack.Screen name="GroupUsersAdmin" component={GroupUsersAdminScreen} />
        <Stack.Screen name="GroupBasketsAdmin" component={GroupBasketsAdminScreen} />
        <Stack.Screen name="GroupLeagueAdmin" component={GroupLeagueAdminScreen} />
        <Stack.Screen name="CardsAdmin" component={CardsAdminScreen} />
        <Stack.Screen name="CardEdit" component={CardEditScreen} />
        <Stack.Screen name="Settings" component={SettingsScreen} />
        <Stack.Screen name="Info" component={InfoScreen} />
        <Stack.Screen name="PatchNotes" component={PatchNotesScreen} />
        <Stack.Screen name="PushNotification" component={PushNotificationScreen} />
        <Stack.Screen name="AuctionCreate" component={AuctionCreateScreen} />
        <Stack.Screen name="AuctionLive" component={AuctionLiveScreen} />
        <Stack.Screen name="AuctionDisplay" component={AuctionDisplayScreen} />
      </Stack.Navigator>
      {/* Listen for auth failures and redirect to Home */}
      <NavigationListener />
      {/* Version Update Banner - dentro NavigationContainer per accesso a navigation */}
      <VersionUpdateBanner />
      <GlobalMatchWidget activeRouteName={activeRouteName} />
    </NavigationContainer>
  );
}
