// MCP is switched off on fcv2 (Mcp__Enabled=false), so its endpoint answers 404.
// The guide stays reachable by its URL, but nothing in the app invites people to it
// unless the build opts in (EXPO_PUBLIC_MCP_ENABLED=true, also set by the local stack).
export const MCP_ENTRY_POINTS = process.env.EXPO_PUBLIC_MCP_ENABLED === 'true'
