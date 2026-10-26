import { GroupCreationContent } from '../components/groups/GroupCreationContent'
import { ScreenContent } from '../components/layout/ScreenContent'
import { useAuth } from '../providers/AuthProvider'

export default function CreateGroupScreen() {
  const { user } = useAuth()
  return <ScreenContent><GroupCreationContent key={user?.email ?? 'anonymous'} /></ScreenContent>
}
