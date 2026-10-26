// Team logo utility functions
// Generates URLs for team logos stored in Azure Blob Storage

/**
 * Generates the URL for a team logo
 * Pattern: https://fasostorage.blob.core.windows.net/logos/{groupId}/{year}/{email}.png
 */
export const getTeamLogoUrl = (groupId: string, year: number, email: string): string => {
  const baseUrl = process.env.EXPO_PUBLIC_BLOB_URL?.replace(/\/$/, '') || 'https://fasostorage.blob.core.windows.net'
  return `${baseUrl}/logos/${encodeURIComponent(groupId)}/${encodeURIComponent(year)}/${encodeURIComponent(email)}.png`
}

/**
 * Generates the URL for a team logo with fallback
 * If the main logo fails to load, provides a fallback URL or placeholder
 */
export const getTeamLogoUrlWithFallback = (
  groupId: string, 
  year: number, 
  email: string,
  fallbackUrl?: string
): { src: string; fallback?: string } => {
  const mainUrl = getTeamLogoUrl(groupId, year, email)
  
  return {
    src: mainUrl,
    fallback: fallbackUrl
  }
}

/**
 * Generates a placeholder logo URL or data URI for when team logo is not available
 */
export const getPlaceholderLogoUrl = (): string => {
  // Return a simple data URI for a placeholder
  return "data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAiIGhlaWdodD0iNDAiIHZpZXdCb3g9IjAgMCA0MCA0MCIgZmlsbD0ibm9uZSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj4KPGNpcmNsZSBjeD0iMjAiIGN5PSIyMCIgcj0iMjAiIGZpbGw9IiNGNUY1RjUiLz4KPHN2ZyB4PSI4IiB5PSI4IiB3aWR0aD0iMjQiIGhlaWdodD0iMjQiIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSI+CjxwYXRoIGQ9Ik0xMiAyQzYuNDggMiAyIDYuNDggMiAxMnM0LjQ4IDEwIDEwIDEwIDEwLTQuNDggMTAtMTBTMTcuNTIgMiAxMiAyem0wIDNjMS42NiAwIDMgMS4zNCAzIDNzLTEuMzQgMy0zIDMtMy0xLjM0LTMtM3MxLjM0LTMgMy0zem0wIDEyLjJjLTIuNSAwLTQuNzEtMS4yOC02LTMuMjIuMDMtMS45OSA0LTMuMDggNi0zLjA4czUuOTcgMS4wOSA2IDMuMDhjLTEuMjkgMS45NC0zLjUgMy4yMi02IDMuMjJ6IiBmaWxsPSIjOUI5Qjk5Ii8+Cjwvc3ZnPgo8L3N2Zz4K"
}

/**
 * Check if an image URL is accessible (for preloading)
 */
export const preloadImage = (src: string): Promise<boolean> => {
  return new Promise((resolve) => {
    const img = new Image()
    img.onload = () => resolve(true)
    img.onerror = () => resolve(false)
    img.src = src
  })
}

/**
 * Component props for team logo with built-in fallback handling
 */
export interface TeamLogoProps {
  groupId: string
  year: number
  email: string
  size?: number
  className?: string
  alt?: string
}

/**
 * Get optimized logo props for img element with fallback handling
 */
export const getTeamLogoProps = (props: TeamLogoProps) => {
  const { groupId, year, email, size = 40, className, alt } = props
  
  return {
    src: getTeamLogoUrl(groupId, year, email),
    width: size,
    height: size,
    alt: alt || `Logo ${email}`,
    className,
    style: {
      borderRadius: '50%',
      objectFit: 'cover' as const,
    },
    onError: (e: React.SyntheticEvent<HTMLImageElement>) => {
      // Fallback to placeholder on error
      e.currentTarget.src = getPlaceholderLogoUrl()
    }
  }
}
